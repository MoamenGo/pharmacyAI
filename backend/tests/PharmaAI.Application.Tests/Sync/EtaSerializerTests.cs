using System.Text.Json;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Tests.Sync;

public class EtaSerializerTests
{
    [Fact]
    public void Serializes_names_upper_cased_values_as_written_and_arrays_with_repeated_names()
    {
        const string json = """{"name":"بانادول","qty":2,"price":15.5,"flag":true,"seller":{"rin":"123"},"items":[{"code":"A"},{"code":"B"}]}""";

        Assert.Equal(
            "\"NAME\"\"بانادول\"\"QTY\"\"2\"\"PRICE\"\"15.5\"\"FLAG\"\"true\"\"SELLER\"\"RIN\"\"123\"\"ITEMS\"\"ITEMS\"\"CODE\"\"A\"\"ITEMS\"\"CODE\"\"B\"",
            EtaSerializer.Serialize(json));
    }

    [Fact]
    public void Reads_escaped_strings_by_their_value() =>
        Assert.Equal("\"NAME\"\"بانادول\"", EtaSerializer.Serialize("{\"name\":\"\\u0628\\u0627\\u0646\\u0627\\u062F\\u0648\\u0644\"}"));

    [Fact]
    public void Hashes_to_lower_case_sha256_hex() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", EtaSerializer.Hash("abc"));
}

public class EtaReceiptBuilderTests
{
    private static readonly EtaSettings Settings = new()
    {
        Enabled = true,
        Rin = "123456789",
        CompanyTradeName = "صيدلية الشفاء",
        BranchCode = "0",
        Governate = "القاهرة",
        RegionCity = "مدينة نصر",
        Street = "عباس العقاد",
        BuildingNumber = "12",
        PosSerial = "POS-001",
    };

    private static (Sale Sale, Dictionary<Guid, Drug> Drugs) StripAndBoxSale()
    {
        var drug = InventoryTestData.Panadol();
        drug.ReceiveStock("B1", new DateOnly(2027, 6, 30), 9, InventoryTestData.ReceivedAt);
        var soldAt = new DateTime(2026, 10, 18, 9, 30, 5, DateTimeKind.Utc);
        var sale = Sale.Create(42, soldAt, new DateOnly(2026, 10, 18), [new(drug, SaleUnit.Pack, 1), new(drug, SaleUnit.SubUnit, 2)], 100m);
        return (sale, new() { [drug.Id] = drug });
    }

    [Fact]
    public void The_uuid_is_the_hash_of_the_receipt_with_an_empty_uuid()
    {
        var (sale, drugs) = StripAndBoxSale();

        var receipt = EtaReceiptBuilder.Build(sale, drugs, Settings, "prev-uuid", referenceOldUuid: null);

        var unsigned = receipt.Json.Replace(receipt.Uuid, string.Empty);
        Assert.Equal(EtaSerializer.Hash(EtaSerializer.Serialize(unsigned)), receipt.Uuid);
        Assert.Matches("^[0-9a-f]{64}$", receipt.Uuid);
        Assert.Equal(receipt, EtaReceiptBuilder.Build(sale, drugs, Settings, "prev-uuid", referenceOldUuid: null));
    }

    [Fact]
    public void Writes_a_v1_2_cash_receipt_with_exempt_vat_lines()
    {
        var (sale, drugs) = StripAndBoxSale();

        var receipt = EtaReceiptBuilder.Build(sale, drugs, Settings, "prev-uuid", referenceOldUuid: null);

        using var json = JsonDocument.Parse(receipt.Json);
        var root = json.RootElement;
        var header = root.GetProperty("header");
        Assert.Equal(("2026-10-18T09:30:05Z", "42", "prev-uuid", "EGP"), (
            header.GetProperty("dateTimeIssued").GetString(),
            header.GetProperty("receiptNumber").GetString(),
            header.GetProperty("previousUUID").GetString(),
            header.GetProperty("currency").GetString()));
        Assert.False(header.TryGetProperty("referenceOldUUID", out _));
        Assert.Equal("s", root.GetProperty("documentType").GetProperty("receiptType").GetString());
        Assert.Equal("1.2", root.GetProperty("documentType").GetProperty("typeVersion").GetString());
        Assert.Equal("POS-001", root.GetProperty("seller").GetProperty("deviceSerialNumber").GetString());
        Assert.Equal("4772", root.GetProperty("seller").GetProperty("activityCode").GetString());

        var items = root.GetProperty("itemData").EnumerateArray().ToList();
        Assert.Equal(["BOX", "EA"], items.Select(i => i.GetProperty("unitType").GetString()));
        Assert.Equal(["45", "15"], items.Select(i => i.GetProperty("unitPrice").GetRawText()));
        Assert.Equal(["45", "30"], items.Select(i => i.GetProperty("total").GetRawText()));
        Assert.All(items, i => Assert.Equal(("GS1", "6221234567890"), (i.GetProperty("itemType").GetString(), i.GetProperty("itemCode").GetString())));
        var tax = items[0].GetProperty("taxableItems")[0];
        Assert.Equal(("T1", "V003", "0"), (tax.GetProperty("taxType").GetString(), tax.GetProperty("subType").GetString(), tax.GetProperty("amount").GetRawText()));

        Assert.Equal("75", root.GetProperty("totalAmount").GetRawText());
        Assert.Equal("C", root.GetProperty("paymentMethod").GetString());
        Assert.Contains("صيدلية الشفاء", receipt.Json);
    }

    [Fact]
    public void A_correction_references_the_receipt_it_replaces_and_gets_a_new_uuid()
    {
        var (sale, drugs) = StripAndBoxSale();
        var original = EtaReceiptBuilder.Build(sale, drugs, Settings, "prev-uuid", referenceOldUuid: null);

        var correction = EtaReceiptBuilder.Build(sale, drugs, Settings, "prev-uuid", referenceOldUuid: original.Uuid);

        using var json = JsonDocument.Parse(correction.Json);
        Assert.Equal(original.Uuid, json.RootElement.GetProperty("header").GetProperty("referenceOldUUID").GetString());
        Assert.NotEqual(original.Uuid, correction.Uuid);
    }
}
