using MediatR;

namespace PharmaAI.Application.Inventory.Commands.ReceiveStock;

/// <summary>Receives <see cref="Packs"/> full packs plus <see cref="LooseSubUnits"/> loose strips/ampoules.</summary>
public sealed record ReceiveStockCommand(
    Guid DrugId,
    string BatchNumber,
    DateOnly ExpiryDate,
    int Packs,
    int LooseSubUnits) : IRequest<DrugDetailsDto>;
