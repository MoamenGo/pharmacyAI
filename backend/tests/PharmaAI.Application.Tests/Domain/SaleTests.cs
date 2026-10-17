using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Domain;

public class SaleTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);
    private static readonly DateTime SoldAt = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Panadol (3 strips a box, EGP 45) with 1 box expiring soon and 2 boxes later.</summary>
    private static Drug StockedPanadol()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("LATE", Today.AddMonths(12), 6, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("EARLY", Today.AddMonths(2), 3, InventoryTestData.ReceivedAt);
        return drug;
    }

    [Fact]
    public void Prices_packs_and_strips_and_computes_change()
    {
        var drug = StockedPanadol();

        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.Pack, 1), new(drug, SaleUnit.SubUnit, 2)], paid: 100m);

        var (pack, strips) = (sale.Lines.First(), sale.Lines.Last());
        Assert.Equal((45.00m, 45.00m, 3, "علبة"), (pack.UnitPrice, pack.LineTotal, pack.QuantityInSubUnits, pack.UnitName));
        Assert.Equal((15.00m, 30.00m, 2, "شريط"), (strips.UnitPrice, strips.LineTotal, strips.QuantityInSubUnits, strips.UnitName));
        Assert.Equal(75.00m, sale.Total);
        Assert.Equal(25.00m, sale.Change);
    }

    [Fact]
    public void Lines_take_stock_fefo_and_remember_their_batches()
    {
        var drug = StockedPanadol();

        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.SubUnit, 2), new(drug, SaleUnit.Pack, 1)], paid: 75m);

        Assert.Equal([("EARLY", 2)], sale.Lines.First().Batches.Select(b => (b.BatchNumber, b.QuantityInSubUnits)));
        Assert.Equal([("EARLY", 1), ("LATE", 2)], sale.Lines.Last().Batches.Select(b => (b.BatchNumber, b.QuantityInSubUnits)));
        Assert.Equal(4, drug.Batches.Sum(b => b.QuantityInSubUnits));
    }

    [Fact]
    public void Running_short_names_the_drug()
    {
        var plenty = StockedPanadol();
        var scarce = InventoryTestData.Panadol("6220000000001");
        scarce.ReceiveStock("B1", Today.AddMonths(6), 1, InventoryTestData.ReceivedAt);

        var ex = Assert.Throws<InsufficientStockException>(() =>
            Sale.Create(1, SoldAt, Today, [new(plenty, SaleUnit.Pack, 1), new(scarce, SaleUnit.SubUnit, 2)], paid: 100m));

        Assert.Equal((scarce.Id, 2, 1), (ex.DrugId, ex.RequestedInSubUnits, ex.AvailableInSubUnits));
    }

    [Fact]
    public void Paying_less_than_the_total_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Sale.Create(1, SoldAt, Today, [new(StockedPanadol(), SaleUnit.Pack, 1)], paid: 44.99m));

        Assert.Equal("sale.insufficient_payment", ex.Code);
    }

    [Fact]
    public void An_empty_sale_is_rejected() =>
        Assert.Equal("sale.invalid_lines", Assert.Throws<DomainException>(() => Sale.Create(1, SoldAt, Today, [], 0m)).Code);
}
