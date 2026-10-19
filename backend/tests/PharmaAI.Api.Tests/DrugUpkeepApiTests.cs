using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

/// <summary>Editing drugs, write-offs, alternatives, alerts and the sales report over HTTP.</summary>
public sealed class DrugUpkeepApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DrugUpkeepApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db"));
            b.UseSetting("Sync:Worker", "false");
        });
        _client = _factory.CreateClient();
    }

    private async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private async Task<JsonElement> GetAsync(string path) => await JsonAsync(await _client.GetAsync(path));

    private async Task<Guid> CreateDrugAsync(string barcode, string ingredient, int reorderLevelInPacks = 0)
    {
        var created = await JsonAsync(await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول اكسترا", nameEn = "Panadol Extra", barcode, packUnitName = "علبة", subUnitName = "شريط",
            subUnitsPerPack = 2, publicPricePiasters = 5400, activeIngredient = ingredient, manufacturer = "GSK", reorderLevelInPacks,
        }));
        return created.GetProperty("drug").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_drug_can_be_edited_written_off_and_shows_up_in_alerts_and_the_report()
    {
        var id = await CreateDrugAsync("6222010610640", "CAFFEINE+PARACETAMOL(ACETAMINOPHEN)", reorderLevelInPacks: 5);
        var received = await JsonAsync(await _client.PostAsJsonAsync($"/api/drugs/{id}/batches", new { batchNumber = "B1", expiryDate = "2030-12-31", packs = 3, looseSubUnits = 0 }));
        var batchId = received.GetProperty("batches")[0].GetProperty("id").GetGuid();

        var edited = await JsonAsync(await _client.PutAsJsonAsync($"/api/drugs/{id}", new
        {
            nameAr = "بانادول اكسترا 24", nameEn = "Panadol Extra 24", barcode = "6222010610640", publicPricePiasters = 5800,
            activeIngredient = "CAFFEINE+PARACETAMOL(ACETAMINOPHEN)", manufacturer = "GSK", reorderLevelInPacks = 2,
        }));
        Assert.Equal(5800, edited.GetProperty("drug").GetProperty("publicPricePiasters").GetInt64());
        Assert.False(edited.GetProperty("drug").GetProperty("isShort").GetBoolean());

        var writtenOff = await JsonAsync(await _client.PostAsJsonAsync($"/api/drugs/{id}/batches/{batchId}/write-offs", new { quantityInSubUnits = 3, reason = "damaged", note = "وقعت" }));
        Assert.Equal(3, writtenOff.GetProperty("drug").GetProperty("sellableQuantityInSubUnits").GetInt32());
        Assert.True(writtenOff.GetProperty("drug").GetProperty("isShort").GetBoolean());

        var alerts = await GetAsync("/api/inventory/alerts");
        Assert.Equal(id, alerts.GetProperty("shortages")[0].GetProperty("id").GetGuid());

        var today = alerts.GetProperty("today").GetString();
        var report = await GetAsync($"/api/reports/sales?from={today}&to={today}");
        var writeOff = report.GetProperty("writeOffs")[0];
        Assert.Equal(("damaged", 3), (writeOff.GetProperty("reason").GetString(), writeOff.GetProperty("quantityInSubUnits").GetInt32()));
    }

    [Fact]
    public async Task Alternatives_include_stocked_drugs_and_the_market_list()
    {
        var first = await CreateDrugAsync("111", "IBUPROFEN");
        await CreateDrugAsync("222", "ibuprofen");

        var alternatives = await GetAsync($"/api/drugs/{first}/alternatives");

        Assert.Equal(1, alternatives.GetProperty("inStock").GetArrayLength());
        Assert.True(alternatives.GetProperty("inMarket").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Bad_requests_are_rejected_with_a_problem()
    {
        var id = await CreateDrugAsync("111", "IBUPROFEN");

        var badReport = await _client.GetAsync("/api/reports/sales?from=2026-02-01&to=2026-01-01");
        var missingBatch = await _client.PostAsJsonAsync($"/api/drugs/{id}/batches/{Guid.NewGuid()}/write-offs", new { quantityInSubUnits = 1, reason = "lost" });
        var unknownDrug = await _client.GetAsync($"/api/drugs/{Guid.NewGuid()}/alternatives");

        Assert.Equal(HttpStatusCode.BadRequest, badReport.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, missingBatch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownDrug.StatusCode);
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
