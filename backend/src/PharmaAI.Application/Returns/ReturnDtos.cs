using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Returns;

public sealed record ReturnLineDto(Guid SaleLineId, string DrugNameAr, SaleUnit Unit, string UnitName, int Quantity, long AmountPiasters);

/// <param name="RefundedPiasters">Handed back: cash, or a card refund for a card sale.</param>
/// <param name="CreditedPiasters">Taken off the customer's balance (credit sales).</param>
public sealed record ReturnDto(
    Guid Id,
    long Number,
    Guid SaleId,
    long SaleNumber,
    DateTime ReturnedAtUtc,
    DateOnly BusinessDate,
    PaymentMethod PaymentMethod,
    long TotalPiasters,
    long RefundedPiasters,
    long CreditedPiasters,
    string? Reason,
    IReadOnlyList<ReturnLineDto> Lines);

public static class ReturnsMapping
{
    public static ReturnDto ToDto(this SaleReturn r) =>
        new(
            r.Id,
            r.Number,
            r.SaleId,
            r.SaleNumber,
            r.ReturnedAtUtc,
            r.BusinessDate,
            r.PaymentMethod,
            Common.Piasters.FromPounds(r.Total),
            Common.Piasters.FromPounds(r.Refunded),
            Common.Piasters.FromPounds(r.CreditedToAccount),
            r.Reason,
            r.Lines
                .Select(l => new ReturnLineDto(l.SaleLineId, l.DrugNameAr, l.Unit, l.UnitName, l.Quantity, Common.Piasters.FromPounds(l.Amount)))
                .ToList());
}
