using PharmaAI.Domain.Common;

namespace PharmaAI.Domain.Inventory;

/// <summary>
/// A sellable product. Stock is always counted in the smallest dispensable unit
/// (strip, ampoule, ...); a pack holds <see cref="SubUnitsPerPack"/> of them.
/// </summary>
public sealed class Drug
{
    public const int NameMaxLength = 200;
    public const int BarcodeMaxLength = 64;
    public const int UnitNameMaxLength = 30;
    public const int MaxSubUnitsPerPack = 1000;

    private readonly List<StockBatch> _batches = [];

    private Drug()
    {
        NameAr = string.Empty;
        Barcode = string.Empty;
        PackUnitName = string.Empty;
        SubUnitName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string NameAr { get; private set; }

    public string? NameEn { get; private set; }

    public string Barcode { get; private set; }

    public string PackUnitName { get; private set; }

    public string SubUnitName { get; private set; }

    public int SubUnitsPerPack { get; private set; }

    /// <summary>Consumer price of one full pack, in EGP.</summary>
    public decimal PublicPrice { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<StockBatch> Batches => _batches;

    /// <summary>Price of one sub-unit, rounded to the piaster.</summary>
    public decimal SubUnitPrice => Math.Round(PublicPrice / SubUnitsPerPack, 2, MidpointRounding.AwayFromZero);

    public static Drug Create(
        string nameAr,
        string? nameEn,
        string barcode,
        string packUnitName,
        string subUnitName,
        int subUnitsPerPack,
        decimal publicPrice,
        DateTime createdAtUtc)
    {
        if (subUnitsPerPack is < 1 or > MaxSubUnitsPerPack)
        {
            throw new DomainException("drug.invalid_sub_units", $"A pack must hold between 1 and {MaxSubUnitsPerPack} sub-units.");
        }

        if (publicPrice <= 0)
        {
            throw new DomainException("drug.invalid_price", "Public price must be greater than zero.");
        }

        return new Drug
        {
            Id = Guid.NewGuid(),
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            Barcode = barcode.Trim(),
            PackUnitName = packUnitName.Trim(),
            SubUnitName = subUnitName.Trim(),
            SubUnitsPerPack = subUnitsPerPack,
            PublicPrice = publicPrice,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>
    /// Records received stock. Stock with the same batch number and expiry is merged into
    /// the existing batch, so re-receiving a batch never splits it.
    /// </summary>
    public StockBatch ReceiveStock(string batchNumber, DateOnly expiryDate, int quantityInSubUnits, DateTime receivedAtUtc)
    {
        var normalized = batchNumber.Trim();
        var existing = _batches.FirstOrDefault(b => b.BatchNumber == normalized && b.ExpiryDate == expiryDate);

        if (existing is not null)
        {
            existing.Add(quantityInSubUnits);
            return existing;
        }

        var batch = StockBatch.Create(Id, normalized, expiryDate, quantityInSubUnits, receivedAtUtc);
        _batches.Add(batch);
        return batch;
    }

    /// <summary>Puts returned stock back into the batch it was sold from, expired or not.</summary>
    internal void Restock(Guid batchId, int quantityInSubUnits)
    {
        var batch = _batches.SingleOrDefault(b => b.Id == batchId)
            ?? throw new DomainException("return.batch_missing", $"Batch {batchId} of drug {Id} was not loaded.");
        batch.Add(quantityInSubUnits);
    }

    /// <summary>
    /// Takes <paramref name="quantityInSubUnits"/> from the batches that expire first (FEFO),
    /// skipping expired batches. Either the whole quantity is taken or nothing is.
    /// </summary>
    public IReadOnlyList<BatchAllocation> Deduct(int quantityInSubUnits, DateOnly today)
    {
        IReadOnlyList<BatchAllocation> allocations;
        try
        {
            allocations = Fefo.Allocate(_batches, quantityInSubUnits, today);
        }
        catch (InsufficientStockException ex)
        {
            throw new InsufficientStockException(ex.RequestedInSubUnits, ex.AvailableInSubUnits) { DrugId = Id };
        }

        foreach (var allocation in allocations)
        {
            _batches.Single(b => b.Id == allocation.BatchId).Take(allocation.QuantityInSubUnits);
        }

        return allocations;
    }
}
