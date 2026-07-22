using System.Security.Claims;
using ChildCare.Application.SepaBatches;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

// Feature 026 — contracts/sepa-direct-debit-api.md. Director-only, mirrors
// CodaTransactionEndpoints/InvoiceEndpoints' single-group-per-file pattern.
public static class SepaBatchEndpoints
{
    public static void MapSepaBatchEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api").WithTags("SepaBatches").RequireAuthorization("DirectorOnly");

        director.MapGet("/locations/{locationId:guid}/sepa-batch-eligibility", async (Guid locationId, DateOnly month, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetSepaBatchEligibilityQuery(locationId, month));
            return Results.Ok(result);
        });

        director.MapPost("/locations/{locationId:guid}/sepa-batches", async (Guid locationId, GenerateSepaBatchRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GenerateSepaBatchCommand(locationId, req.InvoiceIds, req.ExecutionDate, tenantUserId));
            if (result.Succeeded)
                return Results.File(result.Xml!, "application/xml", $"sepa-batch-{result.BatchId}.xml");
            return result.Failure switch
            {
                GenerateSepaBatchFailure.NoInvoicesSelected => Results.Json(new { errorKey = "errors.sepa_batch.no_invoices_selected" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                GenerateSepaBatchFailure.ExecutionDateTooSoon => Results.Json(new { errorKey = "errors.sepa_batch.execution_date_too_soon", minimumExecutionDate = result.MinimumExecutionDate }, statusCode: StatusCodes.Status422UnprocessableEntity),
                GenerateSepaBatchFailure.CreditorNotConfigured => Results.Json(new { errorKey = "errors.sepa_batch.creditor_not_configured" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                GenerateSepaBatchFailure.InvoiceNotEligible => Results.Json(new { errorKey = "errors.sepa_batch.invoice_not_eligible" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                GenerateSepaBatchFailure.GenerationFailed => Results.Json(new { errorKey = "errors.sepa_batch.generation_failed" }, statusCode: StatusCodes.Status500InternalServerError),
                _ => throw new InvalidOperationException($"Unhandled {nameof(GenerateSepaBatchFailure)}: {result.Failure}"),
            };
        });

        director.MapGet("/locations/{locationId:guid}/sepa-batches", async (Guid locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListSepaBatchesQuery(locationId));
            return Results.Ok(result);
        });
    }
}
