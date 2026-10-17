namespace PharmaAI.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Full path of the SQLite file. When empty, defaults to
    /// %LOCALAPPDATA%/PharmaAI/pharmaai.db (or ~/.local/share/PharmaAI on Linux).
    /// </summary>
    public string? Path { get; set; }

    public string ResolvePath() =>
        string.IsNullOrWhiteSpace(Path)
            ? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PharmaAI",
                "pharmaai.db")
            : Path;
}
