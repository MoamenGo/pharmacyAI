using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales;

public sealed record SaleLineBatchDto(string BatchNumber, DateOnly ExpiryDate, int QuantityInSubUnits);

public sealed record SaleLineDto(
    int LineNumber,
    Guid DrugId,
    string DrugNameAr,
    SaleUnit Unit,
    string UnitName,
    int Quantity,
    int QuantityInSubUnits,
    long UnitPricePiasters,
    long LineTotalPiasters,
    IReadOnlyList<SaleLineBatchDto> Batches);

public sealed record SaleDto(
    Guid Id,
    long Number,
    DateTime SoldAtUtc,
    DateOnly BusinessDate,
    long TotalPiasters,
    long PaidPiasters,
    long ChangePiasters,
    IReadOnlyList<SaleLineDto> Lines);

public sealed record SaleSummaryDto(Guid Id, long Number, DateTime SoldAtUtc, int LineCount, long TotalPiasters);

public sealed record DailySalesDto(DateOnly Date, int Count, long TotalPiasters, IReadOnlyList<SaleSummaryDto> Sales);
