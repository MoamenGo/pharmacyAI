using PharmaAI.Application.Settings.Commands.UpsertSetting;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Application.Tests.Settings;

public class UpsertSettingCommandValidatorTests
{
    private readonly UpsertSettingCommandValidator _validator = new();

    [Theory]
    [InlineData("pharmacy.name")]
    [InlineData("tax.eta.registration")]
    [InlineData("receipt.footer2")]
    public void Accepts_well_formed_keys(string key) =>
        Assert.True(_validator.Validate(new UpsertSettingCommand(key, "صيدلية الشفاء")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("Pharmacy.Name")]
    [InlineData("pharmacy..name")]
    [InlineData(".pharmacy")]
    [InlineData("pharmacy name")]
    public void Rejects_malformed_keys(string key)
    {
        var result = _validator.Validate(new UpsertSettingCommand(key, "x"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpsertSettingCommand.Key));
    }

    [Fact]
    public void Rejects_key_longer_than_limit() =>
        Assert.False(_validator.Validate(new UpsertSettingCommand(new string('a', AppSetting.KeyMaxLength + 1), "x")).IsValid);

    [Fact]
    public void Rejects_null_value() =>
        Assert.False(_validator.Validate(new UpsertSettingCommand("pharmacy.name", null!)).IsValid);

    [Fact]
    public void Accepts_empty_value() =>
        Assert.True(_validator.Validate(new UpsertSettingCommand("receipt.footer", string.Empty)).IsValid);

    [Fact]
    public void Rejects_value_longer_than_limit() =>
        Assert.False(_validator.Validate(new UpsertSettingCommand("receipt.footer", new string('x', AppSetting.ValueMaxLength + 1))).IsValid);
}
