using ChildCare.Api.Middleware;
using ChildCare.Application.Contracts;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 024-esignature — public (anonymous), tenant-exempt endpoints
/// (contracts/024-esignature/esignature-api.md, research.md R1). No JWT `tenant_id` claim exists
/// on these routes; each handler resolves its own tenant schema from the `org` query parameter
/// via `OrganisationSlugResolver`, exactly like feature 020's unsubscribe endpoints and feature
/// 023's public enrollment endpoints. No rate-limit policy (unlike 023's public form) — a
/// signing link is only ever reachable via a specific emailed, cryptographically signed token,
/// not an open form (research.md R1).
/// </summary>
public static class PublicContractSigningEndpoints
{
    public static void MapPublicContractSigningEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/public/contracts")
            .WithTags("PublicContractSigning")
            .AllowAnonymous()
            .RequireTenantExempt();

        group.MapGet("/sign", async (string org, string token, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetContractForSigningQuery(org, token));
            return result.Found
                ? Results.Ok(result.Contract)
                : Results.Json(new { errorKey = "errors.contract_signing.invalid_or_expired" }, statusCode: StatusCodes.Status404NotFound);
        });

        group.MapPost("/sign", async (string org, string token, SubmitContractSigningRequest req, HttpContext httpContext, IMediator mediator) =>
        {
            var signedByIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // FluentValidation.ValidationException (SubmitContractSigningCommandValidator —
            // signature type/data required, IBAN format/checksum) is handled centrally by the
            // global exception handler in Program.cs, not here.
            var result = await mediator.Send(new SubmitContractSigningCommand(
                org, token, req.SignatureType, req.SignatureData, req.SepaIban, signedByIp));

            return result.Succeeded
                ? Results.Ok(new { signed = true })
                : Results.Json(new { errorKey = "errors.contract_signing.invalid_or_expired" }, statusCode: StatusCodes.Status404NotFound);
        });
    }
}
