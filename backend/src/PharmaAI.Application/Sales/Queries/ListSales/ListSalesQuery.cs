using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Sales.Queries.ListSales;

/// <summary>The sales of one local day (today when no date is given), newest first, with the day's total.</summary>
public sealed record ListSalesQuery(DateOnly? Date = null) : IRequest<DailySalesDto>;

public sealed class ListSalesQueryHandler(IPharmaDbContext db, PharmacyCalendar calendar) : IRequestHandler<ListSalesQuery, DailySalesDto>
{
    public async Task<DailySalesDto> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? calendar.Today;

        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.BusinessDate == date)
            .OrderByDescending(s => s.Number)
            .Select(s => new { s.Id, s.Number, s.SoldAtUtc, s.Total, LineCount = s.Lines.Count })
            .ToListAsync(cancellationToken);

        // SQLite stores decimals as TEXT and cannot SUM them exactly, so the day's total is added up here.
        var summaries = sales
            .Select(s => new SaleSummaryDto(s.Id, s.Number, s.SoldAtUtc, s.LineCount, Piasters.FromPounds(s.Total)))
            .ToList();

        return new DailySalesDto(date, summaries.Count, summaries.Sum(s => s.TotalPiasters), summaries);
    }
}
