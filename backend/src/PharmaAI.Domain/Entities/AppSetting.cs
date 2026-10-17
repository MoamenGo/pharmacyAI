namespace PharmaAI.Domain.Entities;

/// <summary>
/// A single key/value configuration entry stored in the local database
/// (pharmacy name, tax registration number, receipt footer, ...).
/// </summary>
public sealed class AppSetting
{
    public const int KeyMaxLength = 100;
    public const int ValueMaxLength = 4000;

    private AppSetting()
    {
        Key = string.Empty;
        Value = string.Empty;
    }

    public AppSetting(string key, string value, DateTime updatedAtUtc)
    {
        Key = key;
        Value = value;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Key { get; private set; }

    public string Value { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string value, DateTime updatedAtUtc)
    {
        Value = value;
        UpdatedAtUtc = updatedAtUtc;
    }
}
