using ChildCare.Application.Invoices;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

// Feature 014 — contracts/014-invoicing/invoicing-api.md. Director routes mirror the
// MonthlyMenuEndpoints pattern (director MapGroup + a separate parent MapGroup in the same
// file, feature 013e/013j precedent).
public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api").WithTags("Invoices").RequireAuthorization("DirectorOnly");

        director.MapPost("/locations/{locationId:guid}/invoices/generate", async (Guid locationId, GenerateInvoicesRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new GenerateInvoicesCommand(locationId, req.Year, req.Month));
            if (!result.LocationFound)
                return Results.Json(new { errorKey = "errors.location.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.Ok(result.Invoices);
        });

        director.MapGet("/locations/{locationId:guid}/invoices", async (Guid locationId, IMediator mediator, int? year, int? month, string? status) =>
        {
            var invoices = await mediator.Send(new ListInvoicesQuery(locationId, year, month, status));
            return Results.Ok(invoices);
        });

        director.MapGet("/invoices/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var invoice = await mediator.Send(new GetInvoiceByIdQuery(id));
            return invoice is null
                ? Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(invoice);
        });
    }
}
