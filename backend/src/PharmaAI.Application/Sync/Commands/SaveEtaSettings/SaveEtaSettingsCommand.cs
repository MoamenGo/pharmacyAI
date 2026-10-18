using MediatR;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Sync.Eta;

namespace PharmaAI.Application.Sync.Commands.SaveEtaSettings;

/// <summary>Saves the ETA settings. An empty secret or preshared key keeps the one already stored.</summary>
public sealed record SaveEtaSettingsCommand(
    bool Enabled,
    EtaEnvironment Environment,
    string ClientId,
    string? ClientSecret,
    string PosSerial,
    string PosOsVersion,
    string PosModelFramework,
    string? PresharedKey,
    string Rin,
    string CompanyTradeName,
    string BranchCode,
    string Governate,
    string RegionCity,
    string Street,
    string BuildingNumber,
    string ActivityCode) : IRequest<EtaSettingsDto>;

public sealed class SaveEtaSettingsCommandHandler(IPharmaDbContext db, TimeProvider clock, ISyncTrigger trigger)
    : IRequestHandler<SaveEtaSettingsCommand, EtaSettingsDto>
{
    public async Task<EtaSettingsDto> Handle(SaveEtaSettingsCommand c, CancellationToken cancellationToken)
    {
        var current = await EtaSettingsStore.LoadAsync(db, cancellationToken);
        var settings = new EtaSettings
        {
            Enabled = c.Enabled,
            Environment = c.Environment,
            ClientId = c.ClientId.Trim(),
            ClientSecret = string.IsNullOrWhiteSpace(c.ClientSecret) ? current.ClientSecret : c.ClientSecret.Trim(),
            PosSerial = c.PosSerial.Trim(),
            PosOsVersion = c.PosOsVersion.Trim(),
            PosModelFramework = c.PosModelFramework.Trim(),
            PresharedKey = string.IsNullOrWhiteSpace(c.PresharedKey) ? current.PresharedKey : c.PresharedKey.Trim(),
            Rin = c.Rin.Trim(),
            CompanyTradeName = c.CompanyTradeName.Trim(),
            BranchCode = c.BranchCode.Trim(),
            Governate = c.Governate.Trim(),
            RegionCity = c.RegionCity.Trim(),
            Street = c.Street.Trim(),
            BuildingNumber = c.BuildingNumber.Trim(),
            ActivityCode = c.ActivityCode.Trim(),
        };

        await EtaSettingsStore.SaveAsync(db, settings, clock.GetUtcNow().UtcDateTime, cancellationToken);
        trigger.RequestSync();
        return EtaSettingsDto.From(settings);
    }
}
