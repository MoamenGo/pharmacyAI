using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Inventory;

public enum WriteOffReason
{
    Expired = 1,
    Damaged = 2,
    Lost = 3,
    Other = 4,
}

/// <summary>
/// A record of stock removed without a sale. Kept for good, so the pharmacist (and an inspector)
/// can see why the shelf holds less than was received and sold.
/// </summary>
public sealed class StockAdjustment
{
    public const int NoteMaxLength = 300;

    private StockAdjustment()
    {
        BatchNumber = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid DrugId { get; private set; }

    public Guid StockBatchId { get; private set; }

    public string BatchNumber { get; private set; }

    public DateOnly ExpiryDate { get; private set; }

    public int QuantityInSubUnits { get; private set; }

    public WriteOffReason Reason { get; private set; }

    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static StockAdjustment ForWriteOff(StockBatch batch, int quantityInSubUnits, WriteOffReason reason, string? note, DateTime createdAtUtc)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new DomainException("stock.invalid_reason", $"Unknown write-off reason {reason}.");
        }

        return new StockAdjustment
        {
            Id = Guid.NewGuid(),
            DrugId = batch.DrugId,
            StockBatchId = batch.Id,
            BatchNumber = batch.BatchNumber,
            ExpiryDate = batch.ExpiryDate,
            QuantityInSubUnits = quantityInSubUnits,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = createdAtUtc,
        };
    }
}
