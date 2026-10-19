using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PharmaAI.Domain.Common;

namespace PharmaAI.Infrastructure.Backup;

public sealed record BackupInfo(string Name, long SizeBytes, DateTime CreatedAtUtc);

/// <summary>
/// Copies of the database kept in a "backups" folder next to it. A copy is a consistent snapshot taken with
/// <c>VACUUM INTO</c>, so it is safe while the tills keep selling. Only the newest <see cref="Keep"/> are kept.
/// </summary>
public sealed partial class DatabaseBackups(string databasePath, TimeProvider clock)
{
    public const int Keep = 30;

    public string Folder { get; } = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "backups");

    public async Task<BackupInfo> CreateAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);
        var stamp = clock.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
        var name = $"pharmaai-{stamp}.db";
        for (var n = 2; File.Exists(Path.Combine(Folder, name)); n++)
        {
            name = $"pharmaai-{stamp}-{n}.db";
        }

        var path = Path.Combine(Folder, name);
        await using (var connection = Open(databasePath, SqliteOpenMode.ReadOnly))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $path;";
            command.Parameters.AddWithValue("$path", path);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var old in List().Skip(Keep))
        {
            File.Delete(Path.Combine(Folder, old.Name));
        }

        return Info(new FileInfo(path));
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<BackupInfo> List() =>
        Directory.Exists(Folder)
            ? new DirectoryInfo(Folder).GetFiles("pharmaai-*.db")
                .Where(f => NamePattern().IsMatch(f.Name))
                .Select(Info)
                .OrderByDescending(b => Path.GetFileNameWithoutExtension(b.Name), StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>The full path of a backup, or null when the name is not one of ours (never a path outside the folder).</summary>
    public string? PathOf(string name)
    {
        if (!NamePattern().IsMatch(name))
        {
            return null;
        }

        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Replaces the live database with a backup. The current data is backed up first, so a restore can itself be undone.
    /// The caller migrates the database afterwards, in case the backup came from an older version.
    /// </summary>
    public async Task<BackupInfo> RestoreAsync(string name, CancellationToken cancellationToken)
    {
        var source = PathOf(name) ?? throw new FileNotFoundException(name);

        await using (var check = Open(source, SqliteOpenMode.ReadOnly))
        {
            await using var command = check.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            string? result;
            try
            {
                result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
            }
            catch (SqliteException ex)
            {
                throw new DomainException("backup.invalid", $"{name} is not a readable database: {ex.Message}");
            }

            if (result != "ok")
            {
                throw new DomainException("backup.invalid", $"{name} failed the integrity check: {result}");
            }
        }

        var safety = await CreateAsync(cancellationToken);

        SqliteConnection.ClearAllPools();
        await using (var from = Open(source, SqliteOpenMode.ReadOnly))
        await using (var to = Open(databasePath, SqliteOpenMode.ReadWrite))
        {
            from.BackupDatabase(to);
        }

        SqliteConnection.ClearAllPools();
        return safety;
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static BackupInfo Info(FileInfo file) => new(file.Name, file.Length, file.CreationTimeUtc);

    [GeneratedRegex(@"^pharmaai-\d{8}-\d{6}(-\d+)?\.db$")]
    private static partial Regex NamePattern();
}
