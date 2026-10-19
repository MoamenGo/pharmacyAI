using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory;

internal static class InventoryMapping
{
    public static DrugSummaryDto ToSummary(this Drug drug, DateOnly today)
    {
        var withStock = drug.Batches.Where(b => b.QuantityInSubUnits > 0).ToList();
        var sellable = withStock.Where(b => !b.IsExpiredOn(today)).ToList();
        var sellableQuantity = sellable.Sum(b => b.QuantityInSubUnits);

        return new DrugSummaryDto(
            drug.Id,
            drug.NameAr,
            drug.NameEn,
            drug.Barcode,
            drug.PackUnitName,
            drug.SubUnitName,
            drug.SubUnitsPerPack,
            Piasters.FromPounds(drug.PublicPrice),
            Piasters.FromPounds(drug.SubUnitPrice),
            sellableQuantity,
            withStock.Where(b => b.IsExpiredOn(today)).Sum(b => b.QuantityInSubUnits),
            sellable.Count == 0 ? null : sellable.Min(b => b.ExpiryDate),
            drug.ActiveIngredient,
            drug.Manufacturer,
            drug.ReorderLevelInPacks,
            IsShort(drug, sellableQuantity));
    }

    /// <summary>A drug with a reorder level is short once its sellable stock drops below that many full packs.</summary>
    public static bool IsShort(Drug drug, int sellableQuantityInSubUnits) =>
        drug.ReorderLevelInPacks > 0 && sellableQuantityInSubUnits < (long)drug.ReorderLevelInPacks * drug.SubUnitsPerPack;

    public static DrugDetailsDto ToDetails(this Drug drug, DateOnly today) =>
        new(
            drug.ToSummary(today),
            drug.Batches
                .Where(b => b.QuantityInSubUnits > 0)
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.ReceivedAtUtc)
                .Select(b => new StockBatchDto(b.Id, b.BatchNumber, b.ExpiryDate, b.QuantityInSubUnits, b.IsExpiredOn(today), b.ReceivedAtUtc))
                .ToList());
}
