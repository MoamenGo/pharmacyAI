using FluentValidation.TestHelper;
using PharmaAI.Application.Common;
using PharmaAI.Application.Inventory.Commands.WriteOffStock;
using PharmaAI.Application.Reports.Queries.GetSalesReport;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Reports;

public sealed class SalesReportTests : IDisposable
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    private readonly SqliteTestDatabase _database = new();

    private async Task<Drug> SeedAsync(string barcode, string nameAr)
    {
        await using var db = _database.CreateContext();
        var drug = Drug.Create(nameAr, null, barcode, "علبة", "شريط", 3, 45m, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), 60, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug;
    }

    private async Task SellAsync(Guid drugId, SaleUnit unit, int quantity)
    {
        await using var db = _database.CreateContext();
        await new CreateSaleCommandHandler(db, _clock, new PharmacyCalendar(_clock)).Handle(
            new CreateSaleCommand([new CreateSaleItem(drugId, unit, quantity)], 1_000_000), CancellationToken.None);
    }

    private async Task<SalesReportDto> ReportAsync(DateOnly from, DateOnly to)
    {
        await using var db = _database.CreateContext();
        return await new GetSalesReportQueryHandler(db).Handle(new GetSalesReportQuery(from, to), CancellationToken.None);
    }

    [Fact]
    public async Task The_report_adds_up_days_best_sellers_and_write_offs_in_the_range()
    {
        var panadol = await SeedAsync("1", "بانادول");
        var brufen = await SeedAsync("2", "بروفين");

        await SellAsync(panadol.Id, SaleUnit.Pack, 1);                  // 16 Oct: 45.00
        _clock.Now = _clock.Now.AddDays(1);
        await SellAsync(panadol.Id, SaleUnit.SubUnit, 2);               // 17 Oct: 30.00
        await SellAsync(brufen.Id, SaleUnit.Pack, 2);                   // 17 Oct: 90.00
        await using (var db = _database.CreateContext())
        {
            await new WriteOffStockCommandHandler(db, _clock, new PharmacyCalendar(_clock)).Handle(
                new WriteOffStockCommand(brufen.Id, brufen.Batches.Single().Id, 1, WriteOffReason.Damaged, "اتكسرت"), CancellationToken.None);
        }

        _clock.Now = _clock.Now.AddDays(1);
        await SellAsync(brufen.Id, SaleUnit.Pack, 1);                   // 18 Oct: outside the range

        var report = await ReportAsync(new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 17));

        Assert.Equal((3, 16_500L), (report.Count, report.TotalPiasters));
        Assert.Equal([(new DateOnly(2026, 10, 16), 1, 4_500L), (new DateOnly(2026, 10, 17), 2, 12_000L)],
            report.Days.Select(d => (d.Date, d.Count, d.TotalPiasters)));
        Assert.Equal([("بروفين", 6, 9_000L), ("بانادول", 5, 7_500L)],
            report.TopDrugs.Select(d => (d.DrugNameAr, d.QuantityInSubUnits, d.TotalPiasters)));
        var writeOff = Assert.Single(report.WriteOffs);
        Assert.Equal(("بروفين", 1, WriteOffReason.Damaged, "اتكسرت"), (writeOff.DrugNameAr, writeOff.QuantityInSubUnits, writeOff.Reason, writeOff.Note));
    }

    [Fact]
    public async Task An_empty_range_is_all_zeros()
    {
        var report = await ReportAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal((0, 0L), (report.Count, report.TotalPiasters));
        Assert.Empty(report.Days);
        Assert.Empty(report.TopDrugs);
        Assert.Empty(report.WriteOffs);
    }

    [Fact]
    public void The_range_must_be_in_order_and_at_most_a_year()
    {
        var validator = new GetSalesReportQueryValidator();

        validator.TestValidate(new GetSalesReportQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetSalesReportQuery(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1))).ShouldHaveValidationErrorFor(q => q.To);
        Assert.False(validator.Validate(new GetSalesReportQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 6, 1))).IsValid);
    }

    public void Dispose() => _database.Dispose();
}
