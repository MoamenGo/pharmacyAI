using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Customers;
using PharmaAI.Domain.Entities;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Tests.TestSupport;

/// <summary>Runs <paramref name="beforeFirstSave"/> once, just before the first save, to simulate a racing writer.</summary>
public sealed class BeforeSaveDbContext(IPharmaDbContext inner, Func<Task> beforeFirstSave) : IPharmaDbContext
{
    private bool _fired;

    public DbSet<AppSetting> AppSettings => inner.AppSettings;

    public DbSet<Drug> Drugs => inner.Drugs;

    public DbSet<StockBatch> StockBatches => inner.StockBatches;

    public DbSet<Sale> Sales => inner.Sales;

    public DbSet<OutboxMessage> OutboxMessages => inner.OutboxMessages;

    public DbSet<Customer> Customers => inner.Customers;

    public DbSet<CustomerPayment> CustomerPayments => inner.CustomerPayments;

    public DbSet<SaleReturn> SaleReturns => inner.SaleReturns;

    public DbSet<StockAdjustment> StockAdjustments => inner.StockAdjustments;

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
