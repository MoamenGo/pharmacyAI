using MediatR;
using PharmaAI.Application.Reports.Queries.GetSalesReport;

namespace PharmaAI.Api.Endpoints;

internal static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/reports/sales", async (DateOnly from, DateOnly to, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSalesReportQuery(from, to), ct)));

        return api;
    }
}
