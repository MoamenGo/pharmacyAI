using PharmaAI.Domain.Common;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Domain.Sales;

/// <summary>
/// Items a customer brings back from one sale. The stock goes back into the batches it was sold from and the money is
/// refunded the way the sale was paid; on a credit sale the refund first comes off the customer's balance.
/// </summary>
public sealed class SaleReturn
{
    public const int ReasonMaxLength = 200;

    private readonly List<SaleReturnLine> _lines = [];

    private SaleReturn()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Sequential return number, separate from receipt numbers.</summary>
    public long Number { get; private set; }

    public Guid SaleId { get; private set; }

    public long SaleNumber { get; private set; }

    public DateTime ReturnedAtUtc { get; private set; }

    public DateOnly BusinessDate { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public Guid? CustomerId { get; private set; }

    /// <summary>The value of everything returned.</summary>
    public decimal Total { get; private set; }

    /// <summary>The part taken off the customer's balance (credit sales only).</summary>
    public decimal CreditedToAccount { get; private set; }

    /// <summary>The part handed back: cash for cash and credit sales, a card refund for card sales.</summary>
    public decimal Refunded => Total - CreditedToAccount;

    public string? Reason { get; private set; }

    public IReadOnlyCollection<SaleReturnLine> Lines => _lines;

    /// <param name="drugs">The returned lines' drugs, with all their batches loaded.</param>
    /// <param name="customer">The sale's customer, required when the sale was on credit.</param>
    public static SaleReturn Create(
        long number,
        DateTime returnedAtUtc,
        DateOnly businessDate,
        Sale sale,
        IReadOnlyList<(Guid SaleLineId, int Quantity)> items,
        IReadOnlyDictionary<Guid, Drug> drugs,
        Customer? customer,
        string? reason)
    {
        if (items.Count == 0)
        {
            throw new DomainException("return.no_lines", "Choose at least one item to take back.");
        }

        if (items.Select(i => i.SaleLineId).Distinct().Count() != items.Count)
        {
            throw new DomainException("return.duplicate_line", "Each sale line can appear once in a return.");
        }

        if (sale.PaymentMethod == PaymentMethod.Credit && (customer is null || customer.Id != sale.CustomerId))
        {
            throw new DomainException("return.customer_required", "A credit sale's return needs its customer's account.");
        }

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > ReasonMaxLength)
        {
            throw new DomainException("return.invalid_reason", $"The reason must be at most {ReasonMaxLength} characters.");
        }

        var saleReturn = new SaleReturn
        {
            Id = Guid.NewGuid(),
            Number = number,
            SaleId = sale.Id,
            SaleNumber = sale.Number,
            ReturnedAtUtc = returnedAtUtc,
            BusinessDate = businessDate,
            PaymentMethod = sale.PaymentMethod,
            CustomerId = sale.CustomerId,
            Reason = reason,
        };

        foreach (var (saleLineId, quantity) in items)
        {
            var line = sale.Lines.SingleOrDefault(l => l.Id == saleLineId)
                ?? throw new DomainException("return.unknown_line", $"Line {saleLineId} is not on sale {sale.Number}.");
            var drug = drugs.GetValueOrDefault(line.DrugId)
                ?? throw new DomainException("return.batch_missing", $"Drug {line.DrugId} was not loaded.");

            var (amount, subUnits, restock) = line.Return(quantity);
            foreach (var (batchId, batchQuantity) in restock)
            {
                drug.Restock(batchId, batchQuantity);
            }

            saleReturn._lines.Add(SaleReturnLine.Create(saleReturn.Id, line, quantity, subUnits, amount));
        }

        saleReturn.Total = saleReturn._lines.Sum(l => l.Amount);
        if (sale.PaymentMethod == PaymentMethod.Credit)
        {
            saleReturn.CreditedToAccount = customer!.CreditReturn(saleReturn.Total);
        }

        return saleReturn;
    }
}

public sealed class SaleReturnLine
{
    private SaleReturnLine()
    {
        DrugNameAr = string.Empty;
        UnitName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SaleReturnId { get; private set; }

    public Guid SaleLineId { get; private set; }

    public Guid DrugId { get; private set; }

    public string DrugNameAr { get; private set; }

    public SaleUnit Unit { get; private set; }

    public string UnitName { get; private set; }

    public int Quantity { get; private set; }

    public int QuantityInSubUnits { get; private set; }

    public decimal Amount { get; private set; }

    internal static SaleReturnLine Create(Guid saleReturnId, SaleLine line, int quantity, int quantityInSubUnits, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            SaleReturnId = saleReturnId,
            SaleLineId = line.Id,
            DrugId = line.DrugId,
            DrugNameAr = line.DrugNameAr,
            Unit = line.Unit,
            UnitName = line.UnitName,
            Quantity = quantity,
            QuantityInSubUnits = quantityInSubUnits,
            Amount = amount,
        };
}
