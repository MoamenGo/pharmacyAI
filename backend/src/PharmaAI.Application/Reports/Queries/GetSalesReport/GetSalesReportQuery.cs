using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Reports.Queries.GetSalesReport;

/// <summary>
/// Sales between two local business dates (both included): per-day totals and returns, best sellers by what was kept
/// (after discounts and returns), and written-off stock.
/// </summary>
public sealed record GetSalesReportQuery(DateOnly From, DateOnly To) : IRequest<SalesReportDto>
{
    public const int MaxDays = 366;
    public const int TopDrugCount = 15;
}

/// <param name="ReturnsPiasters">Refunds given that day, whichever day the items were sold.</param>
public sealed record DailyTotalDto(DateOnly Date, int Count, long TotalPiasters, long ReturnsPiasters);

public sealed record TopDrugDto(Guid DrugId, string DrugNameAr, int QuantityInSubUnits, long TotalPiasters);

public sealed record WriteOffDto(
    Guid Id,
    Guid DrugId,
    string DrugNameAr,
    string BatchNumber,
    DateOnly ExpiryDate,
    int QuantityInSubUnits,
    string PackUnitName,
    string SubUnitName,
    int SubUnitsPerPack,
    WriteOffReason Reason,
    string? Note,
    DateTime CreatedAtUtc);

public sealed record SalesReportDto(
    DateOnly From,
    DateOnly To,
    int Count,
    long TotalPiasters,
    long ReturnsPiasters,
    IReadOnlyList<DailyTotalDto> Days,
    IReadOnlyList<TopDrugDto> TopDrugs,
    IReadOnlyList<WriteOffDto> WriteOffs);

public sealed class GetSalesReportQueryValidator : AbstractValidator<GetSalesReportQuery>
{
    public GetSalesReportQueryValidator()
    {
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).WithMessage("The end date must not be before the start date.");
        RuleFor(q => q)
            .Must(q => q.To.DayNumber - q.From.DayNumber < GetSalesReportQuery.MaxDays)
            .WithName("to")
            .WithMessage($"A report covers at most {GetSalesReportQuery.MaxDays} days.");
    }
}

public sealed class GetSalesReportQueryHandler(IPharmaDbContext db) : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    public async Task<SalesReportDto> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.BusinessDate >= request.From && s.BusinessDate <= request.To)
            .Select(s => new { s.BusinessDate, s.Total })
            .ToListAsync(cancellationToken);

        var lines = await db.Sales
            .AsNoTracking()
            .Where(s => s.BusinessDate >= request.From && s.BusinessDate <= request.To)
            .SelectMany(s => s.Lines)
            .Select(l => new { l.DrugId, l.DrugNameAr, l.Quantity, l.QuantityInSubUnits, l.LineTotal, l.Discount, l.ReturnedQuantity, l.ReturnedAmount })
            .ToListAsync(cancellationToken);

        var returns = await db.SaleReturns
            .AsNoTracking()
            .Where(r => r.BusinessDate >= request.From && r.BusinessDate <= request.To)
            .Select(r => new { r.BusinessDate, r.Total })
            .ToListAsync(cancellationToken);

        // SQLite stores decimals as TEXT and cannot SUM them exactly, so totals are added up here.
        var salesByDay = sales.ToLookup(s => s.BusinessDate);
        var returnsByDay = returns.ToLookup(r => r.BusinessDate);
        var days = salesByDay.Select(g => g.Key)
            .Union(returnsByDay.Select(g => g.Key))
            .Order()
            .Select(day => new DailyTotalDto(
                day,
                salesByDay[day].Count(),
                Piasters.FromPounds(salesByDay[day].Sum(s => s.Total)),
                Piasters.FromPounds(returnsByDay[day].Sum(r => r.Total))))
            .ToList();

        var topDrugs = lines
            .GroupBy(l => l.DrugId)
            .Select(g => new TopDrugDto(
                g.Key,
                g.Last().DrugNameAr,
                g.Sum(l => l.QuantityInSubUnits / l.Quantity * (l.Quantity - l.ReturnedQuantity)),
                g.Sum(l => Piasters.FromPounds(l.LineTotal - l.Discount - l.ReturnedAmount))))
            .Where(d => d.QuantityInSubUnits > 0)
            .OrderByDescending(d => d.TotalPiasters)
            .Take(GetSalesReportQuery.TopDrugCount)
            .ToList();

        var writeOffs = await WriteOffsAsync(request, cancellationToken);

        return new SalesReportDto(
            request.From,
            request.To,
            days.Sum(d => d.Count),
            days.Sum(d => d.TotalPiasters),
            days.Sum(d => d.ReturnsPiasters),
            days,
            topDrugs,
            writeOffs);
    }

    private async Task<IReadOnlyList<WriteOffDto>> WriteOffsAsync(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        // Write-offs are stamped in UTC; widen by a day on each side, then keep the ones on the requested Cairo dates.
        var fromUtc = request.From.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = request.To.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await db.StockAdjustments
            .AsNoTracking()
            .Where(a => a.CreatedAtUtc >= fromUtc && a.CreatedAtUtc < toUtc)
            .Join(db.Drugs, a => a.DrugId, d => d.Id, (a, d) => new { Adjustment = a, d.NameAr, d.PackUnitName, d.SubUnitName, d.SubUnitsPerPack })
            .OrderByDescending(x => x.Adjustment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => PharmacyCalendar.ToLocalDate(x.Adjustment.CreatedAtUtc) is var day && day >= request.From && day <= request.To)
            .Select(x => new WriteOffDto(
                x.Adjustment.Id,
                x.Adjustment.DrugId,
                x.NameAr,
                x.Adjustment.BatchNumber,
                x.Adjustment.ExpiryDate,
                x.Adjustment.QuantityInSubUnits,
                x.PackUnitName,
                x.SubUnitName,
                x.SubUnitsPerPack,
                x.Adjustment.Reason,
                x.Adjustment.Note,
                x.Adjustment.CreatedAtUtc))
            .ToList();
    }
}
