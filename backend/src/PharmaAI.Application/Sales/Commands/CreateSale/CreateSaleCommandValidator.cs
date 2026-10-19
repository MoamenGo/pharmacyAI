using FluentValidation;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sales.Commands.CreateSale;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    /// <summary>EGP 1,000,000: far above any till payment, low enough to catch a scanner typed into the wrong box.</summary>
    public const long MaxPaidPiasters = 100_000_000;

    public CreateSaleCommandValidator()
    {
        RuleFor(c => c.Items)
            .NotEmpty()
            .WithErrorCode("sale.empty")
            .WithMessage("Add at least one item.")
            .Must(items => items.Count <= Sale.MaxLines)
            .WithMessage($"A sale holds at most {Sale.MaxLines} lines.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DrugId).NotEmpty();
            item.RuleFor(i => i.Unit).IsInEnum();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, SaleLine.MaxQuantity);
        });

        RuleFor(c => c.PaidPiasters).InclusiveBetween(0, MaxPaidPiasters);
        RuleFor(c => c.DiscountPiasters).InclusiveBetween(0, MaxPaidPiasters);
        RuleFor(c => c.PaymentMethod).IsInEnum();
        RuleFor(c => c.CustomerId)
            .NotEmpty()
            .When(c => c.PaymentMethod == PaymentMethod.Credit)
            .WithErrorCode("sale.customer_required")
            .WithMessage("Choose the customer for a credit sale.");
        RuleFor(c => c.CardReference).MaximumLength(Sale.CardReferenceMaxLength);
    }
}
