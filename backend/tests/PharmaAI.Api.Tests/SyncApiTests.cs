using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class SyncApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SyncApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db"));
            b.UseSetting("Sync:Worker", "false");
        });
        _client = _factory.CreateClient();
    }

    private static object Settings(string? secret) => new
    {
        enabled = true,
        environment = "preProduction",
        clientId = "client",
        clientSecret = secret,
        posSerial = "POS-001",
        posOsVersion = "windows",
        posModelFramework = "1",
        presharedKey = secret,
        rin = "123456789",
        companyTradeName = "صيدلية الشفاء",
        branchCode = "0",
        governate = "القاهرة",
        regionCity = "مدينة نصر",
        street = "عباس العقاد",
        buildingNumber = "12",
        activityCode = "4772",
    };

    private async Task<JsonElement> JsonAsync(string path) =>
        JsonDocument.Parse(await _client.GetStringAsync(path)).RootElement;

    [Fact]
    public async Task Eta_settings_never_return_the_secrets_and_keep_them_when_left_empty()
    {
        (await _client.PutAsJsonAsync("/api/sync/eta-settings", Settings("top-secret"))).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync("/api/sync/eta-settings", Settings(null))).EnsureSuccessStatusCode();

        var raw = await _client.GetStringAsync("/api/sync/eta-settings");
        Assert.DoesNotContain("top-secret", raw);
        var settings = JsonDocument.Parse(raw).RootElement;
        Assert.True(settings.GetProperty("hasClientSecret").GetBoolean());
        Assert.True(settings.GetProperty("hasPresharedKey").GetBoolean());
        Assert.Equal(0, settings.GetProperty("missingFields").GetArrayLength());

        var status = await JsonAsync("/api/sync/status");
        Assert.True(status.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task The_generic_settings_api_cannot_read_or_write_eta_settings()
    {
        (await _client.PutAsJsonAsync("/api/sync/eta-settings", Settings("top-secret"))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/settings/eta.settings")).StatusCode);
        var write = await _client.PutAsJsonAsync("/api/settings/eta.settings", new { value = "{}" });
        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
    }

    private async Task SellOneAsync(string barcode)
    {
        var created = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول", barcode, packUnitName = "علبة", subUnitName = "شريط", subUnitsPerPack = 3, publicPricePiasters = 4500,
        });
        var drugId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("drug").GetProperty("id").GetGuid();
        (await _client.PostAsJsonAsync($"/api/drugs/{drugId}/batches", new { batchNumber = "B1", expiryDate = "2030-12-31", packs = 1, looseSubUnits = 0 })).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/api/sales", new { items = new[] { new { drugId, unit = "pack", quantity = 1 } }, paidPiasters = 4500 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_tax_authority_link_is_optional_and_off_by_default()
    {
        await SellOneAsync("6221234567890");

        var status = await JsonAsync("/api/sync/status");

        Assert.False(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(0, status.GetProperty("pending").GetInt32());
        Assert.Equal(HttpStatusCode.Accepted, (await _client.PostAsync("/api/sync/run", null)).StatusCode);
    }

    [Fact]
    public async Task Once_switched_on_a_sale_waits_as_pending_until_the_settings_are_complete()
    {
        var incomplete = new
        {
            enabled = true, environment = "preProduction", clientId = "", clientSecret = (string?)null, posSerial = "", posOsVersion = "",
            posModelFramework = "", presharedKey = (string?)null, rin = "", companyTradeName = "", branchCode = "0", governate = "",
            regionCity = "", street = "", buildingNumber = "", activityCode = "4772",
        };
        (await _client.PutAsJsonAsync("/api/sync/eta-settings", incomplete)).EnsureSuccessStatusCode();

        await SellOneAsync("6221234567890");

        var status = await JsonAsync("/api/sync/status");
        Assert.True(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, status.GetProperty("pending").GetInt32());
        Assert.Contains("ClientId", status.GetProperty("missingFields").EnumerateArray().Select(f => f.GetString()));
    }

    [Fact]
    public async Task Retrying_an_unknown_message_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/sync/messages/{Guid.NewGuid()}/retry", null)).StatusCode);

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
