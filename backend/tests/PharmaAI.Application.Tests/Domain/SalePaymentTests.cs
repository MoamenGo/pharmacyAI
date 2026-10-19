using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Domain;

public class SalePaymentTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly DateTime SoldAt = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Panadol: EGP 45 a box of 3 strips, EGP 15 a strip; 3 boxes in stock.</summary>
    private static Drug Panadol()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", Today.AddMonths(12), 9, InventoryTestData.ReceivedAt);
        return drug;
    }

    /// <summary>One box (45) and two strips (30): EGP 75 before any discount.</summary>
    private static Sale Sell(SalePayment payment)
    {
        var drug = Panadol();
        return Sale.Create(1, SoldAt, Today, [new(drug, SaleUnit.Pack, 1), new(drug, SaleUnit.SubUnit, 2)], payment);
    }

    private static string CodeOf(Action act) => Assert.Throws<DomainException>(act).Code;

    [Fact]
    public void A_discount_comes_off_the_total_and_is_shared_by_line_size()
    {
        var sale = Sell(new SalePayment(PaymentMethod.Cash, 100m, Discount: 10m));

        Assert.Equal((75m, 10m, 65m, 35m), (sale.Subtotal, sale.Discount, sale.Total, sale.Change));
        Assert.Equal([6m, 4m], sale.Lines.OrderBy(l => l.LineNumber).Select(l => l.Discount));
        Assert.Equal(65m, sale.Lines.Sum(l => l.NetTotal));
    }

    [Fact]
    public void Rounding_leftovers_of_a_shared_discount_go_to_one_line()
    {
        var drug = Panadol();

        var sale = Sale.Create(
            1, SoldAt, Today, [new(drug, SaleUnit.SubUnit, 1), new(drug, SaleUnit.SubUnit, 1), new(drug, SaleUnit.SubUnit, 1)],
            new SalePayment(PaymentMethod.Cash, 35m, Discount: 10m));

        Assert.Equal([3.34m, 3.33m, 3.33m], sale.Lines.OrderBy(l => l.LineNumber).Select(l => l.Discount));
        Assert.Equal(35m, sale.Total);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(75.01)]
    public void A_discount_must_be_between_zero_and_the_lines_total(decimal discount) =>
        Assert.Equal("sale.invalid_discount", CodeOf(() => Sell(new SalePayment(PaymentMethod.Cash, 100m, discount))));

    [Fact]
    public void A_card_sale_is_paid_in_full_with_no_change()
    {
        var sale = Sell(new SalePayment(PaymentMethod.Card, 0m, CardReference: " 4821 "));

        Assert.Equal((75m, 0m, 0m, "4821"), (sale.Paid, sale.Change, sale.Due, sale.CardReference));
    }

    [Fact]
    public void A_credit_sale_puts_the_unpaid_part_on_the_customers_balance()
    {
        var customer = Customer.Create("أحمد علي", "01001234567", SoldAt);

        var sale = Sell(new SalePayment(PaymentMethod.Credit, 25m, Customer: customer));

        Assert.Equal((25m, 50m, 0m), (sale.Paid, sale.Due, sale.Change));
        Assert.Equal((customer.Id, "أحمد علي"), (sale.CustomerId, sale.CustomerName));
        Assert.Equal(50m, customer.Balance);
    }

    [Fact]
    public void A_credit_sale_needs_a_customer_and_a_down_payment_within_the_total()
    {
        var customer = Customer.Create("أحمد علي", null, SoldAt);

        Assert.Equal("sale.customer_required", CodeOf(() => Sell(new SalePayment(PaymentMethod.Credit, 0m))));
        Assert.Equal("sale.invalid_down_payment", CodeOf(() => Sell(new SalePayment(PaymentMethod.Credit, 75.01m, Customer: customer))));
        Assert.Equal(0m, customer.Balance);
    }

    [Fact]
    public void A_cash_sale_ignores_any_customer()
    {
        var customer = Customer.Create("أحمد علي", null, SoldAt);

        var sale = Sell(new SalePayment(PaymentMethod.Cash, 75m, Customer: customer));

        Assert.Null(sale.CustomerId);
        Assert.Equal(0m, customer.Balance);
    }
}

public class CustomerTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_payment_lowers_the_balance_but_never_below_zero()
    {
        var customer = Customer.Create("  منى  ", " 0100 ", Now);
        customer.Charge(50m);

        var payment = customer.RecordPayment(20m, Now, new DateOnly(2026, 10, 19), " نص الحساب ");

        Assert.Equal(("منى", "0100", 30m), (customer.Name, customer.Phone, customer.Balance));
        Assert.Equal((20m, "نص الحساب"), (payment.Amount, payment.Note));
        Assert.Equal("customer.overpayment", Assert.Throws<DomainException>(() => customer.RecordPayment(30.01m, Now, default, null)).Code);
        Assert.Equal("customer.invalid_amount", Assert.Throws<DomainException>(() => customer.RecordPayment(0m, Now, default, null)).Code);
    }

    [Fact]
    public void A_customer_needs_a_name()
    {
        Assert.Equal("customer.invalid_name", Assert.Throws<DomainException>(() => Customer.Create(" ", null, Now)).Code);
        Assert.Null(Customer.Create("منى", " ", Now).Phone);
    }
}
