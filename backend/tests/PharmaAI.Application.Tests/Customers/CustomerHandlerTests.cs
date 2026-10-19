using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Application.Customers;
using PharmaAI.Application.Customers.Commands.CreateCustomer;
using PharmaAI.Application.Customers.Commands.RecordCustomerPayment;
using PharmaAI.Application.Customers.Queries.GetCustomer;
using PharmaAI.Application.Customers.Queries.ListCustomers;
using PharmaAI.Application.Sales;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Sales.Queries.ListSales;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Common;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Customers;

public sealed class CustomerHandlerTests : IDisposable
{
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private static readonly PharmacyCalendar Calendar = new(Clock);

    private readonly SqliteTestDatabase _database = new();

    /// <summary>Panadol (EGP 45 a box) with 3 boxes in stock.</summary>
    private async Task<Guid> SeedDrugAsync()
    {
        await using var db = _database.CreateContext();
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), 9, InventoryTestData.ReceivedAt);
        db.Drugs.Add(drug);
        await db.SaveChangesAsync();
        return drug.Id;
    }

    private async Task<CustomerDto> CreateCustomerAsync(string name = "أحمد علي", string? phone = "01001234567")
    {
        await using var db = _database.CreateContext();
        return await new CreateCustomerCommandHandler(db, Clock).Handle(new CreateCustomerCommand(name, phone), CancellationToken.None);
    }

    private Task<SaleDto> SellAsync(CreateSaleCommand command) => SellWithAsync(_database.CreateContext(), command);

    private static async Task<SaleDto> SellWithAsync(IPharmaDbContext db, CreateSaleCommand command) =>
        await new CreateSaleCommandHandler(db, Clock, Calendar).Handle(command, CancellationToken.None);

    private async Task<CustomerDto> PayAsync(Guid customerId, long piasters, string? note = null)
    {
        await using var db = _database.CreateContext();
        return await new RecordCustomerPaymentCommandHandler(db, Clock, Calendar)
            .Handle(new RecordCustomerPaymentCommand(customerId, piasters, note), CancellationToken.None);
    }

    private async Task<CustomerDetailsDto> GetAsync(Guid id)
    {
        await using var db = _database.CreateContext();
        return (await new GetCustomerQueryHandler(db).Handle(new GetCustomerQuery(id), CancellationToken.None))!;
    }

    [Fact]
    public async Task A_credit_sale_and_a_payment_show_on_the_customers_account()
    {
        var drug = await SeedDrugAsync();
        var customer = await CreateCustomerAsync();

        var sale = await SellAsync(new CreateSaleCommand(
            [new(drug, SaleUnit.Pack, 2)], 2000, PaymentMethod.Credit, DiscountPiasters: 500, CustomerId: customer.Id));
        await PayAsync(customer.Id, 3000, "دفعة");

        Assert.Equal((8500L, 2000L, 6500L, "أحمد علي"), (sale.TotalPiasters, sale.PaidPiasters, sale.DuePiasters, sale.CustomerName));
        var account = await GetAsync(customer.Id);
        Assert.Equal(3500L, account.Customer.BalancePiasters);
        Assert.Equal(
            [(CustomerEntryKind.Sale, 6500L, (long?)sale.Number), (CustomerEntryKind.Payment, 3000L, null)],
            account.Entries.OrderBy(e => e.Kind).Select(e => (e.Kind, e.AmountPiasters, e.SaleNumber)));
    }

    [Fact]
    public async Task A_payment_above_the_balance_is_refused_and_changes_nothing()
    {
        var customer = await CreateCustomerAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => PayAsync(customer.Id, 1));

        Assert.Equal("customer.overpayment", ex.Code);
        Assert.Empty((await GetAsync(customer.Id)).Entries);
    }

    [Fact]
    public async Task A_payment_and_a_credit_sale_at_the_same_time_both_count()
    {
        var drug = await SeedDrugAsync();
        var customer = await CreateCustomerAsync();
        await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 0, PaymentMethod.Credit, CustomerId: customer.Id));

        // One till takes a payment; another sells on the same account before the payment is saved.
        await using var a = _database.CreateContext();
        var interceptingDb = new BeforeSaveDbContext(a, async () =>
        {
            await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 2)], 0, PaymentMethod.Credit, CustomerId: customer.Id));
        });
        await new RecordCustomerPaymentCommandHandler(interceptingDb, Clock, Calendar)
            .Handle(new RecordCustomerPaymentCommand(customer.Id, 1000, null), CancellationToken.None);

        Assert.Equal(12500L, (await GetAsync(customer.Id)).Customer.BalancePiasters);
    }

    [Fact]
    public async Task A_credit_sale_for_an_unknown_customer_is_not_found()
    {
        var drug = await SeedDrugAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 0, PaymentMethod.Credit, CustomerId: Guid.NewGuid())));
    }

    [Fact]
    public async Task ListCustomers_searches_name_and_phone_and_can_keep_only_those_who_owe()
    {
        var drug = await SeedDrugAsync();
        var ahmed = await CreateCustomerAsync("أحمد علي", "01001234567");
        await CreateCustomerAsync("منى حسن", "01112223334");
        await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 0, PaymentMethod.Credit, CustomerId: ahmed.Id));

        await using var db = _database.CreateContext();
        var handler = new ListCustomersQueryHandler(db);
        Assert.Equal(["منى حسن"], (await handler.Handle(new ListCustomersQuery("0111"), CancellationToken.None)).Select(c => c.Name));
        Assert.Equal(["أحمد علي"], (await handler.Handle(new ListCustomersQuery("أحمد"), CancellationToken.None)).Select(c => c.Name));
        Assert.Equal(["أحمد علي"], (await handler.Handle(new ListCustomersQuery(null, OwingOnly: true), CancellationToken.None)).Select(c => c.Name));
        Assert.Equal(2, (await handler.Handle(new ListCustomersQuery(null), CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_phone_number_belongs_to_one_customer()
    {
        await CreateCustomerAsync(phone: "01001234567");

        await using var db = _database.CreateContext();
        var result = await new CreateCustomerCommandValidator(db).ValidateAsync(new CreateCustomerCommand("آخر", " 01001234567 "));

        Assert.Equal("customer.phone_taken", Assert.Single(result.Errors).ErrorCode);
        Assert.True((await new CreateCustomerCommandValidator(db).ValidateAsync(new CreateCustomerCommand("بدون تليفون", null))).IsValid);
    }

    [Fact]
    public async Task The_day_splits_takings_by_payment_method()
    {
        var drug = await SeedDrugAsync();
        var customer = await CreateCustomerAsync();
        await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 5000, DiscountPiasters: 500));
        await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 0, PaymentMethod.Card));
        await SellAsync(new CreateSaleCommand([new(drug, SaleUnit.Pack, 1)], 1000, PaymentMethod.Credit, CustomerId: customer.Id));
        await PayAsync(customer.Id, 500);

        await using var db = _database.CreateContext();
        var day = await new ListSalesQueryHandler(db, Calendar).Handle(new ListSalesQuery(), CancellationToken.None);

        Assert.Equal((3, 13000L, 500L), (day.Count, day.TotalPiasters, day.DiscountPiasters));
        Assert.Equal((5000L, 4500L, 3500L, 500L), (day.CashPiasters, day.CardPiasters, day.CreditPiasters, day.CollectedPiasters));
        Assert.Equal([PaymentMethod.Credit, PaymentMethod.Card, PaymentMethod.Cash], day.Sales.Select(s => s.PaymentMethod));
    }

    public void Dispose() => _database.Dispose();
}
