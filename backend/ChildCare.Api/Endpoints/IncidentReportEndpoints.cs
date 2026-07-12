using ChildCare.Api.Auth;
using ChildCare.Application.IncidentReports;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013b — contracts/incident-reports-api.md.</summary>
public static class IncidentReportEndpoints
{
    public static void MapIncidentReportEndpoints(this WebApplication app)
    {
        // Filing is caregiver-tablet only (research.md/plan.md build no director-facing "file
        // incident" screen; LocationId/GroupId have no resolution source other than a paired
        // device's own claims) — contracts/incident-reports-api.md corrected to match during
        // implementation.
        var deviceGroup = app.MapGroup("/api/incident-reports")
            .WithTags("IncidentReports")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapPost("/", async (FileIncidentReportRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, groupId) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new FileIncidentReportCommand(
                req.ChildId, locationId, groupId, req.OccurredAt, req.LocationDetail, req.Description, req.InjuryType,
                req.FirstAidGiven, req.DoctorCalled, req.DoctorNotes, req.ParentNotified, req.ParentNotifiedAt,
                req.ParentNotifiedHow, req.Witnesses, req.FollowUp));

            return MapResult(result, onSuccess: r => Results.Created($"/api/incident-reports/{r.Id}", r));
        });

        // GET /{id} and PUT /{id} accept either a director's JWT or a caregiver device token
        // (FR-007/FR-018) — no existing single-scheme group covers this combination, mirrors
        // ChildEventEndpoints' correctionGroup precedent (research.md).
        var mixedGroup = app.MapGroup("/api/incident-reports")
            .WithTags("IncidentReports")
            .RequireAuthorization("DeviceOrDirector");

        mixedGroup.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceGroupId) = CallerOf(ctx);
            var result = await mediator.Send(new GetIncidentReportQuery(id, isDirector, deviceGroupId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        mixedGroup.MapPut("/{id:guid}", async (Guid id, UpdateIncidentReportRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerLocationOf(ctx);
            var result = await mediator.Send(new UpdateIncidentReportCommand(
                id, isDirector, deviceLocationId, req.OccurredAt, req.LocationDetail, req.Description, req.InjuryType,
                req.FirstAidGiven, req.DoctorCalled, req.DoctorNotes, req.ParentNotified, req.ParentNotifiedAt,
                req.ParentNotifiedHow, req.Witnesses, req.FollowUp));
            return MapResult(result, onSuccess: Results.Ok);
        });

        // Cross-KDV inspection view + PDF export are DirectorOnly (FR-009/FR-012).
        var directorGroup = app.MapGroup("/api/incident-reports")
            .WithTags("IncidentReports")
            .RequireAuthorization("DirectorOnly");

        directorGroup.MapGet("/", async (Guid? childId, Guid? locationId, DateTime? from, DateTime? to, int? page, int? pageSize, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListIncidentReportsQuery(childId, locationId, from, to, page ?? 1, pageSize ?? 25));
            return Results.Ok(result);
        });

        directorGroup.MapGet("/{id:guid}/pdf", async (Guid id, string? locale, IMediator mediator) =>
        {
            var result = await mediator.Send(new GenerateIncidentReportPdfQuery(id, locale));
            return result.Found
                ? Results.File(result.Bytes, "application/pdf")
                : Results.Json(new { errorKey = "errors.incident_reports.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });
    }

    private static (Guid DeviceId, Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));

    private static (bool IsDirector, Guid? DeviceGroupId) CallerOf(HttpContext ctx)
    {
        if (ctx.User.IsInRole("director"))
            return (true, null);

        var groupClaim = ctx.User.FindFirst(DeviceTokenClaims.GroupId)?.Value;
        return (false, Guid.TryParse(groupClaim, out var groupId) ? groupId : null);
    }

    private static (bool IsDirector, Guid? DeviceLocationId) CallerLocationOf(HttpContext ctx)
    {
        if (ctx.User.IsInRole("director"))
            return (true, null);

        var locationClaim = ctx.User.FindFirst(DeviceTokenClaims.LocationId)?.Value;
        return (false, Guid.TryParse(locationClaim, out var locationId) ? locationId : null);
    }

    private static IResult MapResult(IncidentReportResult result, Func<IncidentReportResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(IncidentReportFailure failure) => failure switch
    {
        IncidentReportFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        IncidentReportFailure.NotFound => Results.Json(
            new { errorKey = "errors.incident_reports.not_found" }, statusCode: StatusCodes.Status404NotFound),

        IncidentReportFailure.Locked => Results.Json(
            new { errorKey = "errors.incident_reports.locked" }, statusCode: StatusCodes.Status409Conflict),

        IncidentReportFailure.ValidationFailed => Results.Json(
            new { errorKey = "errors.validation" }, statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(IncidentReportFailure)}: {failure}"),
    };
}
