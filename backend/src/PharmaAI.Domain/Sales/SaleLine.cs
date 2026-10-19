using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Domain.Sales;

/// <summary>
/// One drug on a sale. Name, unit and price are copied at the moment of sale so the
/// receipt never changes when the drug is later renamed or repriced.
/// </summary>
public sealed class SaleLine
{
    public const int MaxQuantity = 10_000;

    private readonly List<SaleLineBatch> _batches = [];

    private SaleLine()
    {
        DrugNameAr = string.Empty;
        UnitName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid DrugId { get; private set; }

    public string DrugNameAr { get; private set; }

    public SaleUnit Unit { get; private set; }

    public string UnitName { get; private set; }

    /// <summary>Quantity in <see cref="Unit"/>.</summary>
    public int Quantity { get; private set; }

    public int QuantityInSubUnits { get; private set; }

    public decimal UnitPrice { get; private set; }

    /// <summary>Unit price × quantity, before any discount.</summary>
    public decimal LineTotal { get; private set; }

    /// <summary>This line's share of the receipt discount.</summary>
    public decimal Discount { get; private set; }

    public decimal NetTotal => LineTotal - Discount;

    /// <summary>How much of <see cref="Quantity"/> came back on returns.</summary>
    public int ReturnedQuantity { get; private set; }

    /// <summary>What was refunded for those returns.</summary>
    public decimal ReturnedAmount { get; private set; }

    public int ReturnableQuantity => Quantity - ReturnedQuantity;

    public IReadOnlyCollection<SaleLineBatch> Batches => _batches;

    internal static SaleLine Create(Guid saleId, int lineNumber, SaleItem item, DateOnly today)
    {
        var (drug, unit, quantity) = item;

        if (quantity is < 1 or > MaxQuantity)
        {
            throw new DomainException("sale.invalid_quantity", $"Quantity must be between 1 and {MaxQuantity}.");
        }

        var line = new SaleLine
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            LineNumber = lineNumber,
            DrugId = drug.Id,
            DrugNameAr = drug.NameAr,
            Unit = unit,
            Quantity = quantity,
        };

        (line.UnitName, line.UnitPrice, line.QuantityInSubUnits) = unit switch
        {
            SaleUnit.Pack => (drug.PackUnitName, drug.PublicPrice, quantity * drug.SubUnitsPerPack),
            SaleUnit.SubUnit => (drug.SubUnitName, drug.SubUnitPrice, quantity),
            _ => throw new DomainException("sale.invalid_unit", $"Unknown sale unit {unit}."),
        };
        line.LineTotal = line.UnitPrice * quantity;

        foreach (var allocation in drug.Deduct(line.QuantityInSubUnits, today))
        {
            line._batches.Add(SaleLineBatch.Create(
                line.Id, allocation.BatchId, allocation.BatchNumber, allocation.ExpiryDate, allocation.QuantityInSubUnits));
        }

        return line;
    }

    internal void ApplyDiscount(decimal discount) => Discount = discount;

    /// <summary>
    /// Records <paramref name="quantity"/> (in the line's unit) coming back and works out its refund: its share of the
    /// net amount, or exactly what is left of it when the line is now fully returned, so the refunds never add up to more
    /// than was paid. Returns the refund and the stock to put back, batch by batch.
    /// </summary>
    internal (decimal Amount, int QuantityInSubUnits, IReadOnlyList<(Guid BatchId, int QuantityInSubUnits)> Restock) Return(int quantity)
    {
        if (quantity < 1 || quantity > ReturnableQuantity)
        {
            throw new DomainException(
                "return.invalid_quantity",
                $"Line {LineNumber} can take back between 1 and {ReturnableQuantity} {UnitName}.");
        }

        var perUnit = QuantityInSubUnits / Quantity;
        var alreadyBack = ReturnedQuantity * perUnit;
        var subUnits = quantity * perUnit;

        ReturnedQuantity += quantity;
        var amount = ReturnedQuantity == Quantity
            ? NetTotal - ReturnedAmount
            : Math.Round(NetTotal * quantity / Quantity, 2, MidpointRounding.AwayFromZero);
        ReturnedAmount += amount;

        // Stock goes back to the batches it came from, latest expiry first; earlier returns already refilled those.
        var restock = new List<(Guid, int)>();
        var skip = alreadyBack;
        var left = subUnits;
        foreach (var batch in _batches.OrderByDescending(b => b.ExpiryDate).ThenByDescending(b => b.BatchNumber, StringComparer.Ordinal))
        {
            var available = batch.QuantityInSubUnits;
            var skipped = Math.Min(skip, available);
            skip -= skipped;
            var take = Math.Min(left, available - skipped);
            if (take > 0)
            {
                restock.Add((batch.StockBatchId, take));
                left -= take;
            }
        }

        return (amount, subUnits, restock);
    }
}
