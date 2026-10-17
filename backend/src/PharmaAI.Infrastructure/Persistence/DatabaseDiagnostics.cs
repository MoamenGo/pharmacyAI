using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;

namespace PharmaAI.Infrastructure.Persistence;

internal sealed class DatabaseDiagnostics(PharmaDbContext db) : IDatabaseDiagnostics
{
    public async Task<string> GetJournalModeAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToString(result) ?? string.Empty;
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
