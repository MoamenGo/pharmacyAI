using MediatR;
using PharmaAI.Application.SystemStatus.Queries.GetSystemStatus;

namespace PharmaAI.Api.Endpoints;

internal static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSystemStatusQuery(), ct)));

        return api;
    }
}
