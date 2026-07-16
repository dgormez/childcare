using System.Security.Claims;
using ChildCare.Application.FiscalAttestations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

// Feature 015 — contracts/fiscal-attestations-api.md. Director routes mirror InvoiceEndpoints'
// pattern (director MapGroup + a separate parent MapGroup in the same file, 014 precedent).
public static class FiscalAttestationEndpoints
{
    public static void MapFiscalAttestationEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api/fiscal-attestations").WithTags("FiscalAttestations").RequireAuthorization("DirectorOnly");

        director.MapPost("/generate", async (GenerateFiscalAttestationsRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new GenerateFiscalAttestationsCommand(req.TaxYear));
            return Results.Ok(result);
        });

        director.MapGet("/", async (int taxYear, IMediator mediator) =>
        {
            var attestations = await mediator.Send(new ListFiscalAttestationsQuery(taxYear));
            return Results.Ok(attestations);
        });

        director.MapPost("/{childId:guid}/{locationId:guid}/{taxYear:int}/regenerate", async (Guid childId, Guid locationId, int taxYear, IMediator mediator) =>
        {
            var result = await mediator.Send(new RegenerateFiscalAttestationCommand(childId, locationId, taxYear));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                RegenerateFiscalAttestationFailure.NoPaidInvoices => Results.Json(new { errorKey = "errors.fiscalAttestation.no_paid_invoices" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RegenerateFiscalAttestationFailure)}: {result.Failure}"),
            };
        });

        director.MapGet("/{id:guid}/download-url", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFiscalAttestationDownloadUrlQuery(id));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.fiscalAttestation.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.Ok(new { downloadUrl = result.Url, expiresAt = result.ExpiresAt });
        });

        var parent = app.MapGroup("/api/parent/fiscal-attestations").WithTags("FiscalAttestations").RequireAuthorization("ParentOnly");

        parent.MapGet("/", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetParentFiscalAttestationsQuery(tenantUserId));
            return result.Authorized
                ? Results.Ok(result.Attestations)
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });

        parent.MapGet("/{id:guid}/download-url", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetParentFiscalAttestationDownloadUrlQuery(tenantUserId, id));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.fiscalAttestation.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.Ok(new { downloadUrl = result.Url, expiresAt = result.ExpiresAt });
        });
    }
}
