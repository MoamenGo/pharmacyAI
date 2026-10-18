using FluentValidation;
using PharmaAI.Application.Sync.Eta;

namespace PharmaAI.Application.Sync.Commands.SaveEtaSettings;

/// <summary>Length limits from the ETA receipt and POS login specs.</summary>
public sealed class SaveEtaSettingsCommandValidator : AbstractValidator<SaveEtaSettingsCommand>
{
    public SaveEtaSettingsCommandValidator()
    {
        RuleFor(c => c.Environment).IsInEnum();
        RuleFor(c => c.ClientId).NotNull().MaximumLength(100);
        RuleFor(c => c.ClientSecret).MaximumLength(100);
        RuleFor(c => c.PosSerial).NotNull().MaximumLength(100);
        RuleFor(c => c.PosOsVersion).NotNull().MaximumLength(50);
        RuleFor(c => c.PosModelFramework).NotNull().MaximumLength(10);
        RuleFor(c => c.PresharedKey).MaximumLength(200);
        RuleFor(c => c.Rin).NotNull().Matches("^\\d*$").WithErrorCode("eta.invalid_rin").WithMessage("The RIN is digits only.").MaximumLength(30);
        RuleFor(c => c.CompanyTradeName).NotNull().MaximumLength(200);
        RuleFor(c => c.BranchCode).NotNull().MaximumLength(50);
        RuleFor(c => c.Governate).NotNull().MaximumLength(100);
        RuleFor(c => c.RegionCity).NotNull().MaximumLength(100);
        RuleFor(c => c.Street).NotNull().MaximumLength(200);
        RuleFor(c => c.BuildingNumber).NotNull().MaximumLength(100);
        RuleFor(c => c.ActivityCode).NotNull().MaximumLength(10);
    }
}
