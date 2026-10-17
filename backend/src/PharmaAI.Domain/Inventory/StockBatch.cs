using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Inventory;

/// <summary>One received batch of a drug with a single expiry date.</summary>
public sealed class StockBatch
{
    public const int BatchNumberMaxLength = 50;

    private StockBatch()
    {
        BatchNumber = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid DrugId { get; private set; }

    public string BatchNumber { get; private set; }

    /// <summary>Last day the batch may be sold.</summary>
    public DateOnly ExpiryDate { get; private set; }

    public int QuantityInSubUnits { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token: two writers can never both spend the same stock.</summary>
    public int Version { get; private set; }

    public bool IsExpiredOn(DateOnly today) => ExpiryDate < today;

    internal static StockBatch Create(Guid drugId, string batchNumber, DateOnly expiryDate, int quantityInSubUnits, DateTime receivedAtUtc)
    {
        EnsurePositive(quantityInSubUnits);

        return new StockBatch
        {
            Id = Guid.NewGuid(),
            DrugId = drugId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            QuantityInSubUnits = quantityInSubUnits,
            ReceivedAtUtc = receivedAtUtc,
        };
    }

    internal void Add(int quantityInSubUnits)
    {
        EnsurePositive(quantityInSubUnits);

        if ((long)QuantityInSubUnits + quantityInSubUnits > int.MaxValue)
        {
            throw new DomainException("stock.quantity_too_large", $"Batch {BatchNumber} cannot hold that many sub-units.");
        }

        QuantityInSubUnits += quantityInSubUnits;
        Version++;
    }

    internal void Take(int quantityInSubUnits)
    {
        EnsurePositive(quantityInSubUnits);

        if (quantityInSubUnits > QuantityInSubUnits)
        {
            throw new DomainException("stock.batch_overdrawn", $"Batch {BatchNumber} holds only {QuantityInSubUnits} sub-units.");
        }

        QuantityInSubUnits -= quantityInSubUnits;
        Version++;
    }

    private static void EnsurePositive(int quantityInSubUnits)
    {
        if (quantityInSubUnits <= 0)
        {
            throw new DomainException("stock.invalid_quantity", "Quantity must be greater than zero.");
        }
    }
}
