using PharmaAI.Domain.Common;
using PharmaAI.Domain.Customers;

namespace PharmaAI.Domain.Sales;

/// <summary>
/// A completed sale. It is created in one step: every line takes its stock FEFO,
/// or the whole sale fails and no stock moves. A discount applies to the whole receipt and is
/// shared across the lines in proportion to their totals, so each line knows its net amount.
/// </summary>
public sealed class Sale
{
    public const int MaxLines = 200;
    public const int CardReferenceMaxLength = 50;

    private readonly List<SaleLine> _lines = [];

    private Sale()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Sequential receipt number shown to the customer.</summary>
    public long Number { get; private set; }

    public DateTime SoldAtUtc { get; private set; }

    /// <summary>The pharmacy's local (Cairo) day of the sale, used for daily totals.</summary>
    public DateOnly BusinessDate { get; private set; }

    /// <summary>What the customer pays: the lines' total less <see cref="Discount"/>.</summary>
    public decimal Total { get; private set; }

    public decimal Discount { get; private set; }

    public decimal Subtotal => Total + Discount;

    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Cash handed over, the card amount, or the part of a credit sale paid now.</summary>
    public decimal Paid { get; private set; }

    public decimal Change => PaymentMethod == PaymentMethod.Cash ? Paid - Total : 0m;

    /// <summary>The part of a credit sale added to the customer's balance.</summary>
    public decimal Due => PaymentMethod == PaymentMethod.Credit ? Total - Paid : 0m;

    public Guid? CustomerId { get; private set; }

    /// <summary>The customer's name at the time of sale, for the receipt.</summary>
    public string? CustomerName { get; private set; }

    /// <summary>Optional card terminal reference (approval code or last digits).</summary>
    public string? CardReference { get; private set; }

    public IReadOnlyCollection<SaleLine> Lines => _lines;

    public static Sale Create(long number, DateTime soldAtUtc, DateOnly businessDate, IReadOnlyList<SaleItem> items, SalePayment payment)
    {
        if (items.Count is 0 or > MaxLines)
        {
            throw new DomainException("sale.invalid_lines", $"A sale needs between 1 and {MaxLines} lines.");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            Number = number,
            SoldAtUtc = soldAtUtc,
            BusinessDate = businessDate,
            PaymentMethod = payment.Method,
        };

        for (var i = 0; i < items.Count; i++)
        {
            sale._lines.Add(SaleLine.Create(sale.Id, i + 1, items[i], businessDate));
        }

        var subtotal = sale._lines.Sum(l => l.LineTotal);
        var discount = Math.Round(payment.Discount, 2, MidpointRounding.AwayFromZero);
        if (discount < 0 || discount > subtotal)
        {
            throw new DomainException("sale.invalid_discount", $"The discount must be between 0 and the total {subtotal}.");
        }

        sale.Discount = discount;
        sale.Total = subtotal - discount;
        sale.ShareDiscount(subtotal);
        sale.Settle(payment);
        return sale;
    }

    private void ShareDiscount(decimal subtotal)
    {
        if (Discount == 0)
        {
            return;
        }

        // Rounding leaves a few piasters over; they go to the biggest line, which can always absorb them.
        var largest = _lines.MaxBy(l => l.LineTotal)!;
        foreach (var line in _lines.Where(l => l != largest))
        {
            line.ApplyDiscount(Math.Round(Discount * line.LineTotal / subtotal, 2, MidpointRounding.AwayFromZero));
        }

        largest.ApplyDiscount(Discount - _lines.Where(l => l != largest).Sum(l => l.Discount));
    }

    private void Settle(SalePayment payment)
    {
        switch (payment.Method)
        {
            case PaymentMethod.Cash:
                if (payment.Paid < Total)
                {
                    throw new DomainException("sale.insufficient_payment", $"Paid {payment.Paid} is less than the total {Total}.");
                }

                Paid = payment.Paid;
                break;

            case PaymentMethod.Card:
                var reference = string.IsNullOrWhiteSpace(payment.CardReference) ? null : payment.CardReference.Trim();
                if (reference is { Length: > CardReferenceMaxLength })
                {
                    throw new DomainException("sale.invalid_card_reference", $"A card reference has at most {CardReferenceMaxLength} characters.");
                }

                Paid = Total;
                CardReference = reference;
                break;

            case PaymentMethod.Credit:
                var customer = payment.Customer
                    ?? throw new DomainException("sale.customer_required", "A credit sale needs a customer.");
                if (payment.Paid < 0 || payment.Paid > Total)
                {
                    throw new DomainException("sale.invalid_down_payment", $"The amount paid now must be between 0 and the total {Total}.");
                }

                Paid = payment.Paid;
                CustomerId = customer.Id;
                CustomerName = customer.Name;
                customer.Charge(Due);
                break;

            default:
                throw new DomainException("sale.invalid_payment_method", $"Unknown payment method {payment.Method}.");
        }
    }
}
