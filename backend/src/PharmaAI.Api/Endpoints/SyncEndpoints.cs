using MediatR;
using PharmaAI.Application.Sync;
using PharmaAI.Application.Sync.Commands.RetryOutboxMessage;
using PharmaAI.Application.Sync.Commands.SaveEtaSettings;
using PharmaAI.Application.Sync.Queries.GetEtaSettings;
using PharmaAI.Application.Sync.Queries.GetSyncStatus;

namespace PharmaAI.Api.Endpoints;

internal static class SyncEndpoints
{
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder api)
    {
        var sync = api.MapGroup("/sync");

        sync.MapGet("/status", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSyncStatusQuery(), ct)));

        sync.MapPost("/run", (ISyncTrigger trigger) =>
        {
            trigger.RequestSync();
            return Results.Accepted();
        });

        sync.MapPost("/messages/{id:guid}/retry", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RetryOutboxMessageCommand(id), ct);
            return Results.NoContent();
        });

        sync.MapGet("/eta-settings", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetEtaSettingsQuery(), ct)));

        sync.MapPut("/eta-settings", async (SaveEtaSettingsCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command, ct)));

        return api;
    }
}
