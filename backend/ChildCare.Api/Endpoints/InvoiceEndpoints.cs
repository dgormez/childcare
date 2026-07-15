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

        director.MapPut("/invoices/{id:guid}/extra-charges", async (Guid id, UpdateInvoiceExtraChargesRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateInvoiceExtraChargesCommand(
                id, req.ExtraCharges.Select(c => new InvoiceExtraCharge(c.Label, c.AmountCents)).ToList()));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                UpdateInvoiceExtraChargesFailure.NotFound => Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound),
                UpdateInvoiceExtraChargesFailure.NotDraft => Results.Json(new { errorKey = "errors.invoice.not_draft" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(UpdateInvoiceExtraChargesFailure)}: {result.Failure}"),
            };
        });

        director.MapPost("/invoices/send", async (SendInvoicesRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new SendInvoicesCommand(req.InvoiceIds));
            if (result.Succeeded)
                return Results.Ok(result.Responses);
            return result.Failure switch
            {
                SendInvoicesFailure.NotFound => Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound),
                SendInvoicesFailure.NotDraft => Results.Json(new { errorKey = "errors.invoice.not_draft" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(SendInvoicesFailure)}: {result.Failure}"),
            };
        });

        director.MapPost("/invoices/{id:guid}/mark-paid", async (Guid id, MarkInvoicePaidRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkInvoicePaidCommand(id, req.PaidAt));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                MarkInvoicePaidFailure.NotFound => Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound),
                MarkInvoicePaidFailure.NotSent => Results.Json(new { errorKey = "errors.invoice.not_sent" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(MarkInvoicePaidFailure)}: {result.Failure}"),
            };
        });

        director.MapGet("/invoices/{id:guid}/pdf", async (Guid id, IMediator mediator, string? locale) =>
        {
            var result = await mediator.Send(new GenerateInvoicePdfQuery(id, locale));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.File(result.Bytes, "application/pdf", $"invoice-{id}.pdf");
        });
    }
}
