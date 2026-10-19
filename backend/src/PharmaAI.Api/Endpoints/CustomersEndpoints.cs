using MediatR;
using PharmaAI.Application.Customers.Commands.CreateCustomer;
using PharmaAI.Application.Customers.Commands.RecordCustomerPayment;
using PharmaAI.Application.Customers.Queries.GetCustomer;
using PharmaAI.Application.Customers.Queries.ListCustomers;

namespace PharmaAI.Api.Endpoints;

internal static class CustomersEndpoints
{
    public sealed record RecordPaymentRequest(long AmountPiasters, string? Note);

    public static RouteGroupBuilder MapCustomersEndpoints(this RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/customers");

        customers.MapGet("/", async (string? search, bool? owing, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListCustomersQuery(search, owing ?? false), ct)));

        customers.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetCustomerQuery(id), ct) is { } customer ? Results.Ok(customer) : Results.NotFound());

        customers.MapPost("/", async (CreateCustomerCommand command, ISender sender, CancellationToken ct) =>
        {
            var customer = await sender.Send(command, ct);
            return Results.Created($"/api/customers/{customer.Id}", customer);
        });

        customers.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new RecordCustomerPaymentCommand(id, body.AmountPiasters, body.Note), ct)));

        return api;
    }
}
