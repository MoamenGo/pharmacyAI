namespace PharmaAI.Application.Catalog;

/// <summary>One product from the bundled list of medicines registered in Egypt.</summary>
public sealed record CatalogDrug(
    string NameEn,
    string NameAr,
    string ScientificName,
    string Manufacturer,
    string DrugClass,
    string Route,
    decimal Price);
