using System.Security.Claims;
using ChildCare.Api.Auth;
using ChildCare.Application.RoomShifts;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 008a (kiosk mode) — contracts/room-shift-api.md.</summary>
public static class RoomShiftEndpoints
{
    public static void MapRoomShiftEndpoints(this WebApplication app)
    {
        var deviceGroup = app.MapGroup("/api/room-shifts")
            .WithTags("RoomShifts")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapGet("/roster", async (HttpContext ctx, IMediator mediator) =>
        {
            var locationId = LocationIdOf(ctx);
            var roster = await mediator.Send(new GetRoomRosterQuery(locationId));
            return Results.Ok(roster);
        });

        deviceGroup.MapPost("/check-in", async (CheckInRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (deviceId, locationId, groupId) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new CheckInCommand(deviceId, locationId, groupId, req.StaffId, req.Pin));

            if (result.Succeeded)
                return Results.Ok(result.Response);

            return result.Failure switch
            {
                RoomShiftFailure.NotEligible => Results.Json(
                    new { errorKey = "errors.staff.not_eligible_here" }, statusCode: StatusCodes.Status403Forbidden),
                RoomShiftFailure.AlreadyCheckedIn => Results.Json(
                    new { errorKey = "errors.room_shifts.already_checked_in" }, statusCode: StatusCodes.Status409Conflict),
                RoomShiftFailure.Invalid => Results.Json(
                    new { errorKey = "errors.pin.invalid", attemptsRemaining = result.AttemptsRemaining },
                    statusCode: StatusCodes.Status401Unauthorized),
                RoomShiftFailure.Locked => Results.Json(
                    new { errorKey = "errors.pin.locked", lockedUntil = result.LockedUntil },
                    statusCode: StatusCodes.Status423Locked),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RoomShiftFailure)}: {result.Failure}"),
            };
        });

        deviceGroup.MapPost("/check-out", async (CheckOutRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, _) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new CheckOutCommand(locationId, req.StaffId, req.Pin));

            if (result.Succeeded)
                return Results.Ok(result.Response);

            return result.Failure switch
            {
                RoomShiftFailure.NotEligible => Results.Json(
                    new { errorKey = "errors.staff.not_eligible_here" }, statusCode: StatusCodes.Status403Forbidden),
                RoomShiftFailure.NotCheckedIn => Results.Json(
                    new { errorKey = "errors.room_shifts.not_checked_in" }, statusCode: StatusCodes.Status409Conflict),
                RoomShiftFailure.Invalid => Results.Json(
                    new { errorKey = "errors.pin.invalid", attemptsRemaining = result.AttemptsRemaining },
                    statusCode: StatusCodes.Status401Unauthorized),
                RoomShiftFailure.Locked => Results.Json(
                    new { errorKey = "errors.pin.locked", lockedUntil = result.LockedUntil },
                    statusCode: StatusCodes.Status423Locked),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RoomShiftFailure)}: {result.Failure}"),
            };
        });

        deviceGroup.MapPost("/confirm-administrator", async (ConfirmAdministratorRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, _) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new ConfirmAdministratorCommand(locationId, req.StaffId, req.Pin, req.Skip));

            if (result.Succeeded)
                return Results.Ok(result.Response);

            return result.Failure switch
            {
                RoomShiftFailure.NotEligible => Results.Json(
                    new { errorKey = "errors.staff.not_eligible_here" }, statusCode: StatusCodes.Status403Forbidden),
                RoomShiftFailure.NotCheckedIn => Results.Json(
                    new { errorKey = "errors.room_shifts.not_checked_in" }, statusCode: StatusCodes.Status409Conflict),
                RoomShiftFailure.Invalid => Results.Json(
                    new { errorKey = "errors.pin.invalid", attemptsRemaining = result.AttemptsRemaining },
                    statusCode: StatusCodes.Status401Unauthorized),
                RoomShiftFailure.Locked => Results.Json(
                    new { errorKey = "errors.pin.locked", lockedUntil = result.LockedUntil },
                    statusCode: StatusCodes.Status423Locked),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RoomShiftFailure)}: {result.Failure}"),
            };
        });

        // Director-only correction — user JWT, not a device token (contracts/room-shift-api.md).
        app.MapPatch("/api/room-shifts/{id:guid}", async (Guid id, CorrectShiftRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var correctedBy = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new CorrectShiftCommand(id, req.CheckedInAt, req.CheckedOutAt, correctedBy));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.room_shifts.not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("RoomShifts").RequireAuthorization("DirectorOnly");
    }

    private static Guid LocationIdOf(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value);

    private static (Guid DeviceId, Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));
}

/// <summary>Feature 008b: Pin is null when the location's RequiresCaregiverPin is false —
/// enforcement happens server-side in CheckInCommand/CheckOutCommand, never trusting the client.</summary>
public record CheckInRequest(Guid StaffId, string? Pin);

public record CheckOutRequest(Guid StaffId, string? Pin);

public record ConfirmAdministratorRequest(Guid? StaffId, string? Pin, bool Skip);

public record CorrectShiftRequest(DateTime? CheckedInAt, DateTime? CheckedOutAt);
