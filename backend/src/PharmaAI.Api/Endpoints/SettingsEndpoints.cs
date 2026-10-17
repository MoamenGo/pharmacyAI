using MediatR;
using PharmaAI.Application.Settings.Commands.UpsertSetting;
using PharmaAI.Application.Settings.Queries.GetSetting;

namespace PharmaAI.Api.Endpoints;

internal static class SettingsEndpoints
{
    public sealed record SettingValueRequest(string Value);

    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var settings = api.MapGroup("/settings");

        settings.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetSettingQuery(key), ct) is { } setting
                ? Results.Ok(setting)
                : Results.NotFound());

        settings.MapPut("/{key}", async (string key, SettingValueRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpsertSettingCommand(key, body.Value), ct)));

        return api;
    }
}
