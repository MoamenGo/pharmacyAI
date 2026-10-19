using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class BackupApiTests : IDisposable
{
    private sealed record Backup(string Name, long SizeBytes);

    private sealed record RestoreResult(string Restored, string SafetyBackup);

    private sealed record Day(int Count);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BackupApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db"));
            b.UseSetting("Sync:Worker", "false");
            b.UseSetting("Backup:Automatic", "false");
        });
        _client = _factory.CreateClient();
    }

    private async Task<int> SalesTodayAsync() => (await _client.GetFromJsonAsync<Day>("/api/sales"))!.Count;

    [Fact]
    public async Task A_backup_can_be_downloaded_and_restored_and_the_restore_is_itself_backed_up()
    {
        var created = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول", barcode = "6221234567890", packUnitName = "علبة", subUnitName = "شريط", subUnitsPerPack = 3, publicPricePiasters = 4500,
        });
        var drugId = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("drug").GetProperty("id").GetGuid();
        (await _client.PostAsJsonAsync($"/api/drugs/{drugId}/batches", new { batchNumber = "B1", expiryDate = "2030-12-31", packs = 3, looseSubUnits = 0 })).EnsureSuccessStatusCode();
        async Task SellAsync() =>
            (await _client.PostAsJsonAsync("/api/sales", new { items = new[] { new { drugId, unit = "pack", quantity = 1 } }, paidPiasters = 4500 })).EnsureSuccessStatusCode();

        await SellAsync();
        var backup = (await (await _client.PostAsync("/api/backups", null)).Content.ReadFromJsonAsync<Backup>())!;
        Assert.Matches(@"^pharmaai-\d{8}-\d{6}(-\d+)?\.db$", backup.Name);
        Assert.True(backup.SizeBytes > 0);

        var download = await _client.GetAsync($"/api/backups/{backup.Name}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("SQLite format 3\0", System.Text.Encoding.ASCII.GetString((await download.Content.ReadAsByteArrayAsync())[..16]));

        await SellAsync();
        Assert.Equal(2, await SalesTodayAsync());

        var restore = await _client.PostAsync($"/api/backups/{backup.Name}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restored = (await restore.Content.ReadFromJsonAsync<RestoreResult>())!;
        Assert.Equal(1, await SalesTodayAsync());

        var names = (await _client.GetFromJsonAsync<List<Backup>>("/api/backups"))!.Select(b => b.Name).ToList();
        Assert.Contains(backup.Name, names);
        Assert.Contains(restored.SafetyBackup, names);

        // Undo the restore: the safety backup holds both sales.
        (await _client.PostAsync($"/api/backups/{restored.SafetyBackup}/restore", null)).EnsureSuccessStatusCode();
        Assert.Equal(2, await SalesTodayAsync());
    }

    [Theory]
    [InlineData("pharmaai-20990101-000000.db")]
    [InlineData("..%2Ftest.db")]
    [InlineData("test.db")]
    public async Task Unknown_or_foreign_names_are_404(string name)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/backups/{name}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/backups/{name}/restore", null)).StatusCode);
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
