namespace PharmaAI.Application.Settings;

public static class SettingKeys
{
    /// <summary>
    /// Settings under these prefixes hold secrets (the ETA client secret) or are owned by the app
    /// (the bundled drug list's version) and are read and written only through their own code paths,
    /// never through the generic settings API.
    /// </summary>
    private static readonly string[] ReservedPrefixes = ["eta.", "catalog."];

    public static bool IsReserved(string key) =>
        ReservedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
