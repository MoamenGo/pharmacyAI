namespace PharmaAI.Application.Settings;

public static class SettingKeys
{
    /// <summary>
    /// Settings under these prefixes hold secrets (the ETA client secret) and are read and written
    /// only through their own endpoints, never through the generic settings API.
    /// </summary>
    private static readonly string[] ReservedPrefixes = ["eta."];

    public static bool IsReserved(string key) =>
        ReservedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
