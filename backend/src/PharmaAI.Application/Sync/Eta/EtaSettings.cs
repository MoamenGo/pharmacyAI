namespace PharmaAI.Application.Sync.Eta;

/// <summary>
/// What the pharmacy registered with the tax authority's e-receipt system. Stored as one JSON
/// setting; the two secrets never leave the API (see <see cref="EtaSettingsDto"/>).
/// </summary>
public sealed record EtaSettings
{
    public const string SettingKey = "eta.settings";
    public const string RetailPharmacyActivityCode = "4772";

    public bool Enabled { get; init; }

    public EtaEnvironment Environment { get; init; } = EtaEnvironment.PreProduction;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>The POS device serial registered with ETA; also sent as the seller's deviceSerialNumber.</summary>
    public string PosSerial { get; init; } = string.Empty;

    public string PosOsVersion { get; init; } = string.Empty;

    public string PosModelFramework { get; init; } = string.Empty;

    public string PresharedKey { get; init; } = string.Empty;

    /// <summary>The taxpayer registration number (رقم التسجيل الضريبي).</summary>
    public string Rin { get; init; } = string.Empty;

    public string CompanyTradeName { get; init; } = string.Empty;

    public string BranchCode { get; init; } = "0";

    public string Governate { get; init; } = string.Empty;

    public string RegionCity { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public string BuildingNumber { get; init; } = string.Empty;

    public string ActivityCode { get; init; } = RetailPharmacyActivityCode;

    /// <summary>Names of the required fields that are still empty.</summary>
    public IReadOnlyList<string> MissingFields() =>
        new (string Name, string Value)[]
        {
            (nameof(ClientId), ClientId),
            (nameof(ClientSecret), ClientSecret),
            (nameof(PosSerial), PosSerial),
            (nameof(PosOsVersion), PosOsVersion),
            (nameof(PosModelFramework), PosModelFramework),
            (nameof(PresharedKey), PresharedKey),
            (nameof(Rin), Rin),
            (nameof(CompanyTradeName), CompanyTradeName),
            (nameof(BranchCode), BranchCode),
            (nameof(Governate), Governate),
            (nameof(RegionCity), RegionCity),
            (nameof(Street), Street),
            (nameof(BuildingNumber), BuildingNumber),
            (nameof(ActivityCode), ActivityCode),
        }
        .Where(f => string.IsNullOrWhiteSpace(f.Value))
        .Select(f => f.Name)
        .ToList();

    public bool CanSend => Enabled && MissingFields().Count == 0;
}

public enum EtaEnvironment
{
    PreProduction = 1,
    Production = 2,
}

public static class EtaEndpoints
{
    public static Uri Identity(EtaEnvironment environment) => new(environment == EtaEnvironment.Production
        ? "https://id.eta.gov.eg/"
        : "https://id.preprod.eta.gov.eg/");

    public static Uri Api(EtaEnvironment environment) => new(environment == EtaEnvironment.Production
        ? "https://api.invoicing.eta.gov.eg/"
        : "https://api.preprod.invoicing.eta.gov.eg/");
}
