using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Returns.Queries.ListReturns;

/// <summary>The returns of one local day (today when no date is given), or of one sale, newest first.</summary>
public sealed record ListReturnsQuery(DateOnly? Date = null, Guid? SaleId = null) : IRequest<IReadOnlyList<ReturnDto>>;

public sealed class ListReturnsQueryHandler(IPharmaDbContext db, PharmacyCalendar calendar)
    : IRequestHandler<ListReturnsQuery, IReadOnlyList<ReturnDto>>
{
    public async Task<IReadOnlyList<ReturnDto>> Handle(ListReturnsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SaleReturns.AsNoTracking().Include(r => r.Lines).AsQueryable();
        if (request.SaleId is { } saleId)
        {
            query = query.Where(r => r.SaleId == saleId);
        }
        else
        {
            var date = request.Date ?? calendar.Today;
            query = query.Where(r => r.BusinessDate == date);
        }

        var returns = await query.OrderByDescending(r => r.Number).ToListAsync(cancellationToken);
        return returns.Select(r => r.ToDto()).ToList();
    }
}
