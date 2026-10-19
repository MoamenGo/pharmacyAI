using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Returns.Commands.CreateReturn;

/// <param name="Quantity">In the unit the line was sold in (boxes or strips).</param>
public sealed record CreateReturnItem(Guid SaleLineId, int Quantity);

public sealed record CreateReturnCommand(Guid SaleId, IReadOnlyList<CreateReturnItem> Items, string? Reason = null) : IRequest<ReturnDto>;

public sealed class CreateReturnCommandValidator : AbstractValidator<CreateReturnCommand>
{
    public CreateReturnCommandValidator()
    {
        RuleFor(c => c.SaleId).NotEmpty();
        RuleFor(c => c.Items)
            .NotEmpty()
            .WithErrorCode("return.no_lines")
            .WithMessage("Choose at least one item to take back.")
            .Must(items => items.Count <= Sale.MaxLines)
            .WithMessage($"A return holds at most {Sale.MaxLines} lines.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SaleLineId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, SaleLine.MaxQuantity);
        });
        RuleFor(c => c.Reason).MaximumLength(SaleReturn.ReasonMaxLength);
    }
}

/// <summary>
/// Saves a return, its restocking and any change to the customer's balance in one SaveChanges. A concurrent return of
/// the same lines, a sale from the same batches, or another till taking the same return number makes us retry on fresh data.
/// </summary>
public sealed class CreateReturnCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<CreateReturnCommand, ReturnDto>
{
    public const int MaxAttempts = 3;

    public async Task<ReturnDto> Handle(CreateReturnCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var sale = await db.Sales
                .Include(s => s.Lines)
                .ThenInclude(l => l.Batches)
                .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Sale), request.SaleId);

            var drugIds = sale.Lines.Select(l => l.DrugId).Distinct().ToList();
            // All batches, including empty and expired ones: stock goes back where it came from.
            var drugs = await db.Drugs
                .Include(d => d.Batches)
                .Where(d => drugIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

            var customer = sale.CustomerId is { } customerId
                ? await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Customer), customerId)
                : null;

            var number = (await db.SaleReturns.MaxAsync(r => (long?)r.Number, cancellationToken) ?? 0) + 1;
            var saleReturn = SaleReturn.Create(
                number,
                clock.GetUtcNow().UtcDateTime,
                calendar.Today,
                sale,
                request.Items.Select(i => (i.SaleLineId, i.Quantity)).ToList(),
                drugs,
                sale.PaymentMethod == PaymentMethod.Credit ? customer : null,
                request.Reason);
            db.SaleReturns.Add(saleReturn);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return saleReturn.ToDto();
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DuplicateKeyException && attempt < MaxAttempts)
            {
                db.ClearChangeTracker();
            }
        }
    }
}
