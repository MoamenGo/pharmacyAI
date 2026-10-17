namespace PharmaAI.Application.Abstractions;

public interface IDatabaseDiagnostics
{
    /// <summary>Returns the SQLite journal mode of the live connection (expected: "wal").</summary>
    Task<string> GetJournalModeAsync(CancellationToken cancellationToken);
}
