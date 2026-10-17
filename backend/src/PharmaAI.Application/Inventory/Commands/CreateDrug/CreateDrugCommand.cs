using MediatR;

namespace PharmaAI.Application.Inventory.Commands.CreateDrug;

public sealed record CreateDrugCommand(
    string NameAr,
    string? NameEn,
    string Barcode,
    string PackUnitName,
    string SubUnitName,
    int SubUnitsPerPack,
    long PublicPricePiasters) : IRequest<DrugDetailsDto>;
