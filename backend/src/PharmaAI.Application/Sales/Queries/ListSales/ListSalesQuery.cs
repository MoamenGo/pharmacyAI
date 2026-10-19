using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales.Queries.ListSales;

/// <summary>The sales of one local day (today when no date is given), newest first, with the day's totals.</summary>
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
            .Select(s => new
            {
                s.Id,
                s.Number,
                s.SoldAtUtc,
                s.PaymentMethod,
                s.CustomerName,
                s.Total,
                s.Discount,
                s.Paid,
                LineCount = s.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        var collected = await db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.BusinessDate == date)
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);

        // SQLite stores decimals as TEXT and cannot SUM them exactly, so the day's totals are added up here.
        decimal SumOf(PaymentMethod method, Func<decimal, decimal, decimal> amount) =>
            sales.Where(s => s.PaymentMethod == method).Sum(s => amount(s.Total, s.Paid));

        var summaries = sales
            .Select(s => new SaleSummaryDto(s.Id, s.Number, s.SoldAtUtc, s.LineCount, s.PaymentMethod, s.CustomerName, Piasters.FromPounds(s.Total)))
            .ToList();

        return new DailySalesDto(
            date,
            summaries.Count,
            summaries.Sum(s => s.TotalPiasters),
            Piasters.FromPounds(sales.Sum(s => s.Discount)),
            Piasters.FromPounds(SumOf(PaymentMethod.Cash, (total, _) => total) + SumOf(PaymentMethod.Credit, (_, paid) => paid)),
            Piasters.FromPounds(SumOf(PaymentMethod.Card, (total, _) => total)),
            Piasters.FromPounds(SumOf(PaymentMethod.Credit, (total, paid) => total - paid)),
            Piasters.FromPounds(collected.Sum()),
            summaries);
    }
}
