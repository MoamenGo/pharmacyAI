namespace PharmaAI.Domain.Inventory;

public sealed record BatchAllocation(Guid BatchId, string BatchNumber, DateOnly ExpiryDate, int QuantityInSubUnits);

/// <summary>First-Expired, First-Out allocation across a drug's batches.</summary>
public static class Fefo
{
    public static IReadOnlyList<BatchAllocation> Allocate(IEnumerable<StockBatch> batches, int quantityInSubUnits, DateOnly today)
    {
        if (quantityInSubUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityInSubUnits), "Quantity must be greater than zero.");
        }

        var sellable = batches
            .Where(b => b.QuantityInSubUnits > 0 && !b.IsExpiredOn(today))
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.ReceivedAtUtc)
            .ToList();

        var available = sellable.Sum(b => b.QuantityInSubUnits);
        if (available < quantityInSubUnits)
        {
            throw new InsufficientStockException(quantityInSubUnits, available);
        }

        var allocations = new List<BatchAllocation>();
        var remaining = quantityInSubUnits;

        foreach (var batch in sellable)
        {
            var take = Math.Min(batch.QuantityInSubUnits, remaining);
            allocations.Add(new BatchAllocation(batch.Id, batch.BatchNumber, batch.ExpiryDate, take));
            remaining -= take;

            if (remaining == 0)
            {
                break;
            }
        }

        return allocations;
    }
}
