using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class InventoryApiTests : IDisposable
{
    private sealed record DrugSummary(Guid Id, string NameAr, long PublicPricePiasters, long SubUnitPricePiasters, int SellableQuantityInSubUnits, DateOnly? NearestExpiry);

    private sealed record DrugDetails(DrugSummary Drug, List<Batch> Batches);

    private sealed record Batch(string BatchNumber, DateOnly ExpiryDate, int QuantityInSubUnits);

    private sealed record Allocation(string BatchNumber, int QuantityInSubUnits);

    private sealed record DeductResult(List<Allocation> Allocations, DrugSummary Drug);

    private static readonly DateOnly FarExpiry = new(2030, 12, 31);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public InventoryApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db")));
        _client = _factory.CreateClient();
    }

    private async Task<DrugDetails> CreateDrugAsync(string barcode = "6221234567890")
    {
        var response = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول اكسترا",
            nameEn = "Panadol Extra",
            barcode,
            packUnitName = "علبة",
            subUnitName = "شريط",
            subUnitsPerPack = 3,
            publicPricePiasters = 4500,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DrugDetails>())!;
    }

    private Task<HttpResponseMessage> ReceiveAsync(Guid id, string batch, DateOnly expiry, int packs, int loose = 0) =>
        _client.PostAsJsonAsync($"/api/drugs/{id}/batches", new { batchNumber = batch, expiryDate = expiry, packs, looseSubUnits = loose });

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Full_flow_create_receive_and_deduct_fefo()
    {
        var created = await CreateDrugAsync();
        Assert.Equal(4500, created.Drug.PublicPricePiasters);
        Assert.Equal(1500, created.Drug.SubUnitPricePiasters);

        (await ReceiveAsync(created.Drug.Id, "LATE", FarExpiry, packs: 2)).EnsureSuccessStatusCode();
        (await ReceiveAsync(created.Drug.Id, "EARLY", FarExpiry.AddYears(-3), packs: 1)).EnsureSuccessStatusCode();

        var deduct = await _client.PostAsJsonAsync($"/api/drugs/{created.Drug.Id}/deductions", new { quantityInSubUnits = 4 });
        deduct.EnsureSuccessStatusCode();
        var result = (await deduct.Content.ReadFromJsonAsync<DeductResult>())!;

        Assert.Equal(["EARLY", "LATE"], result.Allocations.Select(a => a.BatchNumber));
        Assert.Equal(5, result.Drug.SellableQuantityInSubUnits);

        var details = await _client.GetFromJsonAsync<DrugDetails>($"/api/drugs/{created.Drug.Id}");
        Assert.Equal("LATE", Assert.Single(details!.Batches).BatchNumber);
    }

    [Fact]
    public async Task Barcode_lookup_finds_the_drug()
    {
        await CreateDrugAsync("6229999999999");

        var details = await _client.GetFromJsonAsync<DrugDetails>("/api/drugs/by-barcode/6229999999999");

        Assert.Equal("بانادول اكسترا", details!.Drug.NameAr);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/drugs/by-barcode/000")).StatusCode);
    }

    [Fact]
    public async Task Search_by_arabic_name()
    {
        await CreateDrugAsync();

        var results = await _client.GetFromJsonAsync<List<DrugSummary>>($"/api/drugs?search={Uri.EscapeDataString("بانادول")}");

        Assert.Single(results!);
    }

    [Fact]
    public async Task Duplicate_barcode_is_400_with_code()
    {
        await CreateDrugAsync();

        var response = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "دواء آخر", barcode = "6221234567890", packUnitName = "علبة", subUnitName = "شريط", subUnitsPerPack = 1, publicPricePiasters = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("drug.barcode_taken", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Overselling_is_409_with_code_and_changes_nothing()
    {
        var created = await CreateDrugAsync();
        (await ReceiveAsync(created.Drug.Id, "B1", FarExpiry, packs: 1)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync($"/api/drugs/{created.Drug.Id}/deductions", new { quantityInSubUnits = 4 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("stock.insufficient", await ProblemCodeAsync(response));
        var details = await _client.GetFromJsonAsync<DrugDetails>($"/api/drugs/{created.Drug.Id}");
        Assert.Equal(3, details!.Drug.SellableQuantityInSubUnits);
    }

    [Fact]
    public async Task Receiving_expired_stock_is_400()
    {
        var created = await CreateDrugAsync();

        var response = await ReceiveAsync(created.Drug.Id, "OLD", new DateOnly(2020, 1, 31), packs: 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("stock.already_expired", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Unknown_drug_is_404()
    {
        var response = await ReceiveAsync(Guid.NewGuid(), "B1", FarExpiry, packs: 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
