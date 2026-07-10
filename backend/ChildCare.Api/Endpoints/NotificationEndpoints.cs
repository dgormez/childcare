using System.Security.Claims;
using ChildCare.Application.Notifications;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Every route is ParentOnly (feature 013).</summary>
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
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
