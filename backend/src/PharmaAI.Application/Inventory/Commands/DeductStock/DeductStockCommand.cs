using MediatR;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.DeductStock;

public sealed record DeductStockCommand(Guid DrugId, int QuantityInSubUnits) : IRequest<DeductStockResult>;

public sealed record DeductStockResult(IReadOnlyList<BatchAllocation> Allocations, DrugSummaryDto Drug);
