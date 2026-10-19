using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Application.Inventory.Commands.UpdateDrug;
using PharmaAI.Application.Inventory.Commands.WriteOffStock;
using PharmaAI.Application.Inventory.Queries.GetAlternatives;
using PharmaAI.Application.Inventory.Queries.GetInventoryAlerts;
using PharmaAI.Application.Inventory.Queries.ListDrugs;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;
using PharmaAI.Infrastructure.Catalog;

namespace PharmaAI.Application.Tests.Inventory;

/// <summary>Editing drugs, writing off stock, alerts and alternatives.</summary>
public sealed class InventoryMaintenanceTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);
    private static readonly DateOnly Today = new(2026, 10, 18);
    private const string Paracetamol = "CAFFEINE+PARACETAMOL(ACETAMINOPHEN)";

    private readonly SqliteTestDatabase _database = new();

    private async Task<Drug> SeedAsync(Drug drug, params (string Batch, DateOnly Expiry, int SubUnits)[] batches)
    {
        await using var db = _database.CreateContext();
        foreach (var (batch, expiry, subUnits) in batches)
        {
            drug.ReceiveStock(batch, expiry, subUnits, InventoryTestData.ReceivedAt);
        }

        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug;
    }

    private static Drug Make(string barcode, string nameAr, string? ingredient = null, int reorder = 0, decimal price = 45m) =>
        Drug.Create(nameAr, null, barcode, "علبة", "شريط", 3, price, InventoryTestData.ReceivedAt, ingredient, null, reorder);

    [Fact]
    public async Task Updating_a_drug_changes_its_details_but_never_its_pack_size()
    {
        var drug = await SeedAsync(InventoryTestData.Panadol(), ("B1", Today.AddMonths(6), 6));

        await using var db = _database.CreateContext();
        var updated = await new UpdateDrugCommandHandler(db, Calendar).Handle(
            new UpdateDrugCommand(drug.Id, " بانادول اكسترا 24 ", "Panadol Extra 24", "6220000000001", 5800, Paracetamol, "GSK", 2),
            CancellationToken.None);

        Assert.Equal(("بانادول اكسترا 24", 5800L, 3, Paracetamol, "GSK", 2), (updated.Drug.NameAr, updated.Drug.PublicPricePiasters,
            updated.Drug.SubUnitsPerPack, updated.Drug.ActiveIngredient, updated.Drug.Manufacturer, updated.Drug.ReorderLevelInPacks));
        Assert.Equal(6, updated.Drug.SellableQuantityInSubUnits);
        Assert.False(updated.Drug.IsShort);
        Assert.Equal("6220000000001", (await db.Drugs.AsNoTracking().SingleAsync()).Barcode);
    }

    [Fact]
    public async Task A_barcode_may_be_kept_but_not_taken_from_another_drug()
    {
        var first = await SeedAsync(InventoryTestData.Panadol("111"));
        await SeedAsync(InventoryTestData.Panadol("222"));
        await using var db = _database.CreateContext();
        var validator = new UpdateDrugCommandValidator(db);

        var keep = await validator.TestValidateAsync(new UpdateDrugCommand(first.Id, "بانادول", null, "111", 4500, null, null, 0));
        var steal = await validator.TestValidateAsync(new UpdateDrugCommand(first.Id, "بانادول", null, "222", 4500, null, null, 0));

        keep.ShouldNotHaveAnyValidationErrors();
        steal.ShouldHaveValidationErrorFor(c => c.Barcode).WithErrorCode("drug.barcode_taken");
    }

    [Fact]
    public async Task Expired_stock_can_be_written_off_and_the_reason_is_kept()
    {
        var drug = await SeedAsync(InventoryTestData.Panadol(), ("OLD", Today.AddDays(-1), 4), ("NEW", Today.AddMonths(6), 3));
        var expired = drug.Batches.Single(b => b.BatchNumber == "OLD");

        await using var db = _database.CreateContext();
        var result = await new WriteOffStockCommandHandler(db, Clock, Calendar).Handle(
            new WriteOffStockCommand(drug.Id, expired.Id, 4, WriteOffReason.Expired, "  إعدام شهري "), CancellationToken.None);

        Assert.Equal((3, 0), (result.Drug.SellableQuantityInSubUnits, result.Drug.ExpiredQuantityInSubUnits));
        Assert.DoesNotContain(result.Batches, b => b.BatchNumber == "OLD");
        var record = await db.StockAdjustments.AsNoTracking().SingleAsync();
        Assert.Equal((drug.Id, expired.Id, "OLD", 4, WriteOffReason.Expired, "إعدام شهري"),
            (record.DrugId, record.StockBatchId, record.BatchNumber, record.QuantityInSubUnits, record.Reason, record.Note));
    }

    [Fact]
    public async Task Writing_off_more_than_the_batch_holds_changes_nothing()
    {
        var drug = await SeedAsync(InventoryTestData.Panadol(), ("B1", Today.AddMonths(6), 2));

        await using var db = _database.CreateContext();
        var error = await Assert.ThrowsAsync<DomainException>(() => new WriteOffStockCommandHandler(db, Clock, Calendar).Handle(
            new WriteOffStockCommand(drug.Id, drug.Batches.Single().Id, 3, WriteOffReason.Damaged, null), CancellationToken.None));

        Assert.Equal("stock.batch_overdrawn", error.Code);
        await using var check = _database.CreateContext();
        Assert.Empty(check.StockAdjustments);
        Assert.Equal(2, (await check.StockBatches.SingleAsync()).QuantityInSubUnits);
    }

    [Fact]
    public async Task A_batch_of_another_drug_cannot_be_written_off()
    {
        var drug = await SeedAsync(InventoryTestData.Panadol("111"), ("B1", Today.AddMonths(6), 2));
        var other = await SeedAsync(InventoryTestData.Panadol("222"), ("B2", Today.AddMonths(6), 2));

        await using var db = _database.CreateContext();
        var error = await Assert.ThrowsAsync<DomainException>(() => new WriteOffStockCommandHandler(db, Clock, Calendar).Handle(
            new WriteOffStockCommand(drug.Id, other.Batches.Single().Id, 1, WriteOffReason.Lost, null), CancellationToken.None));

        Assert.Equal("stock.batch_not_found", error.Code);
    }

    [Fact]
    public async Task Alerts_list_shortages_stock_about_to_expire_and_expired_stock()
    {
        // Reorder at 2 boxes (6 strips): 5 sellable strips is short, the expired 3 do not count.
        await SeedAsync(Make("1", "كونكور", reorder: 2), ("SOON", Today.AddDays(30), 5), ("GONE", Today.AddDays(-3), 3));
        await SeedAsync(Make("2", "بروفين", reorder: 1), ("LATER", Today.AddYears(1), 3));
        await SeedAsync(Make("3", "بدون حد"), ("LATER", Today.AddYears(1), 1));

        await using var db = _database.CreateContext();
        var alerts = await new GetInventoryAlertsQueryHandler(db, Calendar).Handle(new GetInventoryAlertsQuery(), CancellationToken.None);

        Assert.Equal(Today, alerts.Today);
        Assert.Equal(["كونكور"], alerts.Shortages.Select(s => s.NameAr));
        Assert.Equal(["SOON"], alerts.ExpiringSoon.Select(b => b.BatchNumber));
        var expired = Assert.Single(alerts.Expired);
        Assert.Equal(("GONE", "كونكور", 3), (expired.BatchNumber, expired.DrugNameAr, expired.QuantityInSubUnits));
    }

    [Fact]
    public async Task Alternatives_are_drugs_with_the_same_active_ingredient_in_stock_first_then_the_market()
    {
        await using (var db = _database.CreateContext())
        {
            await CatalogImporter.ImportIfOutdatedAsync(db);
        }

        var panadol = await SeedAsync(Make("1", "بانادول", Paracetamol));
        await SeedAsync(Make("2", "سولبادين فاضي", Paracetamol.ToLowerInvariant(), price: 30m));
        await SeedAsync(Make("3", "بديل متاح", Paracetamol, price: 50m), ("B1", Today.AddMonths(6), 3));
        await SeedAsync(Make("4", "بروفين", "IBUPROFEN"), ("B1", Today.AddMonths(6), 3));

        await using var query = _database.CreateContext();
        var alternatives = await new GetAlternativesQueryHandler(query, Calendar).Handle(new GetAlternativesQuery(panadol.Id), CancellationToken.None);

        Assert.Equal(Paracetamol, alternatives.ActiveIngredient);
        Assert.Equal(["بديل متاح", "سولبادين فاضي"], alternatives.InStock.Select(d => d.NameAr));
        Assert.NotEmpty(alternatives.InMarket);
        Assert.All(alternatives.InMarket, c => Assert.Equal(Paracetamol, c.ActiveIngredient));
        Assert.Equal(alternatives.InMarket.Select(c => c.PricePiasters ?? long.MaxValue).Order(), alternatives.InMarket.Select(c => c.PricePiasters ?? long.MaxValue));
    }

    [Fact]
    public async Task A_drug_without_an_active_ingredient_has_no_alternatives()
    {
        var drug = await SeedAsync(InventoryTestData.Panadol());

        await using var db = _database.CreateContext();
        var alternatives = await new GetAlternativesQueryHandler(db, Calendar).Handle(new GetAlternativesQuery(drug.Id), CancellationToken.None);

        Assert.Null(alternatives.ActiveIngredient);
        Assert.Empty(alternatives.InStock);
        Assert.Empty(alternatives.InMarket);
    }

    [Fact]
    public async Task Searching_stock_by_active_ingredient_finds_every_brand()
    {
        await SeedAsync(Make("1", "بانادول", Paracetamol));
        await SeedAsync(Make("2", "بروفين", "IBUPROFEN"));

        await using var db = _database.CreateContext();
        var found = await new ListDrugsQueryHandler(db, Calendar).Handle(new ListDrugsQuery("paracetamol"), CancellationToken.None);

        Assert.Equal(["بانادول"], found.Select(d => d.NameAr));
    }

    [Theory]
    [InlineData(0, 0, "drug.invalid_price")]
    [InlineData(45, -1, "drug.invalid_reorder_level")]
    public void A_drug_needs_a_positive_price_and_a_non_negative_reorder_level(int price, int reorder, string code)
    {
        var drug = InventoryTestData.Panadol();

        var error = Assert.Throws<DomainException>(() => drug.Update("بانادول", null, "1", price, null, null, reorder));

        Assert.Equal(code, error.Code);
    }

    public void Dispose() => _database.Dispose();
}
