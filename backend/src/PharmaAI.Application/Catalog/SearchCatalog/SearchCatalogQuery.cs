using MediatR;
using PharmaAI.Application.Common;

namespace PharmaAI.Application.Catalog.SearchCatalog;

public sealed record CatalogDrugDto(
    string NameEn,
    string NameAr,
    string ScientificName,
    string Manufacturer,
    string Route,
    long PublicPricePiasters,
    string PackUnitName,
    string SubUnitName,
    int SubUnitsPerPack);

public static class CatalogMapping
{
    public static CatalogDrugDto ToDto(this CatalogDrug d)
    {
        var units = CatalogUnits.Suggest(d.NameEn);
        return new CatalogDrugDto(
            d.NameEn, d.NameAr, d.ScientificName, d.Manufacturer, d.Route, Piasters.FromPounds(d.Price),
            units.PackUnitName, units.SubUnitName, units.SubUnitsPerPack);
    }
}

public sealed record SearchCatalogQuery(string? Search, int Limit = SearchCatalogQuery.DefaultLimit) : IRequest<IReadOnlyList<CatalogDrugDto>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
}

public sealed class SearchCatalogQueryHandler(DrugCatalog catalog) : IRequestHandler<SearchCatalogQuery, IReadOnlyList<CatalogDrugDto>>
{
    public Task<IReadOnlyList<CatalogDrugDto>> Handle(SearchCatalogQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogDrugDto> results = catalog
            .Search(request.Search, Math.Clamp(request.Limit, 1, SearchCatalogQuery.MaxLimit))
            .Select(d => d.ToDto())
            .ToList();
        return Task.FromResult(results);
    }
}
