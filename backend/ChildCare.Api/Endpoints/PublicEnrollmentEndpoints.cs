using ChildCare.Api.Middleware;
using ChildCare.Application.WaitingList;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 023 — public (anonymous), tenant-exempt endpoints (contracts/023-digital-enrollment/
/// enrollment-api.md, research.md R1). No JWT `tenant_id` claim exists on these routes; each
/// handler resolves its own tenant schema from the `orgSlug` URL segment via
/// `OrganisationSlugResolver`, exactly like feature 020's unsubscribe endpoints.
/// </summary>
public static class PublicEnrollmentEndpoints
{
    public static void MapPublicEnrollmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/public/enrollment")
            .WithTags("PublicEnrollment")
            .AllowAnonymous()
            .RequireTenantExempt();

        group.MapGet("/{orgSlug}/{locationSlug}", async (string orgSlug, string locationSlug, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPublicEnrollmentLocationInfoQuery(orgSlug, locationSlug));
            return result.Succeeded
                ? Results.Ok(new GetPublicEnrollmentLocationInfoResponse(result.Info!.LocationName, result.Info!.Enabled, result.Info!.DefaultLocale))
                : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{orgSlug}/{locationSlug}", async (string orgSlug, string locationSlug, SubmitPublicEnrollmentRequest req, IMediator mediator) =>
        {
            // FR-005: a filled honeypot is silently discarded — no command dispatched, no entry
            // created, no email sent — while still returning the same 200 shape a genuine
            // success would, so the rejection is never observable to the submitter.
            if (!string.IsNullOrEmpty(req.Website))
                return Results.Ok(new SubmitPublicEnrollmentResponse(ReferenceCodePlaceholder));

            var result = await mediator.Send(new SubmitPublicEnrollmentCommand(
                orgSlug, locationSlug, req.ChildFirstName, req.ChildLastName, req.DateOfBirth,
                req.RequestedStartDate, req.ContactName, req.ContactEmail, req.ContactPhone, req.Notes, req.Locale));

            return result.Succeeded
                ? Results.Ok(new SubmitPublicEnrollmentResponse(result.ReferenceCode!))
                : MapFailure(result.Failure!.Value);
        }).RequireRateLimiting("public-enrollment");

        // GET /tour-response (User Story 3, RespondTourInvitationCommand) is added below this
        // method once that command exists.
    }

    // Not tied to any real entity — a filled honeypot must never reveal, via a distinguishable
    // response shape, that the submission was rejected rather than accepted (FR-005).
    private const string ReferenceCodePlaceholder = "00000000";

    private static IResult MapFailure(PublicEnrollmentFailure failure) => failure switch
    {
        PublicEnrollmentFailure.NotFound => Results.Json(
            new { errorKey = "errors.public_enrollment.not_found" }, statusCode: StatusCodes.Status404NotFound),

        PublicEnrollmentFailure.Disabled => Results.Json(
            new { errorKey = "errors.public_enrollment.disabled" }, statusCode: StatusCodes.Status403Forbidden),

        _ => throw new InvalidOperationException($"Unhandled {nameof(PublicEnrollmentFailure)}: {failure}"),
    };
}
