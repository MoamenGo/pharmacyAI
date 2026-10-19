using PharmaAI.Application.Common;
using PharmaAI.Domain.Catalog;

namespace PharmaAI.Application.Catalog;

/// <summary>A product from the Egyptian drug list. The price is the published one and may be out of date.</summary>
public sealed record CatalogDrugDto(
    int Id,
    string NameEn,
    string NameAr,
    string? ActiveIngredient,
    string? Manufacturer,
    string? DrugClass,
    string? Route,
    long? PricePiasters);

internal static class CatalogMapping
{
    public static CatalogDrugDto ToDto(this CatalogDrug c) =>
        new(c.Id, c.NameEn, c.NameAr, c.ActiveIngredient, c.Manufacturer, c.DrugClass, c.Route,
            c.Price is { } price ? Piasters.FromPounds(price) : null);
}
