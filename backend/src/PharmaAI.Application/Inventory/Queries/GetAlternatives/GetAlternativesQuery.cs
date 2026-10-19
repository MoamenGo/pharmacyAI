using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Catalog;
using PharmaAI.Application.Catalog.SearchCatalog;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Application.Inventory.Queries.GetAlternatives;

/// <summary>
/// Drugs with exactly the same active ingredient(s) as <paramref name="DrugId"/>: the ones on the
/// shelf (sellable first) and, for reference, the market list's products cheapest first.
/// </summary>
public sealed record GetAlternativesQuery(Guid DrugId) : IRequest<AlternativesDto>;

public sealed record AlternativesDto(string? ActiveIngredient, IReadOnlyList<DrugSummaryDto> InStock, IReadOnlyList<CatalogDrugDto> InMarket);

public sealed class GetAlternativesQueryHandler(IPharmaDbContext db, DrugCatalog catalog, PharmacyCalendar calendar)
    : IRequestHandler<GetAlternativesQuery, AlternativesDto>
{
    public const int MaxInMarket = 20;

    public async Task<AlternativesDto> Handle(GetAlternativesQuery request, CancellationToken cancellationToken)
    {
        var ingredient = await db.Drugs.AsNoTracking()
            .Where(d => d.Id == request.DrugId)
            .Select(d => new { d.ActiveIngredient })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Drug), request.DrugId);

        if (ingredient.ActiveIngredient is null)
        {
            return new AlternativesDto(null, [], []);
        }

        // SQLite's upper() only folds ASCII, which is what the ingredient names are written in.
        var key = ingredient.ActiveIngredient.ToUpperInvariant();
        var today = calendar.Today;

        var stocked = await db.Drugs.AsNoTracking()
            .Include(d => d.Batches.Where(b => b.QuantityInSubUnits > 0))
            .Where(d => d.Id != request.DrugId && d.ActiveIngredient != null && d.ActiveIngredient.ToUpper() == key)
            .ToListAsync(cancellationToken);

        return new AlternativesDto(
            ingredient.ActiveIngredient,
            stocked.Select(d => d.ToSummary(today))
                .OrderByDescending(d => d.SellableQuantityInSubUnits > 0)
                .ThenBy(d => d.PublicPricePiasters)
                .ToList(),
            catalog.WithIngredient(ingredient.ActiveIngredient, MaxInMarket).Select(c => c.ToDto()).ToList());
    }
}
