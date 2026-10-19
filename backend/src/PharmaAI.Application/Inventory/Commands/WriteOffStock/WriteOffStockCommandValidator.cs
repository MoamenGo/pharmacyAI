using FluentValidation;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.WriteOffStock;

public sealed class WriteOffStockCommandValidator : AbstractValidator<WriteOffStockCommand>
{
    public WriteOffStockCommandValidator()
    {
        RuleFor(c => c.QuantityInSubUnits).GreaterThan(0);
        RuleFor(c => c.Reason).IsInEnum();
        RuleFor(c => c.Note).MaximumLength(StockAdjustment.NoteMaxLength);
    }
}
