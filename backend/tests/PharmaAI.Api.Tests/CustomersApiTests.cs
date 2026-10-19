using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaAI.Api.Tests;

public sealed class CustomersApiTests : IDisposable
{
    private sealed record Created(Drug Drug);

    private sealed record Drug(Guid Id);

    private sealed record Customer(Guid Id, string Name, string? Phone, long BalancePiasters);

    private sealed record Entry(string Kind, long AmountPiasters, long? SaleNumber, string? Note);

    private sealed record Account(Customer Customer, List<Entry> Entries);

    private sealed record Sale(
        long Number, string PaymentMethod, long SubtotalPiasters, long DiscountPiasters, long TotalPiasters,
        long PaidPiasters, long ChangePiasters, long DuePiasters, string? CustomerName, string? CardReference);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pharmaai-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CustomersApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Path", Path.Combine(_directory, "test.db"));
            b.UseSetting("Sync:Worker", "false");
        });
        _client = _factory.CreateClient();
    }

    /// <summary>A box of 3 strips at EGP 45 with 5 boxes in stock.</summary>
    private async Task<Guid> StockedDrugAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/drugs", new
        {
            nameAr = "بانادول اكسترا", barcode = "6221234567890", packUnitName = "علبة", subUnitName = "شريط", subUnitsPerPack = 3, publicPricePiasters = 4500,
        });
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Drug.Id;
        (await _client.PostAsJsonAsync($"/api/drugs/{id}/batches", new { batchNumber = "B1", expiryDate = "2030-12-31", packs = 5, looseSubUnits = 0 }))
            .EnsureSuccessStatusCode();
        return id;
    }

    private async Task<Customer> CreateCustomerAsync(string name = "أحمد علي", string? phone = "01001234567")
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new { name, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Customer>())!;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    [Fact]
    public async Task A_credit_sale_with_a_discount_goes_on_the_account_and_a_payment_comes_off()
    {
        var drug = await StockedDrugAsync();
        var customer = await CreateCustomerAsync();

        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            items = new[] { new { drugId = drug, unit = "pack", quantity = 2 } },
            paidPiasters = 2000,
            paymentMethod = "credit",
            discountPiasters = 1000,
            customerId = customer.Id,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = (await response.Content.ReadFromJsonAsync<Sale>())!;
        Assert.Equal(("credit", 9000L, 1000L, 8000L, 2000L, 0L, 6000L, "أحمد علي"), (
            sale.PaymentMethod, sale.SubtotalPiasters, sale.DiscountPiasters, sale.TotalPiasters,
            sale.PaidPiasters, sale.ChangePiasters, sale.DuePiasters, sale.CustomerName));

        var paid = await _client.PostAsJsonAsync($"/api/customers/{customer.Id}/payments", new { amountPiasters = 2500, note = "دفعة" });
        Assert.Equal(3500L, (await paid.Content.ReadFromJsonAsync<Customer>())!.BalancePiasters);

        var account = (await _client.GetFromJsonAsync<Account>($"/api/customers/{customer.Id}"))!;
        Assert.Equal(["payment", "sale"], account.Entries.Select(e => e.Kind).Order());
        Assert.Equal(["أحمد علي"], (await _client.GetFromJsonAsync<List<Customer>>("/api/customers?owing=true"))!.Select(c => c.Name));
    }

    [Fact]
    public async Task A_card_sale_keeps_its_reference_and_needs_no_cash()
    {
        var drug = await StockedDrugAsync();

        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            items = new[] { new { drugId = drug, unit = "pack", quantity = 1 } },
            paidPiasters = 0,
            paymentMethod = "card",
            cardReference = "4821",
        });

        var sale = (await response.Content.ReadFromJsonAsync<Sale>())!;
        Assert.Equal(("card", 4500L, 0L, "4821"), (sale.PaymentMethod, sale.PaidPiasters, sale.ChangePiasters, sale.CardReference));
    }

    [Fact]
    public async Task Mistakes_come_back_with_codes()
    {
        var drug = await StockedDrugAsync();
        var customer = await CreateCustomerAsync();

        var noCustomer = await _client.PostAsJsonAsync("/api/sales", new
        {
            items = new[] { new { drugId = drug, unit = "pack", quantity = 1 } }, paidPiasters = 0, paymentMethod = "credit",
        });
        var bigDiscount = await _client.PostAsJsonAsync("/api/sales", new
        {
            items = new[] { new { drugId = drug, unit = "pack", quantity = 1 } }, paidPiasters = 0, discountPiasters = 4501,
        });
        var samePhone = await _client.PostAsJsonAsync("/api/customers", new { name = "آخر", phone = "01001234567" });
        var overpaid = await _client.PostAsJsonAsync($"/api/customers/{customer.Id}/payments", new { amountPiasters = 100 });
        var unknown = await _client.GetAsync($"/api/customers/{Guid.NewGuid()}");

        Assert.Equal((HttpStatusCode.BadRequest, "sale.customer_required"), (noCustomer.StatusCode, await CodeAsync(noCustomer)));
        Assert.Equal((HttpStatusCode.Conflict, "sale.invalid_discount"), (bigDiscount.StatusCode, await CodeAsync(bigDiscount)));
        Assert.Equal((HttpStatusCode.BadRequest, "customer.phone_taken"), (samePhone.StatusCode, await CodeAsync(samePhone)));
        Assert.Equal((HttpStatusCode.Conflict, "customer.overpayment"), (overpaid.StatusCode, await CodeAsync(overpaid)));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
