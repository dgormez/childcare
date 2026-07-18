using ChildCare.Application.Reporting;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 018 — director-only management reporting dashboard (contracts/management-reporting-api.md).</summary>
public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reporting")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/occupancy", async (Guid? locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetOccupancySummaryQuery(locationId));
            return Results.Ok(result);
        });

        group.MapGet("/bkr", async (Guid? locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetGroupBkrRatioQuery(locationId));
            return Results.Ok(result);
        });

        group.MapGet("/bkr/breaches", async (Guid? locationId, DateOnly? from, DateOnly? to, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBkrBreachHistoryQuery(locationId, from, to));
            return Results.Ok(result);
        });

        group.MapGet("/attendance-summary", async (Guid? locationId, DateOnly month, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAttendanceSummaryQuery(locationId, month));
            return Results.Ok(result);
        });

        group.MapGet("/attendance-summary/export", async (Guid? locationId, DateOnly month, string format, IMediator mediator) =>
        {
            var result = await mediator.Send(new ExportAttendanceSummaryQuery(locationId, month, format));
            if (!result.Succeeded)
                return Results.BadRequest(new { errorKey = "errors.reporting.invalid_export_format" });

            return format == "csv"
                ? Results.File(result.Content!, "text/csv", $"attendance-summary-{month:yyyy-MM}.csv")
                : Results.File(result.Content!, "application/pdf", $"attendance-summary-{month:yyyy-MM}.pdf");
        });

        group.MapGet("/invoices", async (Guid? locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInvoiceStatusOverviewQuery(locationId));
            return Results.Ok(result);
        });

        group.MapGet("/data-completeness", async (Guid? locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDataCompletenessQuery(locationId));
            return Results.Ok(result);
        });
    }
}
