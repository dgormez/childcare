using System.Security.Claims;
using ChildCare.Application.Notifications;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Every route is ParentOnly (feature 013), plus a feature-027 staff-facing mirror
/// (StaffOrDirector) — both reuse the same TenantUserId-keyed ListNotificationsQuery/
/// MarkNotificationReadCommand handlers (already role-agnostic; only the recipient's
/// TenantUserId scopes the read/write), so this is purely a second route registration, not new
/// Application-layer logic. Not called out as its own task in tasks.md — flagged in the
/// implementation report as a gap staff-mobile's notifications screen (T061) needs to function
/// at all, since no staff-facing notifications endpoint existed before this feature.</summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/parent/notifications")
            .WithTags("Notifications")
            .RequireAuthorization("ParentOnly");

        group.MapGet("/", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var notifications = await mediator.Send(new ListNotificationsQuery(tenantUserId));
            return Results.Ok(notifications);
        });

        group.MapPost("/{id:guid}/read", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var succeeded = await mediator.Send(new MarkNotificationReadCommand(tenantUserId, id));
            return succeeded
                ? Results.Ok()
                : Results.Json(new { errorKey = "errors.notification.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        var staffGroup = app.MapGroup("/api/staff/notifications")
            .WithTags("Notifications")
            .RequireAuthorization("StaffOrDirector");

        staffGroup.MapGet("/", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var notifications = await mediator.Send(new ListNotificationsQuery(tenantUserId));
            return Results.Ok(notifications);
        });

        staffGroup.MapPost("/{id:guid}/read", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var succeeded = await mediator.Send(new MarkNotificationReadCommand(tenantUserId, id));
            return succeeded
                ? Results.Ok()
                : Results.Json(new { errorKey = "errors.notification.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
