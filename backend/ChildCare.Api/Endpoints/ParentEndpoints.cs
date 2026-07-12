using System.Security.Claims;
using ChildCare.Application.Common;
using ChildCare.Application.DayReservations;
using ChildCare.Application.GroupActivities;
using ChildCare.Application.Parent;
using ChildCare.Contracts.Requests;
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
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
