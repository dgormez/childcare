using ChildCare.Api.Auth;
using ChildCare.Application.Common;
using ChildCare.Application.GroupActivities;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 009b — contracts/group-activities-api.md.</summary>
public static class GroupActivityEndpoints
{
    public static void MapGroupActivityEndpoints(this WebApplication app)
    {
        // research.md R8: mirrors ChildEventEndpoints.cs's deviceGroup exactly — no dual-auth
        // DeviceOrDirector policy needed here, since this feature has no device-authenticated
        // edit path to combine with a director path (delete is DirectorOnly only).
        var deviceGroup = app.MapGroup("/api/group-activities")
            .WithTags("GroupActivities")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapPost("/", async (CreateGroupActivityRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (deviceId, locationId, groupId) = DeviceClaimsOf(ctx);
            var activityType = ParseActivityType(req.ActivityType);
            if (activityType is null)
                return Results.Json(new { errorKey = "errors.group_activities.invalid_activity_type" }, statusCode: StatusCodes.Status400BadRequest);

            var result = await mediator.Send(new CreateGroupActivityCommand(
                req.Id, groupId, locationId, deviceId, activityType.Value, req.Title, req.Description, req.OccurredAt));

            return MapResult(result, onSuccess: r => Results.Created($"/api/group-activities/{r.Id}", r));
        });

        deviceGroup.MapPost("/{id:guid}/photos", async (Guid id, IFormFile photo, HttpContext ctx, IMediator mediator) =>
        {
            var caption = ctx.Request.Form["caption"].FirstOrDefault();
            await using var stream = photo.OpenReadStream();
            var result = await mediator.Send(new UploadGroupActivityPhotoCommand(id, stream, photo.Length, caption));
            return MapPhotoResult(result, onSuccess: r => Results.Created($"/api/group-activities/{id}/photos/{r.Id}", r));
        })
        // Minimal APIs auto-require antiforgery for IFormFile-bound endpoints; this API has no
        // cookie/browser session for antiforgery to protect (device-token bearer auth only), so
        // no antiforgery middleware is registered and this must be explicitly opted out.
        .DisableAntiforgery();

        deviceGroup.MapGet("/timeline", async (Guid? groupId, DateOnly? date, HttpContext ctx, IMediator mediator) =>
        {
            var (_, _, deviceGroupId) = DeviceClaimsOf(ctx);
            // groupId is not client-selectable in practice — it must match the device token's
            // own claim (defensive check, same pattern as ChildEventEndpoints' edit-window
            // device-location check; contracts/group-activities-api.md).
            if (groupId.HasValue && groupId.Value != deviceGroupId)
                return Results.Forbid();

            var result = await mediator.Send(new GetGroupTimelineQuery(deviceGroupId, date ?? BelgianCalendarDay.Today()));
            return Results.Ok(result);
        });

        // DirectorOnly — no DeviceTokenRotationFilter (that filter is device-token specific).
        var directorGroup = app.MapGroup("/api/group-activities")
            .WithTags("GroupActivities")
            .RequireAuthorization("DirectorOnly");

        // 031-photo-lifecycle-governance FR-011: widened from DirectorOnly to StaffOrDirector —
        // staff already have an established path to *create* a group-activity photo via the
        // deviceGroup above (unchanged), but had no staff-JWT path to delete one. A standalone
        // route rather than folding into directorGroup, since ASP.NET Core composes group +
        // route policy as AND, not override (same reasoning ChildrenEndpoints.cs/
        // StaffEndpoints.cs document for their own DirectorOnly/StaffOrDirector split).
        var staffOrDirectorGroup = app.MapGroup("/api/group-activities")
            .WithTags("GroupActivities")
            .RequireAuthorization("StaffOrDirector");

        staffOrDirectorGroup.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var result = await mediator.Send(new DeleteGroupActivityCommand(id, role, tenantUserId));
            return result.Succeeded
                ? Results.NoContent()
                : MapFailure(result.Failure!.Value);
        });

        directorGroup.MapGet("/director-timeline", async (Guid groupId, DateOnly date, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetGroupTimelineQuery(groupId, date));
            return Results.Ok(result);
        });
    }

    private static GroupActivityType? ParseActivityType(string value) =>
        GroupActivityTypeExtensions.TryParseWireString(value, out var parsed) ? parsed : null;

    private static (Guid DeviceId, Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));

    private static IResult MapResult(GroupActivityResult result, Func<GroupActivityResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapPhotoResult(GroupActivityPhotoResult result, Func<GroupActivityPhotoResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(GroupActivityFailure failure) => failure switch
    {
        GroupActivityFailure.NotFound => Results.Json(
            new { errorKey = "errors.group_activities.not_found" }, statusCode: StatusCodes.Status404NotFound),

        GroupActivityFailure.PhotoLimitReached => Results.Json(
            new { errorKey = "errors.group_activities.photo_limit_reached" }, statusCode: StatusCodes.Status409Conflict),

        GroupActivityFailure.PhotoTooLarge => Results.Json(
            new { errorKey = "errors.group_activities.photo_too_large" }, statusCode: StatusCodes.Status413PayloadTooLarge),

        _ => throw new InvalidOperationException($"Unhandled {nameof(GroupActivityFailure)}: {failure}"),
    };
}
