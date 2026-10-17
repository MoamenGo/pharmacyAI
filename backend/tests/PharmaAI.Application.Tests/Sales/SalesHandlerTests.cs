using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Application.Sales;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Sales.Queries.GetSale;
using PharmaAI.Application.Sales.Queries.ListSales;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Sales;

public sealed class SalesHandlerTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);
    private static readonly DateOnly Today = new(2026, 10, 17);

    private readonly SqliteTestDatabase _database = new();

    /// <summary>Panadol (3 strips a box, EGP 45) with <paramref name="boxes"/> boxes in one batch.</summary>
    private async Task<Guid> SeedDrugAsync(string barcode = "6221234567890", int boxes = 2)
    {
        await using var db = _database.CreateContext();
        var drug = InventoryTestData.Panadol(barcode);
        drug.ReceiveStock("B1", Today.AddMonths(6), boxes * 3, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug.Id;
    }

    private async Task<int> StockAsync(Guid drugId)
    {
        await using var db = _database.CreateContext();
        return await db.StockBatches.Where(b => b.DrugId == drugId).SumAsync(b => b.QuantityInSubUnits);
    }

    private Task<SaleDto> SellAsync(params CreateSaleItem[] items) => SellWithAsync(_database.CreateContext(), 100_000, items);

    private static async Task<SaleDto> SellWithAsync(IPharmaDbContext db, long paidPiasters, params CreateSaleItem[] items) =>
        await new CreateSaleCommandHandler(db, Clock, Calendar).Handle(new CreateSaleCommand(items, paidPiasters), CancellationToken.None);

    [Fact]
    public async Task CreateSale_saves_the_receipt_and_takes_the_stock()
    {
        var id = await SeedDrugAsync();

        var sale = await SellAsync(new(id, SaleUnit.Pack, 1), new(id, SaleUnit.SubUnit, 1));

        Assert.Equal((1L, 6000L, 94_000L, Today), (sale.Number, sale.TotalPiasters, sale.ChangePiasters, sale.BusinessDate));
        Assert.Equal(2, await StockAsync(id));

        await using var db = _database.CreateContext();
        var stored = await new GetSaleQueryHandler(db).Handle(new GetSaleQuery(sale.Id), CancellationToken.None);
        Assert.Equal(["بانادول اكسترا", "بانادول اكسترا"], stored!.Lines.Select(l => l.DrugNameAr));
        Assert.Equal("B1", Assert.Single(stored.Lines[0].Batches).BatchNumber);
    }

    [Fact]
    public async Task CreateSale_numbers_receipts_in_sequence()
    {
        var id = await SeedDrugAsync();

        var first = await SellAsync(new CreateSaleItem(id, SaleUnit.SubUnit, 1));
        var second = await SellAsync(new CreateSaleItem(id, SaleUnit.SubUnit, 1));

        Assert.Equal([1L, 2L], new[] { first.Number, second.Number });
    }

    [Fact]
    public async Task CreateSale_short_on_one_line_saves_nothing()
    {
        var plenty = await SeedDrugAsync();
        var scarce = await SeedDrugAsync("6220000000001", boxes: 1);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            SellAsync(new(plenty, SaleUnit.Pack, 1), new(scarce, SaleUnit.Pack, 2)));

        Assert.Equal(scarce, ex.DrugId);
        Assert.Equal((6, 3), (await StockAsync(plenty), await StockAsync(scarce)));
        await using var db = _database.CreateContext();
        Assert.Empty(db.Sales);
    }

    [Fact]
    public async Task CreateSale_with_too_little_cash_saves_nothing()
    {
        var id = await SeedDrugAsync();

        await using var db = _database.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainException>(() => SellWithAsync(db, 4499, new CreateSaleItem(id, SaleUnit.Pack, 1)));

        Assert.Equal("sale.insufficient_payment", ex.Code);
        Assert.Equal(6, await StockAsync(id));
    }

    [Fact]
    public async Task CreateSale_for_an_unknown_drug_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SellAsync(new CreateSaleItem(Guid.NewGuid(), SaleUnit.Pack, 1)));
    }

    [Fact]
    public async Task CreateSale_retries_when_another_till_sells_first_and_never_double_spends()
    {
        var id = await SeedDrugAsync();

        // Till A reads stock and the next number; till B completes a sale before A saves.
        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, async () => { await SellAsync(new CreateSaleItem(id, SaleUnit.Pack, 1)); });

        var sale = await SellWithAsync(interceptingDb, 100_000, new CreateSaleItem(id, SaleUnit.SubUnit, 2));

        Assert.Equal(2L, sale.Number);
        Assert.Equal(1, await StockAsync(id));
    }

    [Fact]
    public async Task ListSales_returns_the_day_newest_first_with_its_total()
    {
        var id = await SeedDrugAsync();
        await SellAsync(new CreateSaleItem(id, SaleUnit.Pack, 1));
        await SellAsync(new CreateSaleItem(id, SaleUnit.SubUnit, 1));

        await using var db = _database.CreateContext();
        var handler = new ListSalesQueryHandler(db, Calendar);
        var day = await handler.Handle(new ListSalesQuery(), CancellationToken.None);

        Assert.Equal((Today, 2, 6000L), (day.Date, day.Count, day.TotalPiasters));
        Assert.Equal([2L, 1L], day.Sales.Select(s => s.Number));
        Assert.Equal(0, (await handler.Handle(new ListSalesQuery(Today.AddDays(-1)), CancellationToken.None)).Count);
    }

    public void Dispose() => _database.Dispose();
}
