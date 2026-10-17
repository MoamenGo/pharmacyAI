using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Inventory.Queries.ListDrugs;

/// <summary>Searches by exact barcode (scanner input) or by part of the Arabic or English name.</summary>
public sealed record ListDrugsQuery(string? Search, int Limit = ListDrugsQuery.DefaultLimit) : IRequest<IReadOnlyList<DrugSummaryDto>>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}

public sealed class ListDrugsQueryHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<ListDrugsQuery, IReadOnlyList<DrugSummaryDto>>
{
    public async Task<IReadOnlyList<DrugSummaryDto>> Handle(ListDrugsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Drugs.AsNoTracking().Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0)).AsQueryable();
        var search = request.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(d =>
                d.Barcode == search
                || EF.Functions.Like(d.NameAr, pattern, "\\")
                || (d.NameEn != null && EF.Functions.Like(d.NameEn, pattern, "\\")));
        }

        var drugs = await query
            .OrderBy(d => d.NameAr)
            .Take(Math.Clamp(request.Limit, 1, ListDrugsQuery.MaxLimit))
            .ToListAsync(cancellationToken);

        var today = calendar.Today;
        return drugs.Select(d => d.ToSummary(today)).ToList();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
