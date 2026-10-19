using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Application.Customers.Queries.ListCustomers;

/// <summary>Searches by part of the name or phone. <paramref name="OwingOnly"/> keeps customers with a balance.</summary>
public sealed record ListCustomersQuery(string? Search, bool OwingOnly = false) : IRequest<IReadOnlyList<CustomerDto>>
{
    public const int Limit = 50;
}

public sealed class ListCustomersQueryHandler(IPharmaDbContext db) : IRequestHandler<ListCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<IReadOnlyList<CustomerDto>> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();
        var search = request.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(c => EF.Functions.Like(c.Name, pattern, "\\") || (c.Phone != null && EF.Functions.Like(c.Phone, pattern, "\\")));
        }

        // Balances are stored as TEXT in SQLite, so "owes something" is filtered after loading.
        var customers = await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return customers
            .Where(c => !request.OwingOnly || c.Balance > 0)
            .Take(ListCustomersQuery.Limit)
            .Select(c => c.ToDto())
            .ToList();
    }
}
