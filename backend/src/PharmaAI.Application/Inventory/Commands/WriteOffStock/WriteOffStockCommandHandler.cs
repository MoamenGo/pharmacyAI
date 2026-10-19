using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.WriteOffStock;

/// <summary>
/// Takes the stock out and stores the reason in the same SaveChanges. A sale touching the same
/// batch at the same moment trips the batch version, so we reload and try again.
/// </summary>
public sealed class WriteOffStockCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<WriteOffStockCommand, DrugDetailsDto>
{
    public const int MaxAttempts = 3;

    public async Task<DrugDetailsDto> Handle(WriteOffStockCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var drug = await db.Drugs
                .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
                .SingleOrDefaultAsync(d => d.Id == request.DrugId, cancellationToken)
                ?? throw new NotFoundException(nameof(Drug), request.DrugId);

            var batch = drug.WriteOff(request.BatchId, request.QuantityInSubUnits);
            db.StockAdjustments.Add(StockAdjustment.ForWriteOff(
                batch, request.QuantityInSubUnits, request.Reason, request.Note, clock.GetUtcNow().UtcDateTime));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return drug.ToDetails(calendar.Today);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                db.ClearChangeTracker();
            }
        }
    }
}
