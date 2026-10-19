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
    public const int ActiveIngredientMaxLength = 400;
    public const int ManufacturerMaxLength = 150;
    public const int MaxReorderLevelInPacks = 100_000;

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

    /// <summary>The scientific name (المادة الفعالة), used to find alternatives; null when unknown.</summary>
    public string? ActiveIngredient { get; private set; }

    public string? Manufacturer { get; private set; }

    /// <summary>Below this many full packs of sellable stock the drug is listed as a shortage; 0 turns the alert off.</summary>
    public int ReorderLevelInPacks { get; private set; }

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
        DateTime createdAtUtc,
        string? activeIngredient = null,
        string? manufacturer = null,
        int reorderLevelInPacks = 0)
    {
        if (subUnitsPerPack is < 1 or > MaxSubUnitsPerPack)
        {
            throw new DomainException("drug.invalid_sub_units", $"A pack must hold between 1 and {MaxSubUnitsPerPack} sub-units.");
        }

        var drug = new Drug
        {
            Id = Guid.NewGuid(),
            PackUnitName = packUnitName.Trim(),
            SubUnitName = subUnitName.Trim(),
            SubUnitsPerPack = subUnitsPerPack,
            CreatedAtUtc = createdAtUtc,
        };
        drug.Update(nameAr, nameEn, barcode, publicPrice, activeIngredient, manufacturer, reorderLevelInPacks);
        return drug;
    }

    /// <summary>
    /// Changes what the pharmacist may correct after the drug was created. The pack size is fixed:
    /// stock is counted in sub-units, so changing it would silently change every balance.
    /// Past receipts keep their own copy of the name and price.
    /// </summary>
    public void Update(
        string nameAr,
        string? nameEn,
        string barcode,
        decimal publicPrice,
        string? activeIngredient,
        string? manufacturer,
        int reorderLevelInPacks)
    {
        if (publicPrice <= 0)
        {
            throw new DomainException("drug.invalid_price", "Public price must be greater than zero.");
        }

        if (reorderLevelInPacks is < 0 or > MaxReorderLevelInPacks)
        {
            throw new DomainException("drug.invalid_reorder_level", $"The reorder level must be between 0 and {MaxReorderLevelInPacks} packs.");
        }

        NameAr = nameAr.Trim();
        NameEn = Optional(nameEn);
        Barcode = barcode.Trim();
        PublicPrice = publicPrice;
        ActiveIngredient = Optional(activeIngredient);
        Manufacturer = Optional(manufacturer);
        ReorderLevelInPacks = reorderLevelInPacks;
    }

    /// <summary>
    /// Removes stock that can no longer be sold (expired, damaged, lost) from one batch.
    /// Unlike a sale this may take from an expired batch, which is the point of it.
    /// </summary>
    public StockBatch WriteOff(Guid batchId, int quantityInSubUnits)
    {
        var batch = _batches.SingleOrDefault(b => b.Id == batchId)
            ?? throw new DomainException("stock.batch_not_found", $"Batch {batchId} does not belong to this drug.");

        batch.Take(quantityInSubUnits);
        return batch;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
