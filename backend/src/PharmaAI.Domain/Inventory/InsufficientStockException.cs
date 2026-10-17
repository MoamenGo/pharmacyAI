using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Inventory;

public sealed class InsufficientStockException(int requestedInSubUnits, int availableInSubUnits)
    : DomainException(
        "stock.insufficient",
        $"Requested {requestedInSubUnits} sub-units but only {availableInSubUnits} unexpired sub-units are in stock.")
{
    public int RequestedInSubUnits { get; } = requestedInSubUnits;

    public int AvailableInSubUnits { get; } = availableInSubUnits;

    /// <summary>The drug that ran short, so a multi-line sale can point at the right line.</summary>
    public Guid? DrugId { get; init; }
}
