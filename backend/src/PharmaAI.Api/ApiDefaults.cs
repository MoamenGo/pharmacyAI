namespace PharmaAI.Api;

/// <summary>
/// Built-in configuration so the API runs correctly as a single-file Tauri sidecar,
/// where appsettings.json is not shipped. appsettings, environment variables and
/// command-line arguments (e.g. --Database:Path=...) all override these values.
/// </summary>
internal static class ApiDefaults
{
    public const string Url = "http://127.0.0.1:5197";

    public static readonly IReadOnlyDictionary<string, string?> Values = new Dictionary<string, string?>
    {
        ["Urls"] = Url,
        ["Cors:AllowedOrigins:0"] = "tauri://localhost",
        ["Cors:AllowedOrigins:1"] = "http://tauri.localhost",
        ["Cors:AllowedOrigins:2"] = "https://tauri.localhost",
        ["Cors:AllowedOrigins:3"] = "http://localhost:3000",
    };
}
