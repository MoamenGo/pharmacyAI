using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

/// <summary>Boots the real API against a throwaway SQLite file on disk (WAL needs a file, not :memory:).</summary>
public sealed class ApiTests : IDisposable
{
    private sealed record HealthResponse(string Status, string JournalMode);

    private sealed record SettingResponse(string Key, string Value);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests()
    {
        var databasePath = Path.Combine(_directory, "test.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("Database:Path", databasePath));
    }

    [Fact]
    public async Task Health_reports_database_in_wal_mode()
    {
        var health = await _factory.CreateClient().GetFromJsonAsync<HealthResponse>("/api/health");

        Assert.Equal("wal", health!.JournalMode);
        Assert.Equal("ok", health.Status);
    }

    [Fact]
    public async Task Setting_round_trips_arabic_text()
    {
        var client = _factory.CreateClient();

        var put = await client.PutAsJsonAsync("/api/settings/pharmacy.name", new { value = "صيدلية الشفاء" });
        put.EnsureSuccessStatusCode();

        var setting = await client.GetFromJsonAsync<SettingResponse>("/api/settings/pharmacy.name");
        Assert.Equal("صيدلية الشفاء", setting!.Value);
    }

    [Fact]
    public async Task Unknown_setting_returns_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/settings/does.not.exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_setting_key_returns_400_problem_details()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/settings/BAD_KEY", new { value = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"key\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cors_allows_the_tauri_origin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "http://tauri.localhost");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal("http://tauri.localhost", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
