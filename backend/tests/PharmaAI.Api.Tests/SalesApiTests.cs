using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class SalesApiTests : IDisposable
{
    private sealed record Created(Drug Drug);

    private sealed record Drug(Guid Id, int SellableQuantityInSubUnits);

    private sealed record Line(int LineNumber, string DrugNameAr, string Unit, string UnitName, int Quantity, long LineTotalPiasters);

    private sealed record Sale(Guid Id, long Number, long TotalPiasters, long PaidPiasters, long ChangePiasters, List<Line> Lines);

    private sealed record Day(int Count, long TotalPiasters);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SalesApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db")));
        _client = _factory.CreateClient();
    }

    /// <summary>A box of 3 strips at EGP 45 with <paramref name="boxes"/> boxes in stock.</summary>
    private async Task<Guid> StockedDrugAsync(string barcode = "6221234567890", int boxes = 2)
    {
        var response = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول اكسترا", barcode, packUnitName = "علبة", subUnitName = "شريط", subUnitsPerPack = 3, publicPricePiasters = 4500,
        });
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Drug.Id;
        (await _client.PostAsJsonAsync($"/api/drugs/{id}/batches", new { batchNumber = "B1", expiryDate = "2030-12-31", packs = boxes, looseSubUnits = 0 }))
            .EnsureSuccessStatusCode();
        return id;
    }

    private Task<HttpResponseMessage> SellAsync(long paidPiasters, params object[] items) =>
        _client.PostAsJsonAsync("/api/sales", new { items, paidPiasters });

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Selling_a_box_and_a_strip_returns_the_receipt_and_takes_stock()
    {
        var id = await StockedDrugAsync();

        var response = await SellAsync(10_000, new { drugId = id, unit = "pack", quantity = 1 }, new { drugId = id, unit = "subUnit", quantity = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<Sale>())!;
        Assert.Equal((1L, 6000L, 4000L), (sale.Number, sale.TotalPiasters, sale.ChangePiasters));
        Assert.Equal([("pack", "علبة"), ("subUnit", "شريط")], sale.Lines.Select(l => (l.Unit, l.UnitName)));

        var drug = (await _client.GetFromJsonAsync<Created>($"/api/drugs/{id}"))!.Drug;
        Assert.Equal(2, drug.SellableQuantityInSubUnits);

        Assert.Equal(sale.Id, (await _client.GetFromJsonAsync<Sale>(response.Headers.Location))!.Id);
        Assert.Equal((1, 6000L), (await _client.GetFromJsonAsync<Day>("/api/sales"))! is { } day ? (day.Count, day.TotalPiasters) : default);
    }

    [Fact]
    public async Task Overselling_is_409_naming_the_drug_and_saves_nothing()
    {
        var plenty = await StockedDrugAsync();
        var scarce = await StockedDrugAsync("6220000000001", boxes: 1);

        var response = await SellAsync(100_000, new { drugId = plenty, unit = "pack", quantity = 1 }, new { drugId = scarce, unit = "pack", quantity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("stock.insufficient", problem.GetProperty("code").GetString());
        Assert.Equal(scarce, problem.GetProperty("drugId").GetGuid());
        Assert.Equal(3, problem.GetProperty("availableInSubUnits").GetInt32());
        Assert.Equal(6, (await _client.GetFromJsonAsync<Created>($"/api/drugs/{plenty}"))!.Drug.SellableQuantityInSubUnits);
        Assert.Equal(0, (await _client.GetFromJsonAsync<Day>("/api/sales"))!.Count);
    }

    [Fact]
    public async Task Paying_too_little_is_409_with_code()
    {
        var id = await StockedDrugAsync();

        var response = await SellAsync(4000, new { drugId = id, unit = "pack", quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("sale.insufficient_payment", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_empty_sale_is_400_with_code()
    {
        var response = await SellAsync(0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("sale.empty", (await ProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unknown_unit_is_400()
    {
        var id = await StockedDrugAsync();

        var response = await SellAsync(4500, new { drugId = id, unit = "box", quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_sale_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/sales/{Guid.NewGuid()}")).StatusCode);

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
