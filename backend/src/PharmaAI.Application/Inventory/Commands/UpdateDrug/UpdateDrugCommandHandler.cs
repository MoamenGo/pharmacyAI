using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.UpdateDrug;

public sealed class UpdateDrugCommandHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<UpdateDrugCommand, DrugDetailsDto>
{
    public async Task<DrugDetailsDto> Handle(UpdateDrugCommand request, CancellationToken cancellationToken)
    {
        var drug = await db.Drugs
            .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
            .SingleOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Drug), request.Id);

        drug.Update(
            request.NameAr,
            request.NameEn,
            request.Barcode,
            Piasters.ToPounds(request.PublicPricePiasters),
            request.ActiveIngredient,
            request.Manufacturer,
            request.ReorderLevelInPacks);

        await db.SaveChangesAsync(cancellationToken);
        return drug.ToDetails(calendar.Today);
    }
}
