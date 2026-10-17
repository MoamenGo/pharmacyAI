using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Tests.Domain;

public class FefoTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [Fact]
    public void Takes_from_the_earliest_expiry_first_regardless_of_receive_order()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("LATE", Today.AddMonths(12), 10, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("EARLY", Today.AddMonths(2), 10, InventoryTestData.ReceivedAt.AddDays(1));

        var allocation = Assert.Single(drug.Deduct(4, Today));

        Assert.Equal("EARLY", allocation.BatchNumber);
        Assert.Equal(6, drug.Batches.Single(b => b.BatchNumber == "EARLY").QuantityInSubUnits);
        Assert.Equal(10, drug.Batches.Single(b => b.BatchNumber == "LATE").QuantityInSubUnits);
    }

    [Fact]
    public void Spans_batches_in_expiry_order_when_one_is_not_enough()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("A", Today.AddMonths(1), 2, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("B", Today.AddMonths(6), 5, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("C", Today.AddMonths(3), 1, InventoryTestData.ReceivedAt);

        var allocations = drug.Deduct(5, Today);

        Assert.Equal(["A", "C", "B"], allocations.Select(a => a.BatchNumber));
        Assert.Equal([2, 1, 2], allocations.Select(a => a.QuantityInSubUnits));
    }

    [Fact]
    public void Never_sells_from_an_expired_batch()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("EXPIRED", Today.AddDays(-1), 50, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("GOOD", Today.AddMonths(6), 3, InventoryTestData.ReceivedAt);

        var allocation = Assert.Single(drug.Deduct(3, Today));

        Assert.Equal("GOOD", allocation.BatchNumber);
        Assert.Equal(50, drug.Batches.Single(b => b.BatchNumber == "EXPIRED").QuantityInSubUnits);
    }

    [Fact]
    public void A_batch_expiring_today_is_still_sellable()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("TODAY", Today, 1, InventoryTestData.ReceivedAt);

        Assert.Equal("TODAY", Assert.Single(drug.Deduct(1, Today)).BatchNumber);
    }

    [Fact]
    public void Same_expiry_is_broken_by_receive_time()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("SECOND", Today.AddMonths(4), 5, InventoryTestData.ReceivedAt.AddHours(1));
        drug.ReceiveStock("FIRST", Today.AddMonths(4), 5, InventoryTestData.ReceivedAt);

        Assert.Equal("FIRST", Assert.Single(drug.Deduct(2, Today)).BatchNumber);
    }

    [Fact]
    public void Insufficient_stock_throws_and_changes_nothing()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("A", Today.AddMonths(1), 2, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("EXPIRED", Today.AddDays(-5), 100, InventoryTestData.ReceivedAt);

        var ex = Assert.Throws<InsufficientStockException>(() => drug.Deduct(3, Today));

        Assert.Equal(3, ex.RequestedInSubUnits);
        Assert.Equal(2, ex.AvailableInSubUnits);
        Assert.Equal("stock.insufficient", ex.Code);
        Assert.Equal(2, drug.Batches.Single(b => b.BatchNumber == "A").QuantityInSubUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_quantity(int quantity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Fefo.Allocate([], quantity, Today));
}
