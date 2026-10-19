using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Catalog.Queries.SearchCatalog;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Infrastructure.Catalog;

namespace PharmaAI.Application.Tests.Catalog;

public sealed class CatalogTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    private async Task<IReadOnlyList<Application.Catalog.CatalogDrugDto>> SearchAsync(string search, int limit = 20)
    {
        await using var db = _database.CreateContext();
        return await new SearchCatalogQueryHandler(db).Handle(new SearchCatalogQuery(search, limit), CancellationToken.None);
    }

    private async Task ImportAsync()
    {
        await using var db = _database.CreateContext();
        await CatalogImporter.ImportIfOutdatedAsync(db);
    }

    [Fact]
    public void The_bundled_list_has_the_egyptian_market()
    {
        var drugs = CatalogImporter.ReadBundled();

        Assert.True(drugs.Count > 25_000, $"Only {drugs.Count} drugs were read.");
        Assert.Equal(drugs.Count, drugs.Select(d => d.Id).Distinct().Count());
        var panadol = Assert.Single(drugs, d => d.NameEn == "PANADOL EXTRA 24 F.C. TABS.");
        Assert.Equal(54m, panadol.Price);
        Assert.Equal("CAFFEINE+PARACETAMOL(ACETAMINOPHEN)", panadol.ActiveIngredient);
        Assert.Equal("ORAL.SOLID", panadol.Route);
        Assert.Contains("بانادول", panadol.NameAr);
    }

    [Fact]
    public async Task Importing_twice_loads_the_list_once()
    {
        await using var db = _database.CreateContext();

        var first = await CatalogImporter.ImportIfOutdatedAsync(db);
        var second = await CatalogImporter.ImportIfOutdatedAsync(db);

        Assert.True(first > 25_000);
        Assert.Equal(0, second);
        Assert.Equal(first, await db.CatalogDrugs.CountAsync());
        Assert.Equal(CatalogImporter.Version, (await db.AppSettings.SingleAsync(s => s.Key == CatalogImporter.VersionSettingKey)).Value);
    }

    [Fact]
    public async Task A_newer_bundled_list_replaces_the_old_one()
    {
        await ImportAsync();
        await using (var db = _database.CreateContext())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"AppSettings\" SET \"Value\" = 'old' WHERE \"Key\" = 'catalog.version'");
        }

        await using var check = _database.CreateContext();
        var count = await CatalogImporter.ImportIfOutdatedAsync(check);

        Assert.Equal(count, await check.CatalogDrugs.CountAsync());
    }

    [Fact]
    public async Task Every_word_must_match_and_names_starting_with_the_first_word_come_first()
    {
        await ImportAsync();

        var results = await SearchAsync("panadol extra");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Contains("EXTRA", r.NameEn));
        Assert.StartsWith("PANADOL", results[0].NameEn);
        Assert.Contains(results, r => r.NameEn == "PANADOL EXTRA 24 F.C. TABS." && r.PricePiasters == 5400);
    }

    [Fact]
    public async Task Search_finds_arabic_names_and_active_ingredients()
    {
        await ImportAsync();

        Assert.Contains(await SearchAsync("بانادول"), r => r.NameEn.StartsWith("PANADOL"));
        Assert.Contains(await SearchAsync("ibuprofen"), r => r.ActiveIngredient == "IBUPROFEN");
    }

    [Fact]
    public async Task An_empty_search_returns_nothing_and_the_limit_is_capped()
    {
        await ImportAsync();

        Assert.Empty(await SearchAsync("   "));
        Assert.Equal(SearchCatalogQuery.MaxLimit, (await SearchAsync("a", limit: 10_000)).Count);
    }

    public void Dispose() => _database.Dispose();
}
