namespace PharmaAI.Domain.Catalog;

/// <summary>
/// One product from the bundled Egyptian drug list. Read-only reference data: it is never sold
/// or stocked directly, only used to fill in a new drug's details and to suggest alternatives.
/// The whole list is replaced when a newer one ships with the app.
/// </summary>
public sealed class CatalogDrug
{
    public const int NameMaxLength = 200;
    public const int ActiveIngredientMaxLength = 400;
    public const int ManufacturerMaxLength = 150;
    public const int DrugClassMaxLength = 320;
    public const int RouteMaxLength = 20;

    public CatalogDrug(int id, string nameEn, string nameAr, string? activeIngredient, string? manufacturer, string? drugClass, string? route, decimal? price)
    {
        Id = id;
        NameEn = nameEn;
        NameAr = nameAr;
        ActiveIngredient = activeIngredient;
        Manufacturer = manufacturer;
        DrugClass = drugClass;
        Route = route;
        Price = price;
    }

    public int Id { get; private set; }

    public string NameEn { get; private set; }

    /// <summary>An Arabic spelling of the trade name, good for searching; not always the registered Arabic mark.</summary>
    public string NameAr { get; private set; }

    public string? ActiveIngredient { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? DrugClass { get; private set; }

    /// <summary>ORAL.SOLID, ORAL.LIQUID, INJECTION, TOPICAL, ...</summary>
    public string? Route { get; private set; }

    /// <summary>Consumer price of one pack in EGP when the list was published.</summary>
    public decimal? Price { get; private set; }
}
