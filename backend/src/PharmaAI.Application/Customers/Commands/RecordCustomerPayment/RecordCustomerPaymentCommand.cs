using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Customers;

namespace PharmaAI.Application.Customers.Commands.RecordCustomerPayment;

/// <summary>Cash a customer pays towards their balance.</summary>
public sealed record RecordCustomerPaymentCommand(Guid CustomerId, long AmountPiasters, string? Note) : IRequest<CustomerDto>;

public sealed class RecordCustomerPaymentCommandValidator : AbstractValidator<RecordCustomerPaymentCommand>
{
    /// <summary>EGP 1,000,000, matching the till's limit on a single payment.</summary>
    public const long MaxAmountPiasters = 100_000_000;

    public RecordCustomerPaymentCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.AmountPiasters).GreaterThan(0).LessThanOrEqualTo(MaxAmountPiasters);
        RuleFor(c => c.Note).MaximumLength(CustomerPayment.NoteMaxLength);
    }
}

public sealed class RecordCustomerPaymentCommandHandler(IPharmaDbContext db, TimeProvider clock, PharmacyCalendar calendar)
    : IRequestHandler<RecordCustomerPaymentCommand, CustomerDto>
{
    public const int MaxAttempts = 3;

    public async Task<CustomerDto> Handle(RecordCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

            var payment = customer.RecordPayment(
                Piasters.ToPounds(request.AmountPiasters), clock.GetUtcNow().UtcDateTime, calendar.Today, request.Note);
            db.CustomerPayments.Add(payment);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return customer.ToDto();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // A sale on credit changed the balance meanwhile: check the payment against the new balance.
                db.ClearChangeTracker();
            }
        }
    }
}
