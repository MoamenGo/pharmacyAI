using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Tests.Domain;

public class DrugTests
{
    private static readonly DateOnly Expiry = new(2027, 6, 30);

    [Fact]
    public void Receiving_the_same_batch_twice_merges_it()
    {
        var drug = InventoryTestData.Panadol();

        drug.ReceiveStock("B1", Expiry, 6, InventoryTestData.ReceivedAt);
        drug.ReceiveStock(" B1 ", Expiry, 3, InventoryTestData.ReceivedAt);

        Assert.Equal(9, Assert.Single(drug.Batches).QuantityInSubUnits);
    }

    [Fact]
    public void Same_batch_number_with_another_expiry_is_a_separate_batch()
    {
        var drug = InventoryTestData.Panadol();

        drug.ReceiveStock("B1", Expiry, 6, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("B1", Expiry.AddMonths(1), 3, InventoryTestData.ReceivedAt);

        Assert.Equal(2, drug.Batches.Count);
    }

    [Theory]
    [InlineData(45.00, 3, 15.00)]
    [InlineData(10.00, 3, 3.33)]
    [InlineData(20.00, 3, 6.67)]
    [InlineData(0.05, 2, 0.03)]
    public void Sub_unit_price_is_rounded_to_the_piaster(decimal packPrice, int subUnits, decimal expected)
    {
        var drug = Drug.Create("دواء", null, "1", "علبة", "شريط", subUnits, packPrice, InventoryTestData.ReceivedAt);

        Assert.Equal(expected, drug.SubUnitPrice);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1001, 10)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    public void Create_rejects_invalid_pack_size_or_price(int subUnits, decimal price) =>
        Assert.Throws<DomainException>(() =>
            Drug.Create("دواء", null, "1", "علبة", "شريط", subUnits, price, InventoryTestData.ReceivedAt));

    [Fact]
    public void Receiving_zero_is_rejected() =>
        Assert.Throws<DomainException>(() =>
            InventoryTestData.Panadol().ReceiveStock("B1", Expiry, 0, InventoryTestData.ReceivedAt));

    [Fact]
    public void Receiving_past_the_int_range_is_rejected_without_changing_the_batch()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", Expiry, int.MaxValue - 1, InventoryTestData.ReceivedAt);

        var ex = Assert.Throws<DomainException>(() => drug.ReceiveStock("B1", Expiry, 2, InventoryTestData.ReceivedAt));

        Assert.Equal("stock.quantity_too_large", ex.Code);
        Assert.Equal(int.MaxValue - 1, Assert.Single(drug.Batches).QuantityInSubUnits);
    }
}
