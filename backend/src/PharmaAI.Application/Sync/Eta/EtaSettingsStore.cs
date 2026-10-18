using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Application.Sync.Eta;

internal static class EtaSettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static async Task<EtaSettings> LoadAsync(IPharmaDbContext db, CancellationToken cancellationToken)
    {
        var value = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == EtaSettings.SettingKey)
            .Select(s => s.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return value is null ? new EtaSettings() : JsonSerializer.Deserialize<EtaSettings>(value, Json) ?? new EtaSettings();
    }

    public static async Task SaveAsync(IPharmaDbContext db, EtaSettings settings, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(settings, Json);
        var row = await db.AppSettings.SingleOrDefaultAsync(s => s.Key == EtaSettings.SettingKey, cancellationToken);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting(EtaSettings.SettingKey, value, nowUtc));
        }
        else
        {
            row.Update(value, nowUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
