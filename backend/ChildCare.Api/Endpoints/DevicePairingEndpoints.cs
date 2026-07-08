using System.Security.Claims;
using ChildCare.Api.Auth;
using ChildCare.Application.Devices;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 008a (kiosk mode) — contracts/device-pairing-api.md.</summary>
public static class DevicePairingEndpoints
{
    public static void MapDevicePairingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/devices/pair", async (PairDeviceRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var pairedBy = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new PairDeviceCommand(req.LocationId, req.GroupId, req.DirectorOverridePin, pairedBy));

            if (result.Succeeded)
                return Results.Ok(result.Response);

            return result.Failure switch
            {
                DeviceFailure.LocationNotFound => Results.Json(
                    new { errorKey = "errors.location.not_found" }, statusCode: StatusCodes.Status404NotFound),
                DeviceFailure.GroupNotFound => Results.Json(
                    new { errorKey = "errors.devices.group_not_found" }, statusCode: StatusCodes.Status404NotFound),
                _ => throw new InvalidOperationException($"Unhandled {nameof(DeviceFailure)}: {result.Failure}"),
            };
        }).WithTags("Devices").RequireAuthorization("DirectorOnly");

        app.MapPost("/api/devices/exit-room-mode", async (ExitRoomModeRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var deviceId = Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value);
            var result = await mediator.Send(new ExitRoomModeCommand(deviceId, req.DirectorOverridePin));

            if (result.Succeeded)
                return Results.Ok(new { ok = true });

            return result.Failure switch
            {
                ExitRoomModeFailure.InvalidOverridePin => Results.Json(
                    new { errorKey = "errors.devices.invalid_override_pin", attemptsRemaining = result.AttemptsRemaining },
                    statusCode: StatusCodes.Status401Unauthorized),
                ExitRoomModeFailure.OverridePinLocked => Results.Json(
                    new { errorKey = "errors.devices.override_pin_locked", lockedUntil = result.LockedUntil },
                    statusCode: StatusCodes.Status423Locked),
                _ => throw new InvalidOperationException($"Unhandled {nameof(ExitRoomModeFailure)}: {result.Failure}"),
            };
        }).WithTags("Devices").RequireAuthorization("DeviceAuthenticated");

        app.MapPost("/api/devices/{id:guid}/revoke", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RevokeDeviceCommand(id));
            return result.Succeeded
                ? Results.NoContent()
                : Results.Json(new { errorKey = "errors.devices.not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("Devices").RequireAuthorization("DirectorOnly");
    }
}

public record PairDeviceRequest(Guid LocationId, Guid GroupId, string DirectorOverridePin);

public record ExitRoomModeRequest(string DirectorOverridePin);
