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
    long DiscountPiasters,
    IReadOnlyList<SaleLineBatchDto> Batches,
    Guid Id,
    int ReturnedQuantity,
    long ReturnedPiasters);

public sealed record SaleDto(
    Guid Id,
    long Number,
    DateTime SoldAtUtc,
    DateOnly BusinessDate,
    PaymentMethod PaymentMethod,
    long SubtotalPiasters,
    long DiscountPiasters,
    long TotalPiasters,
    long PaidPiasters,
    long ChangePiasters,
    long DuePiasters,
    Guid? CustomerId,
    string? CustomerName,
    string? CardReference,
    IReadOnlyList<SaleLineDto> Lines);

public sealed record SaleSummaryDto(
    Guid Id,
    long Number,
    DateTime SoldAtUtc,
    int LineCount,
    PaymentMethod PaymentMethod,
    string? CustomerName,
    long TotalPiasters);

/// <param name="CashPiasters">Cash taken for sales: cash sales plus what was paid up front on credit sales.</param>
/// <param name="CardPiasters">Card sales.</param>
/// <param name="CreditPiasters">Sales added to customers' balances.</param>
/// <param name="CollectedPiasters">Cash customers paid towards their balances; the drawer holds this plus <paramref name="CashPiasters"/>, less <paramref name="CashRefundsPiasters"/>.</param>
/// <param name="ReturnsPiasters">The value of everything returned that day, whichever day it was sold.</param>
/// <param name="CashRefundsPiasters">Cash handed back for returns.</param>
/// <param name="CardRefundsPiasters">Card refunds for returns of card sales.</param>
public sealed record DailySalesDto(
    DateOnly Date,
    int Count,
    long TotalPiasters,
    long DiscountPiasters,
    long CashPiasters,
    long CardPiasters,
    long CreditPiasters,
    long CollectedPiasters,
    int ReturnsCount,
    long ReturnsPiasters,
    long CashRefundsPiasters,
    long CardRefundsPiasters,
    IReadOnlyList<SaleSummaryDto> Sales);
