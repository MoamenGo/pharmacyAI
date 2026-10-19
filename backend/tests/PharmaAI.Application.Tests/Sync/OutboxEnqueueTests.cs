using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PharmaAI.Application.Common;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;
using PharmaAI.Domain.Sync;
using PharmaAI.Infrastructure.Persistence;

namespace PharmaAI.Application.Tests.Sync;

public sealed class OutboxEnqueueTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);

    private readonly SqliteTestDatabase _database = new();

    private async Task<Guid> SeedDrugAsync(int boxes = 2)
    {
        await using var db = _database.CreateContext();
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), boxes * 3, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug.Id;
    }

    private async Task SellAsync(Guid drugId, int boxes = 1)
    {
        await using var db = _database.CreateContext();
        await new CreateSaleCommandHandler(db, Clock, Calendar).Handle(
            new CreateSaleCommand([new CreateSaleItem(drugId, SaleUnit.Pack, boxes)], 100_000), CancellationToken.None);
    }

    [Fact]
    public async Task Every_sale_is_queued_for_the_tax_authority_in_sale_order()
    {
        var id = await SeedDrugAsync();

        await SellAsync(id);
        await SellAsync(id);

        await using var db = _database.CreateContext();
        var sales = await db.Sales.OrderBy(s => s.Number).ToListAsync();
        var messages = await db.OutboxMessages.OrderBy(m => m.Sequence).ToListAsync();
        Assert.Equal(sales.Select(s => (s.Id, s.Number)), messages.Select(m => (m.SubjectId, m.Sequence)));
        Assert.All(messages, m => Assert.Equal((OutboxMessage.EtaReceiptKind, OutboxStatus.Pending), (m.Kind, m.Status)));
    }

    [Fact]
    public async Task A_failed_sale_queues_nothing()
    {
        var id = await SeedDrugAsync(boxes: 1);

        await Assert.ThrowsAsync<InsufficientStockException>(() => SellAsync(id, boxes: 2));

        await using var db = _database.CreateContext();
        Assert.Empty(db.OutboxMessages);
    }

    [Fact]
    public void Migrating_queues_sales_made_before_the_outbox_existed()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new PharmaDbContext(new DbContextOptionsBuilder<PharmaDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        migrator.Migrate("AddSales");

        // Written as SQL: today's model has columns the AddSales schema does not.
        var id = Guid.NewGuid();
        var soldAt = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        db.Database.ExecuteSql($"""
            INSERT INTO "Sales" ("Id", "Number", "SoldAtUtc", "BusinessDate", "Total", "Paid")
            VALUES ({id}, 7, {soldAt}, {new DateOnly(2026, 10, 17)}, '45.0', '50.0')
            """);

        migrator.Migrate();

        var message = Assert.Single(db.OutboxMessages.AsNoTracking());
        Assert.Equal((id, 7L, OutboxStatus.Pending, soldAt), (message.SubjectId, message.Sequence, message.Status, message.NextAttemptAtUtc));
        Assert.NotEqual(Guid.Empty, message.Id);

        // Sales from before payment methods were cash sales without a discount.
        var sale = db.Sales.AsNoTracking().Single();
        Assert.Equal((PaymentMethod.Cash, 0m, 45m, 5m), (sale.PaymentMethod, sale.Discount, sale.Total, sale.Change));
    }

    public void Dispose() => _database.Dispose();
}
