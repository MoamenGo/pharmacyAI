using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Entities;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.TestSupport;

/// <summary>Runs <paramref name="beforeFirstSave"/> once, just before the first save, to simulate a racing writer.</summary>
public sealed class BeforeSaveDbContext(IPharmaDbContext inner, Func<Task> beforeFirstSave) : IPharmaDbContext
{
    private bool _fired;

    public DbSet<AppSetting> AppSettings => inner.AppSettings;

    public DbSet<Drug> Drugs => inner.Drugs;

    public DbSet<StockBatch> StockBatches => inner.StockBatches;

    public DbSet<Sale> Sales => inner.Sales;

    public void ClearChangeTracker() => inner.ClearChangeTracker();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!_fired)
        {
            _fired = true;
            await beforeFirstSave();
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }
}
