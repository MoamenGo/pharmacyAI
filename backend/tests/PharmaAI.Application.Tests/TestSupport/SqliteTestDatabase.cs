using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Infrastructure.Persistence;

namespace PharmaAI.Application.Tests.TestSupport;

/// <summary>In-memory SQLite (real SQL semantics, unlike the EF in-memory provider) kept alive for one test.</summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.Migrate();
    }

    public PharmaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PharmaDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
