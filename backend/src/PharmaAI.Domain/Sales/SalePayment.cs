using PharmaAI.Domain.Customers;

namespace PharmaAI.Domain.Sales;

/// <summary>How a sale is settled: the method, what was handed over, and any discount on the whole receipt.</summary>
/// <param name="Paid">Cash handed over; for a credit sale, the part paid now. Ignored for a card.</param>
/// <param name="Customer">Required for a credit sale; the unpaid part goes on their balance.</param>
public sealed record SalePayment(
    PaymentMethod Method,
    decimal Paid,
    decimal Discount = 0m,
    Customer? Customer = null,
    string? CardReference = null)
{
    public static SalePayment Cash(decimal paid) => new(PaymentMethod.Cash, paid);
}
