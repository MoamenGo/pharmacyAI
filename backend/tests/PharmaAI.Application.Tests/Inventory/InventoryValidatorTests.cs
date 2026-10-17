using PharmaAI.Application.Common;
using PharmaAI.Application.Inventory.Commands.CreateDrug;
using PharmaAI.Application.Inventory.Commands.DeductStock;
using PharmaAI.Application.Inventory.Commands.ReceiveStock;
using PharmaAI.Application.Inventory.Queries.GetDrug;
using PharmaAI.Application.Tests.TestSupport;

namespace PharmaAI.Application.Tests.Inventory;

public sealed class InventoryValidatorTests : IDisposable
{
    private static readonly PharmacyCalendar Calendar =
        new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero)));

    private readonly SqliteTestDatabase _database = new();

    private static CreateDrugCommand ValidDrug(string barcode = "6221234567890") =>
        new("بانادول", "Panadol", barcode, "علبة", "شريط", 3, 4500);

    private static ReceiveStockCommand ValidReceipt() =>
        new(Guid.NewGuid(), "B1", new DateOnly(2027, 1, 31), 2, 1);

    [Fact]
    public async Task CreateDrug_accepts_a_valid_drug()
    {
        await using var db = _database.CreateContext();

        Assert.True((await new CreateDrugCommandValidator(db).ValidateAsync(ValidDrug())).IsValid);
    }

    [Fact]
    public async Task CreateDrug_rejects_a_barcode_already_in_use()
    {
        await using (var seed = _database.CreateContext())
        {
            seed.Drugs.Add(InventoryTestData.Panadol("6221234567890"));
            await seed.SaveChangesAsync();
        }

        await using var db = _database.CreateContext();
        var result = await new CreateDrugCommandValidator(db).ValidateAsync(ValidDrug("6221234567890"));

        Assert.Contains(result.Errors, e => e.ErrorCode == "drug.barcode_taken");
    }

    [Theory]
    [InlineData("", "علبة", "شريط", 3, 4500L)]
    [InlineData("بانادول", "", "شريط", 3, 4500L)]
    [InlineData("بانادول", "علبة", "", 3, 4500L)]
    [InlineData("بانادول", "علبة", "شريط", 0, 4500L)]
    [InlineData("بانادول", "علبة", "شريط", 3, 0L)]
    [InlineData("بانادول", "علبة", "شريط", 3, CreateDrugCommandValidator.MaxPricePiasters + 1)]
    public async Task CreateDrug_rejects_missing_or_out_of_range_fields(string nameAr, string pack, string sub, int perPack, long price)
    {
        await using var db = _database.CreateContext();

        var result = await new CreateDrugCommandValidator(db)
            .ValidateAsync(new CreateDrugCommand(nameAr, null, "123", pack, sub, perPack, price));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("12 34")]
    [InlineData("abc$")]
    public async Task CreateDrug_rejects_barcodes_with_odd_characters(string barcode)
    {
        await using var db = _database.CreateContext();

        Assert.False((await new CreateDrugCommandValidator(db).ValidateAsync(ValidDrug(barcode))).IsValid);
    }

    [Fact]
    public async Task CreateDrug_rejects_a_missing_barcode_without_querying()
    {
        await using var db = _database.CreateContext();

        var result = await new CreateDrugCommandValidator(db).ValidateAsync(ValidDrug(null!));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateDrugCommand.Barcode));
    }

    [Fact]
    public void ReceiveStock_accepts_a_valid_receipt() =>
        Assert.True(new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt()).IsValid);

    [Fact]
    public void ReceiveStock_rejects_stock_that_already_expired()
    {
        var result = new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt() with { ExpiryDate = new DateOnly(2026, 10, 16) });

        Assert.Contains(result.Errors, e => e.ErrorCode == "stock.already_expired");
    }

    [Fact]
    public void ReceiveStock_accepts_stock_expiring_today() =>
        Assert.True(new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt() with { ExpiryDate = Calendar.Today }).IsValid);

    [Fact]
    public void ReceiveStock_rejects_an_empty_receipt() =>
        Assert.False(new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt() with { Packs = 0, LooseSubUnits = 0 }).IsValid);

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(ReceiveStockCommandValidator.MaxPacksPerReceipt + 1, 0)]
    public void ReceiveStock_rejects_out_of_range_quantities(int packs, int loose) =>
        Assert.False(new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt() with { Packs = packs, LooseSubUnits = loose }).IsValid);

    [Fact]
    public void ReceiveStock_requires_a_batch_number() =>
        Assert.False(new ReceiveStockCommandValidator(Calendar).Validate(ValidReceipt() with { BatchNumber = " " }).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(DeductStockCommandValidator.MaxQuantityInSubUnits + 1)]
    public void DeductStock_rejects_out_of_range_quantity(int quantity) =>
        Assert.False(new DeductStockCommandValidator().Validate(new DeductStockCommand(Guid.NewGuid(), quantity)).IsValid);

    [Fact]
    public void DeductStock_requires_a_drug() =>
        Assert.False(new DeductStockCommandValidator().Validate(new DeductStockCommand(Guid.Empty, 1)).IsValid);

    [Fact]
    public void GetDrug_requires_exactly_one_lookup_key()
    {
        var validator = new GetDrugQueryValidator();

        Assert.False(validator.Validate(new GetDrugQuery()).IsValid);
        Assert.False(validator.Validate(new GetDrugQuery(Guid.NewGuid(), "123")).IsValid);
        Assert.True(validator.Validate(new GetDrugQuery(Barcode: "123")).IsValid);
    }

    public void Dispose() => _database.Dispose();
}
