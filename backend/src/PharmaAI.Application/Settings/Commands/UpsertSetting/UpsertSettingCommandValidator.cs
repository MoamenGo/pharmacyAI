using FluentValidation;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Application.Settings.Commands.UpsertSetting;

public sealed class UpsertSettingCommandValidator : AbstractValidator<UpsertSettingCommand>
{
    public UpsertSettingCommandValidator()
    {
        RuleFor(c => c.Key)
            .NotEmpty()
            .MaximumLength(AppSetting.KeyMaxLength)
            .Matches("^[a-z0-9]+(\\.[a-z0-9]+)*$")
            .WithMessage("Key must be lowercase segments separated by dots, e.g. 'pharmacy.name'.")
            .Must(key => !SettingKeys.IsReserved(key))
            .WithErrorCode("settings.reserved")
            .WithMessage("This setting has its own screen.");

        RuleFor(c => c.Value)
            .NotNull()
            .MaximumLength(AppSetting.ValueMaxLength);
    }
}
