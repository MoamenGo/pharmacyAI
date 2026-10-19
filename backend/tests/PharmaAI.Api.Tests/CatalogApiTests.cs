using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class CatalogApiTests : IDisposable
{
    private sealed record CatalogDrug(string NameEn, string NameAr, string ScientificName, long PublicPricePiasters, string SubUnitName, int SubUnitsPerPack);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CatalogApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db"));
            b.UseSetting("Sync:Worker", "false");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Bundled_catalog_finds_drugs_by_arabic_or_english_name()
    {
        var arabic = await _client.GetFromJsonAsync<List<CatalogDrug>>("/api/catalog?search=" + Uri.EscapeDataString("بانادول"));
        Assert.NotEmpty(arabic!);
        Assert.All(arabic!, d => Assert.True(d.PublicPricePiasters > 0));
        Assert.Contains(arabic!, d => d.NameEn.StartsWith("PANADOL", StringComparison.Ordinal));

        var english = await _client.GetFromJsonAsync<List<CatalogDrug>>("/api/catalog?search=augmentin&limit=5");
        Assert.InRange(english!.Count, 1, 5);
        Assert.All(english!, d => Assert.Contains("AMOXICILLIN", d.ScientificName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Empty_search_returns_nothing()
    {
        Assert.Empty((await _client.GetFromJsonAsync<List<CatalogDrug>>("/api/catalog?search="))!);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
