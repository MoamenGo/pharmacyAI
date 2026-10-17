using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.DeductStock;

/// <summary>
/// Deducts stock FEFO. Batch rows carry a version token, so if another writer touched the same
/// batches between our read and our write, SaveChanges fails and we retry on fresh data.
/// </summary>
public sealed class DeductStockCommandHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<DeductStockCommand, DeductStockResult>
{
    public const int MaxAttempts = 3;

    public async Task<DeductStockResult> Handle(DeductStockCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var drug = await db.Drugs
                .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
                .SingleOrDefaultAsync(d => d.Id == request.DrugId, cancellationToken)
                ?? throw new NotFoundException(nameof(Drug), request.DrugId);

            var today = calendar.Today;
            var allocations = drug.Deduct(request.QuantityInSubUnits, today);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new DeductStockResult(allocations, drug.ToSummary(today));
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                db.ClearChangeTracker();
            }
        }
    }
}
