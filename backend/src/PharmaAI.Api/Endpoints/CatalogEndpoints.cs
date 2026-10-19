using MediatR;
using PharmaAI.Application.Catalog.Queries.SearchCatalog;

namespace PharmaAI.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/catalog", async (string? search, int? limit, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SearchCatalogQuery(search, limit ?? SearchCatalogQuery.DefaultLimit), ct)));

        return api;
    }
}
