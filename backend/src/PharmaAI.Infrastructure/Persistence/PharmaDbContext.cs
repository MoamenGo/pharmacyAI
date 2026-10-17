using Microsoft.EntityFrameworkCore;
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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmaDbContext).Assembly);
}
