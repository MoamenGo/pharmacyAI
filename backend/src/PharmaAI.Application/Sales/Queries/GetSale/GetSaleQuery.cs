using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Application.Sales.Queries.GetSale;

public sealed record GetSaleQuery(Guid Id) : IRequest<SaleDto?>;

public sealed class GetSaleQueryHandler(IPharmaDbContext db) : IRequestHandler<GetSaleQuery, SaleDto?>
{
    public async Task<SaleDto?> Handle(GetSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Batches)
            .SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return sale?.ToDto();
    }
}
