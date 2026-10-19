using MediatR;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales.Commands.CreateSale;

public sealed record CreateSaleItem(Guid DrugId, SaleUnit Unit, int Quantity);

/// <param name="PaidPiasters">Cash handed over; for a credit sale, the part paid now. Ignored for a card.</param>
/// <param name="DiscountPiasters">A discount on the whole receipt.</param>
/// <param name="CustomerId">Required for a credit sale.</param>
public sealed record CreateSaleCommand(
    IReadOnlyList<CreateSaleItem> Items,
    long PaidPiasters,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    long DiscountPiasters = 0,
    Guid? CustomerId = null,
    string? CardReference = null) : IRequest<SaleDto>;
