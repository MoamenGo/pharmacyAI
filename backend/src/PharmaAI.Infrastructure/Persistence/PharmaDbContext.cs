using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Entities;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Infrastructure.Persistence;

public sealed class PharmaDbContext(DbContextOptions<PharmaDbContext> options) : DbContext(options), IPharmaDbContext
{
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<Drug> Drugs => Set<Drug>();

    public DbSet<StockBatch> StockBatches => Set<StockBatch>();

    public void ClearChangeTracker() => ChangeTracker.Clear();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueViolation or SqlitePrimaryKeyViolation })
        {
            throw new DuplicateKeyException(ex);
        }
    }

    private const int SqliteUniqueViolation = 2067;
    private const int SqlitePrimaryKeyViolation = 1555;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmaDbContext).Assembly);
}
