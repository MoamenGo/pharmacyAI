using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Customers;

namespace PharmaAI.Application.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(string Name, string? Phone) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator(IPharmaDbContext db)
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Customer.NameMaxLength);

        RuleFor(c => c.Phone)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(Customer.PhoneMaxLength)
            .Matches("^[0-9+ -]+$").WithMessage("A phone number may contain only digits, spaces, + and -.")
            .MustAsync(async (phone, ct) => !await db.Customers.AnyAsync(c => c.Phone == phone!.Trim(), ct))
            .WithErrorCode("customer.phone_taken")
            .WithMessage("A customer with this phone number already exists.")
            .When(c => !string.IsNullOrWhiteSpace(c.Phone));
    }
}

public sealed class CreateCustomerCommandHandler(IPharmaDbContext db, TimeProvider clock)
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = Customer.Create(request.Name, request.Phone, clock.GetUtcNow().UtcDateTime);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return customer.ToDto();
    }
}
