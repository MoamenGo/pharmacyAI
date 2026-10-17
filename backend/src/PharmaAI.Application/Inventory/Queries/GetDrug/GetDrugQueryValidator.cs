using FluentValidation;

namespace PharmaAI.Application.Inventory.Queries.GetDrug;

public sealed class GetDrugQueryValidator : AbstractValidator<GetDrugQuery>
{
    public GetDrugQueryValidator()
    {
        RuleFor(q => q)
            .Must(q => q.Id.HasValue ^ !string.IsNullOrWhiteSpace(q.Barcode))
            .OverridePropertyName("lookup")
            .WithMessage("Provide either an id or a barcode.");
    }
}
