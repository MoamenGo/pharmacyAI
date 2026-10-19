using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Application.Customers.Commands.CreateCustomer;
using PharmaAI.Application.Returns;
using PharmaAI.Application.Returns.Commands.CreateReturn;
using PharmaAI.Application.Returns.Queries.ListReturns;
using PharmaAI.Application.Sales;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Sales.Queries.GetSale;
using PharmaAI.Application.Sales.Queries.ListSales;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Returns;

public sealed class ReturnHandlerTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);

    private readonly SqliteTestDatabase _database = new();

    /// <summary>Panadol (EGP 45 a box of 3 strips) with 3 boxes in stock.</summary>
    private async Task<Guid> SeedDrugAsync()
    {
        await using var db = _database.CreateContext();
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), 9, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug.Id;
    }

    private async Task<SaleDto> SellAsync(CreateSaleCommand command)
    {
        await using var db = _database.CreateContext();
        return await new CreateSaleCommandHandler(db, Clock, Calendar).Handle(command, CancellationToken.None);
    }

    private async Task<ReturnDto> ReturnAsync(SaleDto sale, int quantity, Abstractions.IPharmaDbContext? db = null)
    {
        await using var own = _database.CreateContext();
        return await new CreateReturnCommandHandler(db ?? own, Clock, Calendar)
            .Handle(new CreateReturnCommand(sale.Id, [new(sale.Lines.Single().Id, quantity)], "منتهي"), CancellationToken.None);
    }

    private async Task<int> StockAsync(Guid drugId)
    {
        await using var db = _database.CreateContext();
        return (await db.StockBatches.Where(b => b.DrugId == drugId).ToListAsync()).Sum(b => b.QuantityInSubUnits);
    }

    [Fact]
    public async Task A_return_restocks_refunds_and_shows_on_the_sale_and_the_day()
    {
        var drug = await SeedDrugAsync();
        var sale = await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.SubUnit, 3)], 4500));

        var back = await ReturnAsync(sale, 2);

        Assert.Equal((1L, 3000L, 3000L, 0L), (back.Number, back.TotalPiasters, back.RefundedPiasters, back.CreditedPiasters));
        Assert.Equal(8, await StockAsync(drug));

        await using var db = _database.CreateContext();
        var line = (await new GetSaleQueryHandler(db).Handle(new GetSaleQuery(Number: sale.Number), CancellationToken.None))!.Lines.Single();
        Assert.Equal((2, 3000L), (line.ReturnedQuantity, line.ReturnedPiasters));

        var day = await new ListSalesQueryHandler(db, Calendar).Handle(new ListSalesQuery(), CancellationToken.None);
        Assert.Equal((1, 3000L, 3000L, 0L), (day.ReturnsCount, day.ReturnsPiasters, day.CashRefundsPiasters, day.CardRefundsPiasters));

        var listed = await new ListReturnsQueryHandler(db, Calendar).Handle(new ListReturnsQuery(SaleId: sale.Id), CancellationToken.None);
        Assert.Equal("منتهي", Assert.Single(listed).Reason);
    }

    [Fact]
    public async Task A_credit_return_lowers_the_customers_balance()
    {
        var drug = await SeedDrugAsync();
        await using var db = _database.CreateContext();
        var customer = await new CreateCustomerCommandHandler(db, Clock).Handle(new CreateCustomerCommand("أحمد علي", null), CancellationToken.None);

        var sale = await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 2)], 0, PaymentMethod.Credit, CustomerId: customer.Id));
        var back = await ReturnAsync(sale, 1);

        Assert.Equal((4500L, 0L), (back.CreditedPiasters, back.RefundedPiasters));
        await using var check = _database.CreateContext();
        Assert.Equal(45m, (await check.Customers.SingleAsync(c => c.Id == customer.Id)).Balance);
    }

    [Fact]
    public async Task Two_tills_returning_the_same_items_cannot_return_more_than_was_sold()
    {
        var drug = await SeedDrugAsync();
        var sale = await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.SubUnit, 3)], 4500));

        // Till A returns 2 strips; till B returns 2 of the same 3 before A saves.
        await using var a = _database.CreateContext();
        var racing = new BeforeSaveDbContext(a, async () => await ReturnAsync(sale, 2));

        var error = await Assert.ThrowsAsync<DomainException>(() => ReturnAsync(sale, 2, racing));

        Assert.Equal("return.invalid_quantity", error.Code);
        Assert.Equal(8, await StockAsync(drug));
    }

    [Fact]
    public async Task Returning_from_an_unknown_sale_is_not_found()
    {
        await using var db = _database.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateReturnCommandHandler(db, Clock, Calendar)
            .Handle(new CreateReturnCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)]), CancellationToken.None));
    }

    [Fact]
    public void Validator_needs_lines_and_a_short_reason()
    {
        var validator = new CreateReturnCommandValidator();
        Assert.False(validator.Validate(new CreateReturnCommand(Guid.NewGuid(), [])).IsValid);
        Assert.False(validator.Validate(new CreateReturnCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 0)])).IsValid);
        Assert.False(validator.Validate(new CreateReturnCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], new string('x', 201))).IsValid);
        Assert.True(validator.Validate(new CreateReturnCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)], "منتهي")).IsValid);
    }

    public void Dispose() => _database.Dispose();
}
