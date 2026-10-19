using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Application.Sales.Queries.GetSale;

/// <summary>A sale by id, or by its receipt number (typed in when a customer brings items back).</summary>
public sealed record GetSaleQuery(Guid? Id = null, long? Number = null) : IRequest<SaleDto?>;

public sealed class GetSaleQueryHandler(IPharmaDbContext db) : IRequestHandler<GetSaleQuery, SaleDto?>
{
    public async Task<SaleDto?> Handle(GetSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Batches)
            .SingleOrDefaultAsync(s => request.Id != null ? s.Id == request.Id : s.Number == request.Number, cancellationToken);

        return sale?.ToDto();
    }
}
