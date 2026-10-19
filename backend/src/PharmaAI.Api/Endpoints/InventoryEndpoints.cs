using MediatR;
using PharmaAI.Application.Inventory.Commands.CreateDrug;
using PharmaAI.Application.Inventory.Commands.DeductStock;
using PharmaAI.Application.Inventory.Commands.ReceiveStock;
using PharmaAI.Application.Inventory.Commands.UpdateDrug;
using PharmaAI.Application.Inventory.Commands.WriteOffStock;
using PharmaAI.Application.Inventory.Queries.GetAlternatives;
using PharmaAI.Application.Inventory.Queries.GetDrug;
using PharmaAI.Application.Inventory.Queries.GetInventoryAlerts;
using PharmaAI.Application.Inventory.Queries.ListDrugs;
using PharmaAI.Domain.Inventory;

namespace PharmaAI.Api.Endpoints;

internal static class InventoryEndpoints
{
    public sealed record ReceiveStockRequest(string BatchNumber, DateOnly ExpiryDate, int Packs, int LooseSubUnits);

    public sealed record DeductStockRequest(int QuantityInSubUnits);

    public sealed record UpdateDrugRequest(
        string NameAr,
        string? NameEn,
        string Barcode,
        long PublicPricePiasters,
        string? ActiveIngredient,
        string? Manufacturer,
        int ReorderLevelInPacks);

    public sealed record WriteOffRequest(int QuantityInSubUnits, WriteOffReason Reason, string? Note);

    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder api)
    {
        var drugs = api.MapGroup("/drugs");

        drugs.MapGet("/", async (string? search, int? limit, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListDrugsQuery(search, limit ?? ListDrugsQuery.DefaultLimit), ct)));

        drugs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetDrugQuery(Id: id), ct) is { } drug ? Results.Ok(drug) : Results.NotFound());

        drugs.MapGet("/by-barcode/{barcode}", async (string barcode, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetDrugQuery(Barcode: barcode), ct) is { } drug ? Results.Ok(drug) : Results.NotFound());

        drugs.MapPost("/", async (CreateDrugCommand command, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(command, ct);
            return Results.Created($"/api/drugs/{created.Drug.Id}", created);
        });

        drugs.MapPost("/{id:guid}/batches", async (Guid id, ReceiveStockRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new ReceiveStockCommand(id, body.BatchNumber, body.ExpiryDate, body.Packs, body.LooseSubUnits), ct)));

        drugs.MapPost("/{id:guid}/deductions", async (Guid id, DeductStockRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new DeductStockCommand(id, body.QuantityInSubUnits), ct)));

        drugs.MapPut("/{id:guid}", async (Guid id, UpdateDrugRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new UpdateDrugCommand(
                    id, body.NameAr, body.NameEn, body.Barcode, body.PublicPricePiasters,
                    body.ActiveIngredient, body.Manufacturer, body.ReorderLevelInPacks),
                ct)));

        drugs.MapPost("/{id:guid}/batches/{batchId:guid}/write-offs", async (Guid id, Guid batchId, WriteOffRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new WriteOffStockCommand(id, batchId, body.QuantityInSubUnits, body.Reason, body.Note), ct)));

        drugs.MapGet("/{id:guid}/alternatives", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAlternativesQuery(id), ct)));

        api.MapGet("/inventory/alerts", async (int? expiryWarningDays, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new GetInventoryAlertsQuery(expiryWarningDays ?? GetInventoryAlertsQuery.DefaultExpiryWarningDays), ct)));

        return api;
    }
}
