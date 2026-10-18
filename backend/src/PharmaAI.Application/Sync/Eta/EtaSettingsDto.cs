namespace PharmaAI.Application.Sync.Eta;

/// <summary>ETA settings as the UI sees them: the client secret and preshared key are never sent back.</summary>
public sealed record EtaSettingsDto(
    bool Enabled,
    EtaEnvironment Environment,
    string ClientId,
    bool HasClientSecret,
    string PosSerial,
    string PosOsVersion,
    string PosModelFramework,
    bool HasPresharedKey,
    string Rin,
    string CompanyTradeName,
    string BranchCode,
    string Governate,
    string RegionCity,
    string Street,
    string BuildingNumber,
    string ActivityCode,
    IReadOnlyList<string> MissingFields)
{
    public static EtaSettingsDto From(EtaSettings s) =>
        new(
            s.Enabled,
            s.Environment,
            s.ClientId,
            s.ClientSecret.Length > 0,
            s.PosSerial,
            s.PosOsVersion,
            s.PosModelFramework,
            s.PresharedKey.Length > 0,
            s.Rin,
            s.CompanyTradeName,
            s.BranchCode,
            s.Governate,
            s.RegionCity,
            s.Street,
            s.BuildingNumber,
            s.ActivityCode,
            s.MissingFields());
}
