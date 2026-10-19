using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Sales.Commands.CreateSale;

/// <summary>
/// Saves a sale and its stock deductions in one SaveChanges, so either the whole receipt is
/// recorded, every line's stock is taken and (when the tax authority link is on) the receipt is
/// queued for sync, or nothing changes. A concurrent write to the same batches, or another till
/// taking the same receipt number, makes us retry on fresh data.
/// </summary>
public sealed class CreateSaleCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<CreateSaleCommand, SaleDto>
{
    public const int MaxAttempts = 3;

    public async Task<SaleDto> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var drugIds = request.Items.Select(i => i.DrugId).Distinct().ToList();
        // The ETA link is optional. While it is off nothing is queued: ETA refuses receipts older
        // than 24 hours, so sales made before it is switched on could never be sent anyway.
        var queueForEta = (await EtaSettingsStore.LoadAsync(db, cancellationToken)).Enabled;

        for (var attempt = 1; ; attempt++)
        {
            var drugs = await db.Drugs
                .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
                .Where(d => drugIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

            var items = request.Items
                .Select(i => new SaleItem(
                    drugs.GetValueOrDefault(i.DrugId) ?? throw new NotFoundException(nameof(Drug), i.DrugId),
                    i.Unit,
                    i.Quantity))
                .ToList();

            var number = (await db.Sales.MaxAsync(s => (long?)s.Number, cancellationToken) ?? 0) + 1;
            var now = clock.GetUtcNow().UtcDateTime;
            var sale = Sale.Create(number, now, calendar.Today, items, Piasters.ToPounds(request.PaidPiasters));
            db.Sales.Add(sale);
            if (queueForEta)
            {
                // Same SaveChanges as the sale: a receipt can never be saved without being queued for the tax authority.
                db.OutboxMessages.Add(OutboxMessage.ForEtaReceipt(sale.Id, sale.Number, now));
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return sale.ToDto();
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DuplicateKeyException && attempt < MaxAttempts)
            {
                db.ClearChangeTracker();
            }
        }
    }
}
