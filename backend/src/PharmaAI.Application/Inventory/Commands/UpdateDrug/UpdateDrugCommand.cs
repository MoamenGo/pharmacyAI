using MediatR;

namespace PharmaAI.Application.Inventory.Commands.UpdateDrug;

/// <summary>Corrects a drug's names, barcode, price and alert level. The pack size cannot change once created.</summary>
public sealed record UpdateDrugCommand(
    Guid Id,
    string NameAr,
    string? NameEn,
    string Barcode,
    long PublicPricePiasters,
    string? ActiveIngredient,
    string? Manufacturer,
    int ReorderLevelInPacks) : IRequest<DrugDetailsDto>;
