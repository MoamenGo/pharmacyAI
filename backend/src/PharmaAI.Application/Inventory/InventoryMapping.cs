using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory;

internal static class InventoryMapping
{
    public static DrugSummaryDto ToSummary(this Drug drug, DateOnly today)
    {
        var withStock = drug.Batches.Where(b => b.QuantityInSubUnits > 0).ToList();
        var sellable = withStock.Where(b => !b.IsExpiredOn(today)).ToList();

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
            sellable.Sum(b => b.QuantityInSubUnits),
            withStock.Where(b => b.IsExpiredOn(today)).Sum(b => b.QuantityInSubUnits),
            sellable.Count == 0 ? null : sellable.Min(b => b.ExpiryDate));
    }

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
