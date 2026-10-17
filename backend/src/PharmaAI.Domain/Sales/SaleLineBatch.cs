namespace PharmaAI.Domain.Sales;

/// <summary>The stock batch a sale line was taken from, kept for recalls, returns and tax reporting.</summary>
public sealed class SaleLineBatch
{
    private SaleLineBatch()
    {
        BatchNumber = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SaleLineId { get; private set; }

    public Guid StockBatchId { get; private set; }

    public string BatchNumber { get; private set; }

    public DateOnly ExpiryDate { get; private set; }

    public int QuantityInSubUnits { get; private set; }

    internal static SaleLineBatch Create(Guid saleLineId, Guid stockBatchId, string batchNumber, DateOnly expiryDate, int quantityInSubUnits) =>
        new()
        {
            Id = Guid.NewGuid(),
            SaleLineId = saleLineId,
            StockBatchId = stockBatchId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            QuantityInSubUnits = quantityInSubUnits,
        };
}
