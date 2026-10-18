using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using PharmaAI.Domain.Inventory;
using PharmaAI.Domain.Sales;

namespace PharmaAI.Application.Sync.Eta;

public sealed record EtaReceipt(string Json, string Uuid);

/// <summary>
/// Builds an ETA e-receipt (v1.2, type "s") for a cash sale. Medicines are VAT-exempt, so every
/// line carries VAT (T1) as exempt (V003) at 0. The JSON is written once and that exact text is
/// both hashed and sent, so the UUID always matches the bytes ETA receives.
/// See https://sdk.invoicing.eta.gov.eg/documents/receipt-v1-2/.
/// </summary>
public static class EtaReceiptBuilder
{
    public const string ReceiptType = "s";
    public const string TypeVersion = "1.2";
    public const string CashPayment = "C";
    public const string PackUnitType = "BOX";
    public const string SubUnitType = "EA";
    public const string Vat = "T1";
    public const string VatExempt = "V003";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // Arabic names stay readable; the serializer reads unescaped values either way.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="previousUuid">The chained receipt's UUID, or empty for this device's first receipt.</param>
    /// <param name="referenceOldUuid">When correcting a rejected receipt, the UUID it replaces.</param>
    public static EtaReceipt Build(
        Sale sale,
        IReadOnlyDictionary<Guid, Drug> drugs,
        EtaSettings settings,
        string previousUuid,
        string? referenceOldUuid)
    {
        var unsigned = Write(sale, drugs, settings, uuid: string.Empty, previousUuid, referenceOldUuid);
        var uuid = EtaSerializer.Hash(EtaSerializer.Serialize(unsigned));
        return new EtaReceipt(Write(sale, drugs, settings, uuid, previousUuid, referenceOldUuid), uuid);
    }

    private static string Write(
        Sale sale,
        IReadOnlyDictionary<Guid, Drug> drugs,
        EtaSettings settings,
        string uuid,
        string previousUuid,
        string? referenceOldUuid)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();

            w.WriteStartObject("header");
            w.WriteString("dateTimeIssued", DateTime.SpecifyKind(sale.SoldAtUtc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            w.WriteString("receiptNumber", sale.Number.ToString(CultureInfo.InvariantCulture));
            w.WriteString("uuid", uuid);
            w.WriteString("previousUUID", previousUuid);
            if (referenceOldUuid is not null)
            {
                w.WriteString("referenceOldUUID", referenceOldUuid);
            }

            w.WriteString("currency", "EGP");
            w.WriteEndObject();

            w.WriteStartObject("documentType");
            w.WriteString("receiptType", ReceiptType);
            w.WriteString("typeVersion", TypeVersion);
            w.WriteEndObject();

            w.WriteStartObject("seller");
            w.WriteString("rin", settings.Rin);
            w.WriteString("companyTradeName", settings.CompanyTradeName);
            w.WriteString("branchCode", settings.BranchCode);
            w.WriteStartObject("branchAddress");
            w.WriteString("country", "EG");
            w.WriteString("governate", settings.Governate);
            w.WriteString("regionCity", settings.RegionCity);
            w.WriteString("street", settings.Street);
            w.WriteString("buildingNumber", settings.BuildingNumber);
            w.WriteEndObject();
            w.WriteString("deviceSerialNumber", settings.PosSerial);
            w.WriteString("activityCode", settings.ActivityCode);
            w.WriteEndObject();

            w.WriteStartObject("buyer");
            w.WriteString("type", "P");
            w.WriteEndObject();

            w.WriteStartArray("itemData");
            foreach (var line in sale.Lines.OrderBy(l => l.LineNumber))
            {
                var barcode = drugs[line.DrugId].Barcode;
                w.WriteStartObject();
                w.WriteString("internalCode", barcode);
                w.WriteString("description", line.DrugNameAr);
                w.WriteString("itemType", "GS1");
                w.WriteString("itemCode", barcode);
                w.WriteString("unitType", line.Unit == SaleUnit.Pack ? PackUnitType : SubUnitType);
                w.WriteNumber("quantity", line.Quantity);
                WriteAmount(w, "unitPrice", line.UnitPrice);
                WriteAmount(w, "netSale", line.LineTotal);
                WriteAmount(w, "totalSale", line.LineTotal);
                WriteAmount(w, "total", line.LineTotal);
                w.WriteStartArray("taxableItems");
                w.WriteStartObject();
                w.WriteString("taxType", Vat);
                WriteAmount(w, "amount", 0m);
                w.WriteString("subType", VatExempt);
                WriteAmount(w, "rate", 0m);
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();

            WriteAmount(w, "totalSales", sale.Total);
            WriteAmount(w, "netAmount", sale.Total);
            WriteAmount(w, "totalAmount", sale.Total);
            w.WriteStartArray("taxTotals");
            w.WriteStartObject();
            w.WriteString("taxType", Vat);
            WriteAmount(w, "amount", 0m);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteString("paymentMethod", CashPayment);

            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Amounts are written in their shortest form (45, 15.5, 15.17): ETA hashes numbers exactly as
    /// written, and the shortest form is the one every serializer agrees on.
    /// </summary>
    private static void WriteAmount(Utf8JsonWriter w, string name, decimal amount) =>
        w.WriteNumber(name, decimal.Parse(Math.Round(amount, 5).ToString("0.#####", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
}
