using System.Security.Claims;
using ChildCare.Api.Auth;
using ChildCare.Application.Attendance;
using ChildCare.Contracts.Responses;
using MediatR;
using AttendanceContracts = ChildCare.Contracts.Requests;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 010 — contracts/attendance-api.md.</summary>
public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this WebApplication app)
    {
        var deviceGroup = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapPost("/check-in", async (AttendanceContracts.AttendanceCheckInRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, groupId) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new CheckInCommand(req.ChildId, locationId, groupId, req.Date));
            return MapResult(result, onSuccess: r => result.Created
                ? Results.Created($"/api/attendance/{r.Id}", r)
                : Results.Ok(r));
        });

        deviceGroup.MapPost("/check-out", async (AttendanceContracts.AttendanceCheckOutRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, _) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new CheckOutCommand(req.ChildId, locationId, req.Date));
            return MapResult(result, onSuccess: Results.Ok);
        });

        // Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
        deviceGroup.MapPost("/qr-code/verify", async (AttendanceContracts.VerifyCheckInCodeRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, groupId) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new VerifyCheckInCodeCommand(req.Code, locationId, groupId));
            return MapVerifyResult(result);
        });

        deviceGroup.MapGet("/bkr", async (Guid locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBkrRatioQuery(locationId));
            return Results.Ok(result);
        });

        deviceGroup.MapGet("/today", async (HttpContext ctx, IMediator mediator) =>
        {
            var (_, locationId, _) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new GetTodayAttendanceQuery(locationId));
            return Results.Ok(result);
        });

        // FR-005: absence-marking is creatable by either a caregiver (device token) or a
        // director — DeviceOrDirector (feature 009 precedent), not DeviceAuthenticated alone.
        var absenceGroup = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization("DeviceOrDirector");

        absenceGroup.MapPost("/absence", async (AttendanceContracts.MarkAbsentRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerOf(ctx);
            Guid locationId;
            Guid? groupId;
            Guid? directorTenantUserId = null;

            if (isDirector)
            {
                locationId = req.LocationId;
                groupId = req.GroupId;
                directorTenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            }
            else
            {
                var (_, claimLocationId, claimGroupId) = DeviceClaimsOf(ctx);
                locationId = claimLocationId;
                groupId = claimGroupId;
            }

            var result = await mediator.Send(new MarkAbsentCommand(
                req.ChildId, locationId, groupId, req.Date, req.AbsenceJustified, req.AbsenceReason, directorTenantUserId));
            return MapResult(result, onSuccess: r => Results.Created($"/api/attendance/{r.Id}", r));
        });

        // PATCH/DELETE accept either a device token (caregiver, same-day-and-location only) or
        // a director's user JWT (any record, any day) — same DeviceOrDirector composite policy
        // feature 009 introduced (contracts/attendance-api.md).
        var correctionGroup = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization("DeviceOrDirector");

        correctionGroup.MapPatch("/{id:guid}", async (Guid id, AttendanceContracts.CorrectAttendanceRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerOf(ctx);
            var result = await mediator.Send(new CorrectAttendanceRecordCommand(
                id, isDirector, deviceLocationId, req.Status, req.CheckInAt, req.CheckOutAt, req.AbsenceJustified, req.AbsenceReason));
            return MapResult(result, onSuccess: Results.Ok);
        });

        correctionGroup.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (isDirector, deviceLocationId) = CallerOf(ctx);
            var result = await mediator.Send(new DeleteAttendanceRecordCommand(id, isDirector, deviceLocationId));
            return result.Succeeded ? Results.NoContent() : MapFailure(result.Failure!.Value);
        });

        // Director-web history/correction view — no caregiver-tablet consumer needs the full
        // cross-day list (contracts/attendance-api.md).
        var directorGroup = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization("DirectorOnly");

        directorGroup.MapGet("/", async (Guid locationId, DateOnly date, string? before, int? limit, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListAttendanceQuery(locationId, date, before, limit ?? 20));
            return Results.Ok(result);
        });

        // Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
        var parentGroup = app.MapGroup("/api/parent/attendance")
            .WithTags("Attendance")
            .RequireAuthorization("ParentOnly");

        parentGroup.MapPost("/qr-code", async (AttendanceContracts.IssueCheckInCodeRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new IssueCheckInCodeCommand(tenantUserId, req.ChildId));
            if (!result.Succeeded)
            {
                return result.Failure switch
                {
                    IssueCheckInCodeFailure.ChildNotFound => Results.Json(
                        new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),
                    IssueCheckInCodeFailure.NotYourChild => Results.Json(
                        new { errorKey = "errors.qrCheckIn.not_your_child" }, statusCode: StatusCodes.Status403Forbidden),
                    IssueCheckInCodeFailure.NotEnabled => Results.Json(
                        new { errorKey = "errors.qrCheckIn.not_enabled" }, statusCode: StatusCodes.Status400BadRequest),
                    _ => throw new InvalidOperationException($"Unhandled {nameof(IssueCheckInCodeFailure)}: {result.Failure}"),
                };
            }

            return Results.Ok(new IssueCheckInCodeResponse(result.Code!.Code, result.Code.ExpiresAtUnix));
        });
    }

    private static IResult MapVerifyResult(VerifyCheckInCodeResult result)
    {
        if (result.Succeeded)
            return Results.Ok(new VerifyCheckInCodeResponse(result.Response!, result.Direction!));

        return result.Failure switch
        {
            VerifyCheckInCodeFailure.InvalidCode => Results.Json(
                new { errorKey = "errors.qrCheckIn.invalid_code" }, statusCode: StatusCodes.Status401Unauthorized),
            VerifyCheckInCodeFailure.AlreadyUsed => Results.Json(
                new { errorKey = "errors.qrCheckIn.already_used" }, statusCode: StatusCodes.Status409Conflict),
            VerifyCheckInCodeFailure.CodeExpired => Results.Json(
                new { errorKey = "errors.qrCheckIn.code_expired" }, statusCode: StatusCodes.Status410Gone),
            VerifyCheckInCodeFailure.WrongLocation => Results.Json(
                new { errorKey = "errors.qrCheckIn.wrong_location" }, statusCode: StatusCodes.Status403Forbidden),
            VerifyCheckInCodeFailure.AttendanceCommandFailed => MapFailure(result.AttendanceFailure!.Value),
            _ => throw new InvalidOperationException($"Unhandled {nameof(VerifyCheckInCodeFailure)}: {result.Failure}"),
        };
    }

    private static (Guid DeviceId, Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.DeviceId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));

    // Branches on which auth scheme actually authenticated this request (mirrors
    // ChildEventEndpoints.CallerOf) — a request authenticated as neither a device nor a
    // director is treated as a non-director device-less caller, which every handler rejects.
    private static (bool IsDirector, Guid? DeviceLocationId) CallerOf(HttpContext ctx)
    {
        if (ctx.User.IsInRole("director"))
            return (true, null);

        var locationClaim = ctx.User.FindFirst(DeviceTokenClaims.LocationId)?.Value;
        return (false, Guid.TryParse(locationClaim, out var locationId) ? locationId : null);
    }

    private static IResult MapResult(AttendanceResult result, Func<AttendanceRecordResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(AttendanceFailure failure) => failure switch
    {
        AttendanceFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        AttendanceFailure.NotFound => Results.Json(
            new { errorKey = "errors.attendance.not_found" }, statusCode: StatusCodes.Status404NotFound),

        AttendanceFailure.AlreadyRecorded => Results.Json(
            new { errorKey = "errors.attendance.already_recorded" }, statusCode: StatusCodes.Status409Conflict),

        AttendanceFailure.ClosureDay => Results.Json(
            new { errorKey = "errors.attendance.closure_day" }, statusCode: StatusCodes.Status403Forbidden),

        AttendanceFailure.EditWindowExpired => Results.Json(
            new { errorKey = "errors.attendance.edit_window_expired" }, statusCode: StatusCodes.Status403Forbidden),

        AttendanceFailure.ClosureStatusImmutable => Results.Json(
            new { errorKey = "errors.attendance.closure_status_immutable" }, statusCode: StatusCodes.Status403Forbidden),

        _ => throw new InvalidOperationException($"Unhandled {nameof(AttendanceFailure)}: {failure}"),
    };
}
