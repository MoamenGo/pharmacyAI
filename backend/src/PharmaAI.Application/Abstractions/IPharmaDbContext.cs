using Microsoft.EntityFrameworkCore;
using PharmaAI.Domain.Entities;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Abstractions;

public interface IPharmaDbContext
{
    DbSet<AppSetting> AppSettings { get; }

    DbSet<Drug> Drugs { get; }

    DbSet<StockBatch> StockBatches { get; }

    DbSet<Sale> Sales { get; }

    /// <summary>Forgets every tracked entity so the next query reads fresh rows (used when retrying a conflict).</summary>
    void ClearChangeTracker();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
