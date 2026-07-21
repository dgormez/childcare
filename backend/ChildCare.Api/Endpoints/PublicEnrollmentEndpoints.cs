using System.Net;
using ChildCare.Api.Middleware;
using ChildCare.Api.Services;
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

        // Feature 023, User Story 3 (FR-016/FR-018) — server-rendered HTML, not a JSON API
        // response, mirroring EmailEndpoints.RenderUnsubscribePage's exact pattern
        // (research.md R4): a one-click accept/decline confirmation is the same shape of
        // interaction as unsubscribe/resubscribe, reached only via an emailed link.
        group.MapGet("/tour-response", async (string token, string org, string response, IMediator mediator) =>
        {
            var result = await mediator.Send(new RespondTourInvitationCommand(org, token, response));
            return Results.Content(RenderTourResponsePage(result), "text/html");
        });
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

    private static string RenderTourResponsePage(RespondTourInvitationResult result)
    {
        var labels = TourResponsePageLabelsProvider.For(result.Locale);

        if (result.Outcome == TourResponseOutcome.Invalid)
        {
            return WrapPage(WebUtility.HtmlEncode(labels.InvalidLinkText));
        }

        if (result.Outcome == TourResponseOutcome.NoLongerActive)
        {
            return WrapPage($"""
                <h1 style="font-size:18px">{WebUtility.HtmlEncode(labels.NoLongerActiveTitle)}</h1>
                <p>{WebUtility.HtmlEncode(labels.NoLongerActiveText)}</p>
                """);
        }

        var (title, textFormat) = result.Accepted
            ? (labels.AcceptedTitle, labels.AcceptedText)
            : (labels.DeclinedTitle, labels.DeclinedText);

        return WrapPage($"""
            <h1 style="font-size:18px">{WebUtility.HtmlEncode(title)}</h1>
            <p>{WebUtility.HtmlEncode(string.Format(textFormat, result.ChildName ?? ""))}</p>
            """);
    }

    private static string WrapPage(string bodyHtml) => $"""
        <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
        <body style="font-family:sans-serif;max-width:420px;margin:60px auto;padding:0 16px;color:#1F2937">
        {bodyHtml}
        </body></html>
        """;
}
