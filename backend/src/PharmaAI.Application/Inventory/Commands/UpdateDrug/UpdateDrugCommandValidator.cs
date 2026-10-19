using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Inventory.Commands.CreateDrug;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.UpdateDrug;

public sealed class UpdateDrugCommandValidator : AbstractValidator<UpdateDrugCommand>
{
    public UpdateDrugCommandValidator(IPharmaDbContext db)
    {
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(Drug.NameMaxLength);
        RuleFor(c => c.NameEn).MaximumLength(Drug.NameMaxLength);
        RuleFor(c => c.PublicPricePiasters).GreaterThan(0).LessThanOrEqualTo(CreateDrugCommandValidator.MaxPricePiasters);
        RuleFor(c => c.ActiveIngredient).MaximumLength(Drug.ActiveIngredientMaxLength);
        RuleFor(c => c.Manufacturer).MaximumLength(Drug.ManufacturerMaxLength);
        RuleFor(c => c.ReorderLevelInPacks).InclusiveBetween(0, Drug.MaxReorderLevelInPacks);

        RuleFor(c => c.Barcode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(Drug.BarcodeMaxLength)
            .Matches(CreateDrugCommandValidator.BarcodePattern).WithMessage("Barcode may contain only letters, digits and dashes.")
            .MustAsync(async (command, barcode, ct) => !await db.Drugs.AnyAsync(d => d.Barcode == barcode.Trim() && d.Id != command.Id, ct))
            .WithErrorCode("drug.barcode_taken")
            .WithMessage("A drug with this barcode already exists.");
    }
}
