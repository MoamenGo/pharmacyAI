using MediatR;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales.Commands.CreateSale;

public sealed record CreateSaleItem(Guid DrugId, SaleUnit Unit, int Quantity);

public sealed record CreateSaleCommand(IReadOnlyList<CreateSaleItem> Items, long PaidPiasters) : IRequest<SaleDto>;
