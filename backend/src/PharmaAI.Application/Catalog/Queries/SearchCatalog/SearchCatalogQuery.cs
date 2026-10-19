using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Inventory.Queries.ListDrugs;

namespace PharmaAI.Application.Catalog.Queries.SearchCatalog;

/// <summary>
/// Searches the Egyptian drug list. Every word typed must appear in the English name, the Arabic
/// name or the active ingredient, so "panadol extra" and "بانادول" both find Panadol Extra.
/// </summary>
public sealed record SearchCatalogQuery(string? Search, int Limit = SearchCatalogQuery.DefaultLimit) : IRequest<IReadOnlyList<CatalogDrugDto>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxWords = 5;
}

public sealed class SearchCatalogQueryHandler(IPharmaDbContext db) : IRequestHandler<SearchCatalogQuery, IReadOnlyList<CatalogDrugDto>>
{
    public async Task<IReadOnlyList<CatalogDrugDto>> Handle(SearchCatalogQuery request, CancellationToken cancellationToken)
    {
        var words = (request.Search ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(SearchCatalogQuery.MaxWords)
            .ToList();
        if (words.Count == 0)
        {
            return [];
        }

        var query = db.CatalogDrugs.AsNoTracking();
        foreach (var word in words)
        {
            var pattern = $"%{ListDrugsQueryHandler.EscapeLike(word)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.NameEn, pattern, "\\")
                || EF.Functions.Like(c.NameAr, pattern, "\\")
                || (c.ActiveIngredient != null && EF.Functions.Like(c.ActiveIngredient, pattern, "\\")));
        }

        // Names that start with the first word come first: typing "con" should show Concor before Diacon.
        var prefix = $"{ListDrugsQueryHandler.EscapeLike(words[0])}%";
        var drugs = await query
            .OrderBy(c => EF.Functions.Like(c.NameEn, prefix, "\\") || EF.Functions.Like(c.NameAr, prefix, "\\") ? 0 : 1)
            .ThenBy(c => c.NameEn)
            .Take(Math.Clamp(request.Limit, 1, SearchCatalogQuery.MaxLimit))
            .ToListAsync(cancellationToken);

        return drugs.Select(c => c.ToDto()).ToList();
    }
}
