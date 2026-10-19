using MediatR;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.CreateDrug;

public sealed class CreateDrugCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<CreateDrugCommand, DrugDetailsDto>
{
    public async Task<DrugDetailsDto> Handle(CreateDrugCommand request, CancellationToken cancellationToken)
    {
        var drug = Drug.Create(
            request.NameAr,
            request.NameEn,
            request.Barcode,
            request.PackUnitName,
            request.SubUnitName,
            request.SubUnitsPerPack,
            Piasters.ToPounds(request.PublicPricePiasters),
            clock.GetUtcNow().UtcDateTime,
            request.ActiveIngredient,
            request.Manufacturer,
            request.ReorderLevelInPacks);

        db.Drugs.Add(drug);
        await db.SaveChangesAsync(cancellationToken);

        return drug.ToDetails(calendar.Today);
    }
}
