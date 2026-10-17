using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PharmaAI.Infrastructure.Persistence;

/// <summary>
/// Applies the pragmas every connection needs: WAL so POS writes never block readers,
/// NORMAL sync (safe with WAL, much faster than FULL), enforced foreign keys,
/// and a busy timeout so background sync never fails a sale with SQLITE_BUSY.
/// </summary>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Pragmas =
        "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
