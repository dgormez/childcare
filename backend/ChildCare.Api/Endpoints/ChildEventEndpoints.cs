using ChildCare.Api.Auth;
using ChildCare.Application.ChildEvents;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 009 — contracts/child-events-api.md.</summary>
public static class ChildEventEndpoints
{
    public static void MapChildEventEndpoints(this WebApplication app)
    {
        var deviceGroup = app.MapGroup("/api/child-events")
            .WithTags("ChildEvents")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapPost("/", async (RecordChildEventRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (deviceId, locationId, groupId) = DeviceClaimsOf(ctx);
            var eventType = ParseEventType(req.EventType);
            if (eventType is null)
                return Results.Json(new { errorKey = "errors.child_events.invalid_event_type" }, statusCode: StatusCodes.Status400BadRequest);

            var result = await mediator.Send(new RecordChildEventCommand(
                req.Id, req.ChildId, locationId, groupId, deviceId, eventType.Value,
                req.OccurredAt, req.EndedAt, req.Payload, req.VisibleToParent, req.AdministeredByStaffId));

            return MapResult(result, onSuccess: r => Results.Created($"/api/child-events/{r.Id}", r));
        });

        // Feature 009c — contracts/child-events-batch-api.md.
        deviceGroup.MapPost("/batch", async (RecordChildEventBatchRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (deviceId, locationId, groupId) = DeviceClaimsOf(ctx);
            var eventType = ParseEventType(req.EventType);
            if (eventType is null)
                return Results.Json(new { errorKey = "errors.child_events.invalid_event_type" }, statusCode: StatusCodes.Status400BadRequest);

            var items = req.Items.Select(i => new ChildEventBatchItem(i.ChildId, i.Id)).ToList();
            var result = await mediator.Send(new RecordChildEventBatchCommand(
                items, locationId, groupId, deviceId, eventType.Value,
                req.OccurredAt, req.EndedAt, req.Payload, req.VisibleToParent));

            var response = new ChildEventBatchResponse(
                result.Created.Select(c => new ChildEventBatchCreatedItem(c.ChildId, c.EventId)).ToList(),
                result.Errors.Select(e => new ChildEventBatchErrorItem(e.ChildId, ToWireReason(e.Reason))).ToList());
            return Results.Ok(response);
        });

        deviceGroup.MapGet("/", async (Guid childId, string? before, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListChildEventsQuery(childId, before, limit ?? 20));
            return Results.Ok(result);
        });

        deviceGroup.MapGet("/daily-summary", async (Guid childId, DateOnly date, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDailySummaryQuery(childId, date));
            return Results.Ok(result);
        });

        // PATCH/DELETE need to accept either a device token (caregiver, same-day-and-location)
        // or a director's user JWT (any event, any day) on one route — no existing endpoint in
        // this codebase does this (contracts/child-events-api.md's corrected note), so a
        // dedicated composite policy is used instead of either single-scheme group above.
        var correctionGroup = app.MapGroup("/api/child-events")
            .WithTags("ChildEvents")
            .RequireAuthorization("DeviceOrDirector");

        correctionGroup.MapPatch("/{id:guid}", async (Guid id, UpdateChildEventRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerOf(ctx);
            var result = await mediator.Send(new UpdateChildEventCommand(
                id, isDirector, deviceLocationId, req.EndedAt, req.Payload, req.VisibleToParent, req.AdministeredByStaffId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        correctionGroup.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerOf(ctx);
            var result = await mediator.Send(new DeleteChildEventCommand(id, isDirector, deviceLocationId));
            return result.Succeeded
                ? Results.NoContent()
                : MapFailure(result.Failure!.Value);
        });
    }

    private static ChildEventType? ParseEventType(string value) =>
        ChildEventTypeExtensions.TryParseWireString(value, out var parsed) ? parsed : null;

    private static string ToWireReason(ChildEventBatchFailureReason reason) => reason switch
    {
        ChildEventBatchFailureReason.ChildNotFound => "child_not_found",
        ChildEventBatchFailureReason.NotPresent => "not_present",
        _ => throw new InvalidOperationException($"Unhandled {nameof(ChildEventBatchFailureReason)}: {reason}"),
    };

    private static (Guid DeviceId, Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));

    // Branches on which auth scheme actually authenticated this request (research.md R4,
    // contracts/child-events-api.md) — a request authenticated as neither a device nor a
    // director (e.g. a caregiver's own personal session JWT) is deliberately treated as a
    // non-director device-less caller, which CanModify always rejects.
    private static (bool IsDirector, Guid? DeviceLocationId) CallerOf(HttpContext ctx)
    {
        if (ctx.User.IsInRole("director"))
            return (true, null);

        var locationClaim = ctx.User.FindFirst(DeviceTokenClaims.LocationId)?.Value;
        return (false, Guid.TryParse(locationClaim, out var locationId) ? locationId : null);
    }

    private static IResult MapResult(ChildEventResult result, Func<ChildEventResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(ChildEventFailure failure) => failure switch
    {
        ChildEventFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        ChildEventFailure.NotFound => Results.Json(
            new { errorKey = "errors.child_events.not_found" }, statusCode: StatusCodes.Status404NotFound),

        ChildEventFailure.EditWindowExpired => Results.Json(
            new { errorKey = "errors.child_events.edit_window_expired" }, statusCode: StatusCodes.Status403Forbidden),

        _ => throw new InvalidOperationException($"Unhandled {nameof(ChildEventFailure)}: {failure}"),
    };
}
