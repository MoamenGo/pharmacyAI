using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Application.Inventory.Commands.CreateDrug;
using PharmaAI.Application.Inventory.Commands.DeductStock;
using PharmaAI.Application.Inventory.Commands.ReceiveStock;
using PharmaAI.Application.Inventory.Queries.GetDrug;
using PharmaAI.Application.Inventory.Queries.ListDrugs;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Tests.Inventory;

public sealed class InventoryHandlerTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);
    private static readonly DateOnly Today = new(2026, 10, 17);

    private readonly SqliteTestDatabase _database = new();

    private async Task<Guid> CreateDrugAsync(string barcode = "6221234567890", int subUnitsPerPack = 3)
    {
        await using var db = _database.CreateContext();
        var created = await new CreateDrugCommandHandler(db, Clock, Calendar).Handle(
            new CreateDrugCommand("بانادول اكسترا", "Panadol Extra", barcode, "علبة", "شريط", subUnitsPerPack, 4500),
            CancellationToken.None);
        return created.Drug.Id;
    }

    private async Task ReceiveAsync(Guid drugId, string batch, DateOnly expiry, int packs, int loose = 0)
    {
        await using var db = _database.CreateContext();
        await new ReceiveStockCommandHandler(db, Clock, Calendar).Handle(
            new ReceiveStockCommand(drugId, batch, expiry, packs, loose), CancellationToken.None);
    }

    private async Task<Dictionary<string, int>> StoredQuantitiesAsync()
    {
        await using var db = _database.CreateContext();
        return await db.StockBatches.ToDictionaryAsync(b => b.BatchNumber, b => b.QuantityInSubUnits);
    }

    [Fact]
    public async Task CreateDrug_stores_price_as_exact_decimal_and_returns_piasters()
    {
        var id = await CreateDrugAsync();

        await using var db = _database.CreateContext();
        Assert.Equal(45.00m, (await db.Drugs.SingleAsync(d => d.Id == id)).PublicPrice);

        var details = await new GetDrugQueryHandler(db, Calendar).Handle(new GetDrugQuery(Id: id), CancellationToken.None);
        Assert.Equal(4500, details!.Drug.PublicPricePiasters);
        Assert.Equal(1500, details.Drug.SubUnitPricePiasters);
    }

    [Fact]
    public async Task ReceiveStock_converts_packs_and_loose_strips_to_sub_units()
    {
        var id = await CreateDrugAsync();

        await ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 2, loose: 1);

        Assert.Equal(7, (await StoredQuantitiesAsync())["B1"]);
    }

    [Fact]
    public async Task ReceiveStock_merges_a_repeated_batch_in_the_database()
    {
        var id = await CreateDrugAsync();

        await ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 1);
        await ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 2);

        Assert.Equal(new Dictionary<string, int> { ["B1"] = 9 }, await StoredQuantitiesAsync());
    }

    [Fact]
    public async Task ReceiveStock_rejects_loose_strips_that_make_a_full_pack()
    {
        var id = await CreateDrugAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 0, loose: 3));
        Assert.Equal("stock.loose_exceeds_pack", ex.Code);
    }

    [Fact]
    public async Task ReceiveStock_for_unknown_drug_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ReceiveAsync(Guid.NewGuid(), "B1", Today.AddMonths(6), packs: 1));
    }

    [Fact]
    public async Task DeductStock_persists_fefo_allocation_across_batches()
    {
        var id = await CreateDrugAsync();
        await ReceiveAsync(id, "LATE", Today.AddMonths(12), packs: 2);
        await ReceiveAsync(id, "EARLY", Today.AddMonths(2), packs: 1);

        await using var db = _database.CreateContext();
        var result = await new DeductStockCommandHandler(db, Calendar).Handle(new DeductStockCommand(id, 4), CancellationToken.None);

        Assert.Equal(["EARLY", "LATE"], result.Allocations.Select(a => a.BatchNumber));
        Assert.Equal(5, result.Drug.SellableQuantityInSubUnits);
        Assert.Equal(new Dictionary<string, int> { ["EARLY"] = 0, ["LATE"] = 5 }, await StoredQuantitiesAsync());
    }

    [Fact]
    public async Task DeductStock_with_insufficient_stock_saves_nothing()
    {
        var id = await CreateDrugAsync();
        await ReceiveAsync(id, "B1", Today.AddMonths(2), packs: 1);

        await using var db = _database.CreateContext();
        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            new DeductStockCommandHandler(db, Calendar).Handle(new DeductStockCommand(id, 4), CancellationToken.None));

        Assert.Equal(3, (await StoredQuantitiesAsync())["B1"]);
    }

    [Fact]
    public async Task DeductStock_retries_on_a_concurrent_write_and_never_double_spends()
    {
        var id = await CreateDrugAsync();
        await ReceiveAsync(id, "B1", Today.AddMonths(2), packs: 2);

        // Context A loads the batch, then another writer spends from it before A saves.
        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, async () =>
        {
            await using var b = _database.CreateContext();
            await new DeductStockCommandHandler(b, Calendar).Handle(new DeductStockCommand(id, 2), CancellationToken.None);
        });

        var result = await new DeductStockCommandHandler(interceptingDb, Calendar).Handle(new DeductStockCommand(id, 3), CancellationToken.None);

        Assert.Equal(1, result.Drug.SellableQuantityInSubUnits);
        Assert.Equal(1, (await StoredQuantitiesAsync())["B1"]);
    }

    [Fact]
    public async Task ReceiveStock_retries_when_another_writer_inserts_the_same_batch_first()
    {
        var id = await CreateDrugAsync();

        // Context A sees no batch yet; B inserts it before A saves, so A's insert hits the unique key and must merge.
        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, () => ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 1));

        await new ReceiveStockCommandHandler(interceptingDb, Clock, Calendar).Handle(
            new ReceiveStockCommand(id, "B1", Today.AddMonths(6), 2, 0), CancellationToken.None);

        Assert.Equal(new Dictionary<string, int> { ["B1"] = 9 }, await StoredQuantitiesAsync());
    }

    [Fact]
    public async Task ReceiveStock_retries_when_a_sale_spends_from_the_batch_first()
    {
        var id = await CreateDrugAsync();
        await ReceiveAsync(id, "B1", Today.AddMonths(6), packs: 2);

        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, async () =>
        {
            await using var b = _database.CreateContext();
            await new DeductStockCommandHandler(b, Calendar).Handle(new DeductStockCommand(id, 4), CancellationToken.None);
        });

        await new ReceiveStockCommandHandler(interceptingDb, Clock, Calendar).Handle(
            new ReceiveStockCommand(id, "B1", Today.AddMonths(6), 1, 0), CancellationToken.None);

        Assert.Equal(5, (await StoredQuantitiesAsync())["B1"]);
    }

    [Fact]
    public async Task CreateDrug_racing_on_the_same_barcode_raises_duplicate_key()
    {
        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, async () => { await CreateDrugAsync("6229999999999"); });

        await Assert.ThrowsAsync<DuplicateKeyException>(() => new CreateDrugCommandHandler(interceptingDb, Clock, Calendar).Handle(
            new CreateDrugCommand("دواء", null, "6229999999999", "علبة", "شريط", 1, 100), CancellationToken.None));
    }

    [Fact]
    public async Task ListDrugs_finds_by_partial_arabic_name_or_exact_barcode_and_reports_stock()
    {
        var id = await CreateDrugAsync("6221111111111");
        await CreateDrugAsync("6222222222222");
        await ReceiveAsync(id, "B1", Today.AddMonths(3), packs: 1, loose: 2);

        await using var db = _database.CreateContext();
        var handler = new ListDrugsQueryHandler(db, Calendar);

        Assert.Equal(2, (await handler.Handle(new ListDrugsQuery("اكسترا"), CancellationToken.None)).Count);

        var byBarcode = Assert.Single(await handler.Handle(new ListDrugsQuery("6221111111111"), CancellationToken.None));
        Assert.Equal(5, byBarcode.SellableQuantityInSubUnits);
        Assert.Equal(Today.AddMonths(3), byBarcode.NearestExpiry);
    }

    [Fact]
    public async Task ListDrugs_treats_like_wildcards_literally()
    {
        await CreateDrugAsync();

        await using var db = _database.CreateContext();
        Assert.Empty(await new ListDrugsQueryHandler(db, Calendar).Handle(new ListDrugsQuery("%"), CancellationToken.None));
    }

    public void Dispose() => _database.Dispose();
}
