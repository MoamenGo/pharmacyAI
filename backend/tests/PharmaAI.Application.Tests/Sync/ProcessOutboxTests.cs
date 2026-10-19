using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Sync;
using PharmaAI.Application.Sync.Commands.ProcessOutbox;
using PharmaAI.Application.Sync.Commands.RetryOutboxMessage;
using PharmaAI.Application.Sync.Commands.SaveEtaSettings;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Application.Sync.Queries.GetSyncStatus;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Sales;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Tests.Sync;

public sealed class ProcessOutboxTests : IDisposable
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeEtaClient _eta = new();
    private Guid _drugId;

    private sealed class NoTrigger : ISyncTrigger
    {
        public void RequestSync()
        {
        }
    }

    private async Task SetupAsync(bool configure = true, bool enable = true)
    {
        await using var db = _database.CreateContext();
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), 30, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        _drugId = drug.Id;

        if (!configure && enable)
        {
            // Turned on, but none of the registration details filled in yet.
            await new SaveEtaSettingsCommandHandler(db, _clock, new NoTrigger()).Handle(
                new SaveEtaSettingsCommand(
                    true, EtaEnvironment.PreProduction, "", null, "", "", "", null, "", "", "0", "", "", "", "", "4772"),
                CancellationToken.None);
        }

        if (configure)
        {
            await new SaveEtaSettingsCommandHandler(db, _clock, new NoTrigger()).Handle(
                new SaveEtaSettingsCommand(
                    true, EtaEnvironment.PreProduction, "client", "secret", "POS-001", "windows", "1", "key",
                    "123456789", "صيدلية الشفاء", "0", "القاهرة", "مدينة نصر", "عباس العقاد", "12", "4772"),
                CancellationToken.None);
        }
    }

    private async Task SellAsync()
    {
        await using var db = _database.CreateContext();
        await new CreateSaleCommandHandler(db, _clock, new PharmacyCalendar(_clock)).Handle(
            new CreateSaleCommand([new CreateSaleItem(_drugId, SaleUnit.SubUnit, 1)], 100_000), CancellationToken.None);
    }

    private async Task<ProcessOutboxResult> ProcessAsync()
    {
        await using var db = _database.CreateContext();
        return await new ProcessOutboxCommandHandler(db, _eta, _clock).Handle(new ProcessOutboxCommand(), CancellationToken.None);
    }

    private async Task<List<OutboxMessage>> MessagesAsync()
    {
        await using var db = _database.CreateContext();
        return await db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync();
    }

    private static string Header(string json, string field)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("header").TryGetProperty(field, out var value) ? value.GetString()! : "<none>";
    }

    [Fact]
    public async Task Without_the_registration_details_nothing_is_built_or_sent()
    {
        await SetupAsync(configure: false);
        await SellAsync();

        Assert.False((await ProcessAsync()).Configured);
        Assert.Empty(_eta.Calls);
        Assert.Null(Assert.Single(await MessagesAsync()).Document);
    }

    [Fact]
    public async Task Receipts_are_chained_in_sale_order_and_sent_together()
    {
        await SetupAsync();
        await SellAsync();
        await SellAsync();
        await SellAsync();

        var result = await ProcessAsync();

        Assert.Equal((3, 3), (result.Built, result.Sent));
        var messages = await MessagesAsync();
        Assert.All(messages, m => Assert.Equal(OutboxStatus.Sent, m.Status));
        Assert.Equal(["", messages[0].ExternalId!, messages[1].ExternalId!], messages.Select(m => Header(m.Document!, "previousUUID")));
        Assert.Equal(messages.Select(m => m.Document), Assert.Single(_eta.Calls));
    }

    [Fact]
    public async Task A_later_sale_chains_to_the_last_receipt_already_sent()
    {
        await SetupAsync();
        await SellAsync();
        await ProcessAsync();

        await SellAsync();
        await ProcessAsync();

        var messages = await MessagesAsync();
        Assert.Equal(messages[0].ExternalId, Header(messages[1].Document!, "previousUUID"));
    }

    [Fact]
    public async Task When_eta_is_unreachable_the_same_receipts_are_resent_after_the_back_off()
    {
        await SetupAsync();
        await SellAsync();
        _eta.FailWith = "Could not reach ETA";

        Assert.Equal(1, (await ProcessAsync()).Failed);
        var failed = Assert.Single(await MessagesAsync());
        Assert.Equal((OutboxStatus.Pending, 1, "Could not reach ETA"), (failed.Status, failed.Attempts, failed.LastError));

        _eta.FailWith = null;
        Assert.Equal(0, (await ProcessAsync()).Sent);

        _clock.Now = _clock.Now.AddSeconds(30);
        Assert.Equal(1, (await ProcessAsync()).Sent);
        Assert.Equal(_eta.Calls[0], _eta.Calls[^1]);
        Assert.Equal(OutboxStatus.Sent, Assert.Single(await MessagesAsync()).Status);
    }

    [Fact]
    public async Task A_rejected_receipt_waits_for_a_person_then_is_resent_as_a_correction()
    {
        await SetupAsync();
        await SellAsync();
        _eta.RejectReceiptNumbers.Add("1");

        Assert.Equal(1, (await ProcessAsync()).Rejected);
        var rejected = (await MessagesAsync())[0];
        Assert.Equal((OutboxStatus.Rejected, "CF313 Issuance date time value is out of range"), (rejected.Status, rejected.LastError));

        // Still rejected on later passes until someone asks for a retry.
        _clock.Now = _clock.Now.AddDays(1);
        Assert.Equal(0, (await ProcessAsync()).Sent);

        _eta.RejectReceiptNumbers.Clear();
        await using (var db = _database.CreateContext())
        {
            await new RetryOutboxMessageCommandHandler(db, _clock, new NoTrigger()).Handle(new RetryOutboxMessageCommand(rejected.Id), CancellationToken.None);
        }

        Assert.Equal((1, 1), ((await ProcessAsync()).Built, _eta.Calls[^1].Count));
        var messages = await MessagesAsync();
        var corrected = messages[0];
        Assert.Equal(OutboxStatus.Sent, corrected.Status);
        Assert.NotEqual(rejected.ExternalId, corrected.ExternalId);
        Assert.Equal(rejected.ExternalId, Header(corrected.Document!, "referenceOldUUID"));
        Assert.Equal("", Header(corrected.Document!, "previousUUID"));

        // The next receipt chains to the UUID the corrected receipt was first issued under.
        await SellAsync();
        await ProcessAsync();
        Assert.Equal(rejected.ExternalId, Header((await MessagesAsync())[1].Document!, "previousUUID"));
    }

    [Fact]
    public async Task A_receipt_eta_does_not_mention_is_retried()
    {
        await SetupAsync();
        await SellAsync();
        var silent = new SilentEtaClient();

        await using var db = _database.CreateContext();
        var result = await new ProcessOutboxCommandHandler(db, silent, _clock).Handle(new ProcessOutboxCommand(), CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(OutboxStatus.Pending, Assert.Single(await MessagesAsync()).Status);
    }

    [Fact]
    public async Task Status_counts_receipts_and_lists_problems()
    {
        await SetupAsync();
        await SellAsync();
        await SellAsync();
        await SellAsync();
        _eta.RejectReceiptNumbers.Add("2");
        await ProcessAsync();

        await using var db = _database.CreateContext();
        var status = await new GetSyncStatusQueryHandler(db).Handle(new GetSyncStatusQuery(), CancellationToken.None);

        Assert.Equal((true, 0, 2, 1), (status.Enabled, status.Pending, status.Sent, status.Rejected));
        Assert.Empty(status.MissingFields);
        var problem = Assert.Single(status.Problems);
        Assert.Equal((2L, OutboxStatus.Rejected), (problem.SaleNumber, problem.Status));
    }

    private sealed class SilentEtaClient : IEtaClient
    {
        public Task<EtaSubmitResult> SubmitAsync(EtaSettings settings, IReadOnlyList<string> receipts, CancellationToken cancellationToken) =>
            Task.FromResult(new EtaSubmitResult(new HashSet<string>(), new Dictionary<string, string>(), null));
    }

    public void Dispose() => _database.Dispose();
}
