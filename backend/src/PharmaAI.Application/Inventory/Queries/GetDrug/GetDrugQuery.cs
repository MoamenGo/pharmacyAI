using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Inventory.Queries.GetDrug;

/// <summary>Looks a drug up by id or by barcode; exactly one of them must be set.</summary>
public sealed record GetDrugQuery(Guid? Id = null, string? Barcode = null) : IRequest<DrugDetailsDto?>;

public sealed class GetDrugQueryHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<GetDrugQuery, DrugDetailsDto?>
{
    public async Task<DrugDetailsDto?> Handle(GetDrugQuery request, CancellationToken cancellationToken)
    {
        var query = db.Drugs.AsNoTracking().Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0));

        var drug = request.Id is { } id
            ? await query.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            : await query.SingleOrDefaultAsync(d => d.Barcode == request.Barcode!.Trim(), cancellationToken);

        return drug?.ToDetails(calendar.Today);
    }
}
