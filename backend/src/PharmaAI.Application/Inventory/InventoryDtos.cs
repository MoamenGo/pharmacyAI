namespace PharmaAI.Application.Inventory;

public sealed record DrugSummaryDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string Barcode,
    string PackUnitName,
    string SubUnitName,
    int SubUnitsPerPack,
    long PublicPricePiasters,
    long SubUnitPricePiasters,
    int SellableQuantityInSubUnits,
    int ExpiredQuantityInSubUnits,
    DateOnly? NearestExpiry);

public sealed record StockBatchDto(
    Guid Id,
    string BatchNumber,
    DateOnly ExpiryDate,
    int QuantityInSubUnits,
    bool IsExpired,
    DateTime ReceivedAtUtc);

public sealed record DrugDetailsDto(DrugSummaryDto Drug, IReadOnlyList<StockBatchDto> Batches);
