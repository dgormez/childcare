using ChildCare.Api.Middleware;
using ChildCare.Application.Invitations;
using ChildCare.Application.Organisations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organisations").WithTags("Organisations");

        // Feature 032 — found during implementation (spec.md User Story 2, AC1/AC3): the
        // registration page needs to pre-fill/lock the invited email and show an invalid-link
        // state before the user submits anything. Same generic-404 posture as the register
        // endpoint below (research.md R5) — never reveal *why* a token doesn't resolve. Shares
        // the same rate-limit policy since it's equally unauthenticated and token-guessable.
        group.MapGet("/register/{token}", async (string token, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInvitationInfoByTokenQuery(token));
            return result.Succeeded
                ? Results.Ok(new ChildCare.Contracts.Responses.InvitationInfoResponse(result.Email!))
                : Results.Json(new { errorKey = "errors.invitation.not_found" }, statusCode: StatusCodes.Status404NotFound);
        })
        .RequireTenantExempt()
        .RequireRateLimiting("organisation-register");

        // Feature 007a (spec.md FR-005a): the only DirectorOnly, tenant-scoped read on this
        // group — registration below is anonymous/tenant-exempt.
        group.MapGet("/me", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCurrentOrganisationQuery());
            return Results.Ok(result);
        }).RequireAuthorization("DirectorOnly");

        // Feature 014 — contracts/014-invoicing/invoicing-api.md. First PUT on this resource.
        // Feature 024-esignature (User Story 4) extends it with SepaCreditorIdentifier.
        group.MapPut("/me", async (UpdateOrganisationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateOrganisationCommand(req.KboNumber, req.SepaCreditorIdentifier));
            return Results.Ok(result);
        }).RequireAuthorization("DirectorOnly");

        group.MapPost("/register", async (RegisterOrganisationRequest req, IMediator mediator) =>
        {
            // FluentValidation.ValidationException (RegisterOrganisationCommandValidator) is
            // handled centrally by the global exception handler in Program.cs, not here.
            var result = await mediator.Send(new RegisterOrganisationCommand(
                req.InvitationToken,
                req.OrganisationName,
                req.DirectorName,
                req.Email,
                req.Password));

            if (result.Succeeded)
                return Results.Created("/api/organisations/register", result.Response);

            // research.md R5: not-found/expired/already-used are all the same generic 404 —
            // deliberately indistinguishable, so a caller can't enumerate invitation state.
            return result.Failure switch
            {
                RegisterOrganisationFailure.InvitationNotFound => Results.Json(
                    new { errorKey = "errors.invitation.not_found" },
                    statusCode: StatusCodes.Status404NotFound),

                RegisterOrganisationFailure.EmailMismatch => Results.Json(
                    new { errorKey = "errors.validation", fieldErrors = new { email = "errors.registration.email_mismatch" } },
                    statusCode: StatusCodes.Status422UnprocessableEntity),

                _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterOrganisationFailure)}: {result.Failure}"),
            };
        })
        .RequireTenantExempt()
        // Feature 032, FR-011a: this endpoint has existed since feature 001 with no rate
        // limiting because nothing ever linked to it publicly — feature 032's new
        // web/app/register page is what makes it genuinely reachable by real traffic.
        .RequireRateLimiting("organisation-register");
    }
}
