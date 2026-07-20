using System.Security.Claims;
using ChildCare.Application.Common;
using ChildCare.Application.DayReservations;
using ChildCare.Application.GroupActivities;
using ChildCare.Application.Parent;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Every route is ParentOnly (feature 013).</summary>
public static class ParentEndpoints
{
    public static void MapParentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/parent")
            .WithTags("Parent")
            .RequireAuthorization("ParentOnly");

        group.MapGet("/children", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var children = await mediator.Send(new GetParentChildrenQuery(tenantUserId));
            return Results.Ok(children);
        });

        // Feature 030 (US5) — contracts/family-siblings-api.md.
        group.MapGet("/children/previous", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var children = await mediator.Send(new GetParentPreviousChildrenQuery(tenantUserId));
            return Results.Ok(children);
        });

        group.MapGet("/children/{childId:guid}/daily-summary", async (Guid childId, DateOnly? date, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var requestedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await mediator.Send(new GetParentDailySummaryQuery(tenantUserId, childId, requestedDate));
            return result.Authorized
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });

        group.MapPut("/push-token", async (RegisterPushTokenRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var succeeded = await mediator.Send(new RegisterPushTokenCommand(tenantUserId, req.PushToken));
            return succeeded
                ? Results.Ok()
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });

        // Feature 013f — contracts/reservation-settings-api.md.
        group.MapGet("/children/{childId:guid}/reservation-availability", async (Guid childId, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetReservationAvailabilityQuery(tenantUserId, childId));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.day_reservations.child_not_linked" }, statusCode: StatusCodes.Status403Forbidden);
        });

        // Feature 009b — contracts/group-activities-api.md. Defaults to the current
        // Europe/Brussels calendar month (spec.md Assumptions: no historical browsing).
        group.MapGet("/group-activities/gallery", async (int? year, int? month, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var today = BelgianCalendarDay.Today();
            var result = await mediator.Send(new GetParentGroupActivityGalleryQuery(
                tenantUserId, year ?? today.Year, month ?? today.Month));
            return result.Authorized
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });

        // 031-photo-lifecycle-governance FR-012/FR-013 — contracts/photo-lifecycle-api.md.
        group.MapGet("/photos/{photoType}/{objectRef:guid}/download", async (string photoType, Guid objectRef, HttpContext ctx, IMediator mediator) =>
        {
            var parsedType = photoType switch
            {
                "profile" => ParentPhotoType.Profile,
                "group-activity" => ParentPhotoType.GroupActivity,
                _ => (ParentPhotoType?)null,
            };
            if (parsedType is null)
                return Results.Json(new { errorKey = "errors.photos.not_found" }, statusCode: StatusCodes.Status404NotFound);

            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetParentPhotoDownloadUrlQuery(tenantUserId, parsedType.Value, objectRef));
            if (!result.Succeeded)
            {
                return result.Failure switch
                {
                    ParentPhotoDownloadFailure.Forbidden => Results.Json(
                        new { errorKey = "errors.photos.forbidden" }, statusCode: StatusCodes.Status403Forbidden),
                    ParentPhotoDownloadFailure.NotFound => Results.Json(
                        new { errorKey = "errors.photos.not_found" }, statusCode: StatusCodes.Status404NotFound),
                    _ => throw new InvalidOperationException($"Unhandled {nameof(ParentPhotoDownloadFailure)}: {result.Failure}"),
                };
            }

            return Results.Ok(new ParentPhotoDownloadResponse(result.DownloadUrl!, result.ExpiresAt!.Value));
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
