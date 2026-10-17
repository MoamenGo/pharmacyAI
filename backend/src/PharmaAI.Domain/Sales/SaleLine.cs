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

    public decimal LineTotal { get; private set; }

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
}
