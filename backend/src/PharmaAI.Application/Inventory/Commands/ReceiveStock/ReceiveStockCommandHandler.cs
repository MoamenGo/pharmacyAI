using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.ReceiveStock;

public sealed class ReceiveStockCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<ReceiveStockCommand, DrugDetailsDto>
{
    public async Task<DrugDetailsDto> Handle(ReceiveStockCommand request, CancellationToken cancellationToken)
    {
        var drug = await db.Drugs
            .Include(d => d.Batches)
            .SingleOrDefaultAsync(d => d.Id == request.DrugId, cancellationToken)
            ?? throw new NotFoundException(nameof(Drug), request.DrugId);

        if (request.LooseSubUnits >= drug.SubUnitsPerPack && drug.SubUnitsPerPack > 1)
        {
            throw new DomainException(
                "stock.loose_exceeds_pack",
                $"Loose sub-units must be fewer than a full pack ({drug.SubUnitsPerPack}).");
        }

        var quantity = checked(request.Packs * drug.SubUnitsPerPack + request.LooseSubUnits);
        drug.ReceiveStock(request.BatchNumber, request.ExpiryDate, quantity, clock.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(cancellationToken);

        return drug.ToDetails(calendar.Today);
    }
}
