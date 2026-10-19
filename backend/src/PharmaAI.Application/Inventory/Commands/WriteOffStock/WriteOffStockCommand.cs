using MediatR;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.WriteOffStock;

/// <summary>Removes expired, damaged or lost stock from one batch and records why.</summary>
public sealed record WriteOffStockCommand(
    Guid DrugId,
    Guid BatchId,
    int QuantityInSubUnits,
    WriteOffReason Reason,
    string? Note) : IRequest<DrugDetailsDto>;
