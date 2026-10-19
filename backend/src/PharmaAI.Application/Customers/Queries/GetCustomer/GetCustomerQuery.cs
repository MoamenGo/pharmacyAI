using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Customers.Queries.GetCustomer;

/// <summary>A customer with their latest credit sales and payments.</summary>
public sealed record GetCustomerQuery(Guid Id) : IRequest<CustomerDetailsDto?>
{
    public const int EntryLimit = 100;
}

public sealed class GetCustomerQueryHandler(IPharmaDbContext db) : IRequestHandler<GetCustomerQuery, CustomerDetailsDto?>
{
    public async Task<CustomerDetailsDto?> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CustomerId == customer.Id && s.PaymentMethod == PaymentMethod.Credit)
            .OrderByDescending(s => s.SoldAtUtc)
            .Take(GetCustomerQuery.EntryLimit)
            .Select(s => new { s.Id, s.Number, s.SoldAtUtc, s.Total, s.Paid })
            .ToListAsync(cancellationToken);

        var payments = await db.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CustomerId == customer.Id)
            .OrderByDescending(p => p.PaidAtUtc)
            .Take(GetCustomerQuery.EntryLimit)
            .ToListAsync(cancellationToken);

        var entries = sales
            .Select(s => new CustomerEntryDto(
                CustomerEntryKind.Sale, s.SoldAtUtc, Piasters.FromPounds(s.Total - s.Paid), s.Id, s.Number, null))
            .Concat(payments.Select(p => new CustomerEntryDto(
                CustomerEntryKind.Payment, p.PaidAtUtc, Piasters.FromPounds(p.Amount), null, null, p.Note)))
            .OrderByDescending(e => e.AtUtc)
            .Take(GetCustomerQuery.EntryLimit)
            .ToList();

        return new CustomerDetailsDto(customer.ToDto(), entries);
    }
}
