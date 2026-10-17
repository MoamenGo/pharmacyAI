using FluentValidation;
using PharmaAI.Application.Behaviors;
using PharmaAI.Application.Settings;
using PharmaAI.Application.Settings.Commands.UpsertSetting;

namespace PharmaAI.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private static readonly SettingDto Expected = new("pharmacy.name", "x", DateTime.UnixEpoch);

    [Fact]
    public async Task Throws_and_skips_handler_when_request_is_invalid()
    {
        var behavior = new ValidationBehavior<UpsertSettingCommand, SettingDto>([new UpsertSettingCommandValidator()]);
        var handlerCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new UpsertSettingCommand("BAD KEY", "x"),
            () => { handlerCalled = true; return Task.FromResult(Expected); },
            CancellationToken.None));

        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Calls_handler_when_request_is_valid()
    {
        var behavior = new ValidationBehavior<UpsertSettingCommand, SettingDto>([new UpsertSettingCommandValidator()]);

        var result = await behavior.Handle(
            new UpsertSettingCommand("pharmacy.name", "x"),
            () => Task.FromResult(Expected),
            CancellationToken.None);

        Assert.Same(Expected, result);
    }
}
