using FluentValidation;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.ReceiveStock;

public sealed class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
{
    public const int MaxPacksPerReceipt = 100_000;

    public ReceiveStockCommandValidator(PharmacyCalendar calendar)
    {
        RuleFor(c => c.DrugId).NotEmpty();
        RuleFor(c => c.BatchNumber).NotEmpty().MaximumLength(StockBatch.BatchNumberMaxLength);

        RuleFor(c => c.ExpiryDate)
            .Must(expiry => expiry >= calendar.Today)
            .WithErrorCode("stock.already_expired")
            .WithMessage("Cannot receive stock that has already expired.");

        RuleFor(c => c.Packs).InclusiveBetween(0, MaxPacksPerReceipt);
        RuleFor(c => c.LooseSubUnits).InclusiveBetween(0, Drug.MaxSubUnitsPerPack);

        RuleFor(c => c)
            .Must(c => c.Packs > 0 || c.LooseSubUnits > 0)
            .WithName("quantity")
            .WithMessage("Receive at least one pack or one sub-unit.");
    }
}
