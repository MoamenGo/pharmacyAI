using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Commands.CreateDrug;

public sealed class CreateDrugCommandValidator : AbstractValidator<CreateDrugCommand>
{
    /// <summary>EGP 1,000,000 per pack: far above any real price, catches a misplaced decimal point.</summary>
    public const long MaxPricePiasters = 100_000_000;

    public CreateDrugCommandValidator(IPharmaDbContext db)
    {
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(Drug.NameMaxLength);
        RuleFor(c => c.NameEn).MaximumLength(Drug.NameMaxLength);
        RuleFor(c => c.PackUnitName).NotEmpty().MaximumLength(Drug.UnitNameMaxLength);
        RuleFor(c => c.SubUnitName).NotEmpty().MaximumLength(Drug.UnitNameMaxLength);
        RuleFor(c => c.SubUnitsPerPack).InclusiveBetween(1, Drug.MaxSubUnitsPerPack);
        RuleFor(c => c.PublicPricePiasters).GreaterThan(0).LessThanOrEqualTo(MaxPricePiasters);

        RuleFor(c => c.Barcode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(Drug.BarcodeMaxLength)
            .Matches("^[0-9A-Za-z-]+$").WithMessage("Barcode may contain only letters, digits and dashes.")
            .MustAsync(async (barcode, ct) => !await db.Drugs.AnyAsync(d => d.Barcode == barcode.Trim(), ct))
            .WithErrorCode("drug.barcode_taken")
            .WithMessage("A drug with this barcode already exists.");
    }
}
