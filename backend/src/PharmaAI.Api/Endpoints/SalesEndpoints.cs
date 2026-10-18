using MediatR;
using PharmaAI.Application.Sync;
using PharmaAI.Application.Sales.Commands.CreateSale;
using PharmaAI.Application.Sales.Queries.GetSale;
using PharmaAI.Application.Sales.Queries.ListSales;

namespace PharmaAI.Api.Endpoints;

internal static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this RouteGroupBuilder api)
    {
        var sales = api.MapGroup("/sales");

        sales.MapGet("/", async (DateOnly? date, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListSalesQuery(date), ct)));

        sales.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetSaleQuery(id), ct) is { } sale ? Results.Ok(sale) : Results.NotFound());

        sales.MapPost("/", async (CreateSaleCommand command, ISender sender, ISyncTrigger sync, CancellationToken ct) =>
        {
            var sale = await sender.Send(command, ct);
            sync.RequestSync();
            return Results.Created($"/api/sales/{sale.Id}", sale);
        });

        return api;
    }
}
