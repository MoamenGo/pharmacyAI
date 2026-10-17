namespace PharmaAI.Application.Settings;

public sealed record SettingDto(string Key, string Value, DateTime UpdatedAtUtc);
