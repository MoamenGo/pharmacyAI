using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Domain;

public class SaleReturnTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly DateTime SoldAt = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Panadol, EGP 45 a box of 3 strips: 2 strips in a batch expiring soon, 9 in a later one.</summary>
    private static Drug Panadol()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("EARLY", Today.AddMonths(2), 2, InventoryTestData.ReceivedAt);
        drug.ReceiveStock("LATE", Today.AddMonths(12), 9, InventoryTestData.ReceivedAt);
        return drug;
    }

    private static int Stock(Drug drug, string batch) => drug.Batches.Single(b => b.BatchNumber == batch).QuantityInSubUnits;

    private static SaleReturn Return(Sale sale, Drug drug, int quantity, Customer? customer = null, long number = 1) =>
        SaleReturn.Create(number, SoldAt.AddHours(1), Today, sale, [(sale.Lines.Single().Id, quantity)],
            new Dictionary<Guid, Drug> { [drug.Id] = drug }, customer, "الصنف غلط");

    private static string CodeOf(Action act) => Assert.Throws<DomainException>(act).Code;

    [Fact]
    public void Returned_strips_go_back_to_the_batches_they_came_from_latest_expiry_first()
    {
        var drug = Panadol();
        // 5 strips: 2 from EARLY (FEFO), 3 from LATE.
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.SubUnit, 5)], SalePayment.Cash(100m));
        Assert.Equal((0, 6), (Stock(drug, "EARLY"), Stock(drug, "LATE")));

        Return(sale, drug, 4);
        Assert.Equal((1, 9), (Stock(drug, "EARLY"), Stock(drug, "LATE")));

        Return(sale, drug, 1, number: 2);
        Assert.Equal((2, 9), (Stock(drug, "EARLY"), Stock(drug, "LATE")));
    }

    [Fact]
    public void Partial_refunds_are_shares_of_the_net_amount_and_the_last_one_takes_the_remainder()
    {
        var drug = Panadol();
        // 3 strips at 15 = 45, less 10 discount = 35: a third is 11.67, so the last strip refunds 11.66.
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.SubUnit, 3)], new SalePayment(PaymentMethod.Cash, 35m, Discount: 10m));

        var first = Return(sale, drug, 1);
        var second = Return(sale, drug, 1, number: 2);
        var last = Return(sale, drug, 1, number: 3);

        Assert.Equal([11.67m, 11.67m, 11.66m], [first.Total, second.Total, last.Total]);
        Assert.Equal(35m, sale.Lines.Single().ReturnedAmount);
        Assert.Equal(first.Total, first.Refunded);
    }

    [Fact]
    public void Nothing_more_than_was_sold_can_come_back()
    {
        var drug = Panadol();
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.Pack, 1)], SalePayment.Cash(45m));
        Return(sale, drug, 1);

        Assert.Equal("return.invalid_quantity", CodeOf(() => Return(sale, drug, 1, number: 2)));
        Assert.Equal(0, sale.Lines.Single().ReturnableQuantity);
    }

    [Fact]
    public void A_credit_sale_refund_comes_off_the_balance_and_only_the_rest_is_paid_back()
    {
        var drug = Panadol();
        var customer = Customer.Create("أحمد علي", null, SoldAt);
        // 2 boxes = 90, 20 paid now: 70 owed. The customer then pays 50, leaving 20.
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.Pack, 2)], new SalePayment(PaymentMethod.Credit, 20m, Customer: customer));
        customer.RecordPayment(50m, SoldAt, Today, null);

        var back = Return(sale, drug, 1, customer);

        Assert.Equal((45m, 20m, 25m), (back.Total, back.CreditedToAccount, back.Refunded));
        Assert.Equal(0m, customer.Balance);
    }

    [Fact]
    public void A_credit_sale_needs_its_customer()
    {
        var drug = Panadol();
        var customer = Customer.Create("أحمد علي", null, SoldAt);
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.Pack, 1)], new SalePayment(PaymentMethod.Credit, 0m, Customer: customer));

        Assert.Equal("return.customer_required", CodeOf(() => Return(sale, drug, 1)));
        Assert.Equal("return.customer_required", CodeOf(() => Return(sale, drug, 1, Customer.Create("غيره", null, SoldAt))));
    }

    [Fact]
    public void Lines_must_be_on_the_sale_and_listed_once()
    {
        var drug = Panadol();
        var sale = Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.SubUnit, 2)], SalePayment.Cash(30m));
        var drugs = new Dictionary<Guid, Drug> { [drug.Id] = drug };
        var lineId = sale.Lines.Single().Id;

        Assert.Equal("return.unknown_line", CodeOf(() => SaleReturn.Create(1, SoldAt, Today, sale, [(Guid.NewGuid(), 1)], drugs, null, null)));
        Assert.Equal("return.duplicate_line", CodeOf(() => SaleReturn.Create(1, SoldAt, Today, sale, [(lineId, 1), (lineId, 1)], drugs, null, null)));
        Assert.Equal("return.no_lines", CodeOf(() => SaleReturn.Create(1, SoldAt, Today, sale, [], drugs, null, null)));
    }
}
