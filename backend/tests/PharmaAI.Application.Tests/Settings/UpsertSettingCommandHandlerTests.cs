using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Settings.Commands.UpsertSetting;
using PharmaAI.Application.Settings.Queries.GetSetting;
using PharmaAI.Application.Tests.TestSupport;

namespace PharmaAI.Application.Tests.Settings;

public sealed class UpsertSettingCommandHandlerTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _clock = new(T0);

    [Fact]
    public async Task Inserts_a_new_setting()
    {
        await using var db = _database.CreateContext();

        var result = await new UpsertSettingCommandHandler(db, _clock)
            .Handle(new UpsertSettingCommand("pharmacy.name", "صيدلية الشفاء"), CancellationToken.None);

        Assert.Equal("صيدلية الشفاء", result.Value);
        Assert.Equal(T0.UtcDateTime, result.UpdatedAtUtc);

        await using var verify = _database.CreateContext();
        Assert.Equal("صيدلية الشفاء", (await verify.AppSettings.SingleAsync()).Value);
    }

    [Fact]
    public async Task Updates_an_existing_setting_without_duplicating_it()
    {
        await using (var db = _database.CreateContext())
        {
            await new UpsertSettingCommandHandler(db, _clock)
                .Handle(new UpsertSettingCommand("pharmacy.name", "old"), CancellationToken.None);
        }

        _clock.Now = T0.AddHours(1);
        await using (var db = _database.CreateContext())
        {
            await new UpsertSettingCommandHandler(db, _clock)
                .Handle(new UpsertSettingCommand("pharmacy.name", "new"), CancellationToken.None);
        }

        await using var verify = _database.CreateContext();
        var stored = await verify.AppSettings.SingleAsync();
        Assert.Equal("new", stored.Value);
        Assert.Equal(T0.AddHours(1).UtcDateTime, stored.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedAtUtc.Kind);
    }

    [Fact]
    public async Task GetSetting_returns_null_for_unknown_key()
    {
        await using var db = _database.CreateContext();

        Assert.Null(await new GetSettingQueryHandler(db).Handle(new GetSettingQuery("missing.key"), CancellationToken.None));
    }

    public void Dispose() => _database.Dispose();
}
