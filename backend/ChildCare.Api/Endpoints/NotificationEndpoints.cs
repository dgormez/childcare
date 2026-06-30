using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ChildCare.Api.Data;

namespace ChildCare.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        // ── POST /api/notifications/push-token ───────────────────────────────
        // Called by the mobile app after requesting notification permissions.
        // Stores the Expo push token so the server can send push notifications.
        group.MapPost("/push-token", async (
            PushTokenRequest req, HttpContext context, AppDbContext db) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user is null) return Results.NotFound();

            user.ExpoPushToken = req.Token;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("api-user");
    }
}

public record PushTokenRequest(string Token);
