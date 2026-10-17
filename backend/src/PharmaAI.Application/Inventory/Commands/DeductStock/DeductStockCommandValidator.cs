using FluentValidation;

namespace PharmaAI.Application.Inventory.Commands.DeductStock;

public sealed class DeductStockCommandValidator : AbstractValidator<DeductStockCommand>
{
    public const int MaxQuantityInSubUnits = 100_000;

    public DeductStockCommandValidator()
    {
        RuleFor(c => c.DrugId).NotEmpty();
        RuleFor(c => c.QuantityInSubUnits).InclusiveBetween(1, MaxQuantityInSubUnits);
    }
}
