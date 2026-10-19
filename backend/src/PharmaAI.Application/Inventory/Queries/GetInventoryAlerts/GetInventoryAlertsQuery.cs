using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Inventory.Queries.GetInventoryAlerts;

/// <summary>What the pharmacist should act on: drugs to reorder, stock about to expire, and expired stock still on the shelf.</summary>
public sealed record GetInventoryAlertsQuery(int ExpiryWarningDays = GetInventoryAlertsQuery.DefaultExpiryWarningDays) : IRequest<InventoryAlertsDto>
{
    public const int DefaultExpiryWarningDays = 90;
    public const int MaxExpiryWarningDays = 730;
}

public sealed record BatchAlertDto(
    Guid DrugId,
    string DrugNameAr,
    string PackUnitName,
    string SubUnitName,
    int SubUnitsPerPack,
    Guid BatchId,
    string BatchNumber,
    DateOnly ExpiryDate,
    int QuantityInSubUnits);

public sealed record InventoryAlertsDto(
    DateOnly Today,
    IReadOnlyList<DrugSummaryDto> Shortages,
    IReadOnlyList<BatchAlertDto> ExpiringSoon,
    IReadOnlyList<BatchAlertDto> Expired);

public sealed class GetInventoryAlertsQueryHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<GetInventoryAlertsQuery, InventoryAlertsDto>
{
    public async Task<InventoryAlertsDto> Handle(GetInventoryAlertsQuery request, CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var warnUntil = today.AddDays(Math.Clamp(request.ExpiryWarningDays, 0, GetInventoryAlertsQuery.MaxExpiryWarningDays));

        // A pharmacy stocks a few thousand drugs at most; one pass over them is cheaper than several SQL round trips.
        var drugs = await db.Drugs.AsNoTracking()
            .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
            .ToListAsync(cancellationToken);

        var shortages = drugs
            .Select(d => d.ToSummary(today))
            .Where(d => d.IsShort)
            .OrderBy(d => d.SellableQuantityInSubUnits)
            .ThenBy(d => d.NameAr)
            .ToList();

        var batches = drugs
            .SelectMany(d => d.Batches.Select(b => new BatchAlertDto(
                d.Id, d.NameAr, d.PackUnitName, d.SubUnitName, d.SubUnitsPerPack, b.Id, b.BatchNumber, b.ExpiryDate, b.QuantityInSubUnits)))
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.DrugNameAr)
            .ToList();

        return new InventoryAlertsDto(
            today,
            shortages,
            batches.Where(b => b.ExpiryDate >= today && b.ExpiryDate <= warnUntil).ToList(),
            batches.Where(b => b.ExpiryDate < today).ToList());
    }
}
