namespace PharmaAI.Domain.Sales;

/// <summary>How a line was sold: whole packs, or loose sub-units (strips, ampoules, ...).</summary>
public enum SaleUnit
{
    Pack = 1,
    SubUnit = 2,
}
