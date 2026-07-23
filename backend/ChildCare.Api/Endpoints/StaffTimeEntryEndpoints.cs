using System.Security.Claims;
using ChildCare.Application.StaffTimeEntries;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 028 — staff clock in/out (StaffOrDirector, identity resolved from the JWT, never a
/// client-supplied staff id — research.md R2) plus director-only time-entry correction/
/// lock-management, registered as its own standalone group per feature 012/027's precedent for
/// mixing a permissive personal route with a stricter DirectorOnly group.
/// </summary>
public static class StaffTimeEntryEndpoints
{
    public static void MapStaffTimeEntryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/staff-time-entries/clock-in", async (ClockInRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new ClockInCommand(tenantUserId, req.LocationId, req.GroupId, req.Function));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        }).WithTags("StaffTimeEntries").RequireAuthorization("StaffOrDirector");

        app.MapPost("/api/staff-time-entries/clock-out", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new ClockOutCommand(tenantUserId));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        }).WithTags("StaffTimeEntries").RequireAuthorization("StaffOrDirector");

        // FR-001 Acceptance Scenario 3 — lets staff-mobile show "Einde dienst" vs "Begin dienst"
        // correctly on app load/reopen, not only right after a clock-in/out call.
        app.MapGet("/api/staff-time-entries/me/current", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var entry = await mediator.Send(new GetMyOpenTimeEntryQuery(tenantUserId));
            return Results.Ok(entry);
        }).WithTags("StaffTimeEntries").RequireAuthorization("StaffOrDirector");

        var group = app.MapGroup("/api/staff-time-entries")
            .WithTags("StaffTimeEntries")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (Guid staffProfileId, DateOnly from, DateOnly to, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListStaffTimeEntriesQuery(staffProfileId, from, to));
            return Results.Ok(result);
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateStaffTimeEntryRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateStaffTimeEntryCommand(id, req.ClockedOutAt, req.Function, req.GroupId, req.Notes));
            return result.Succeeded ? Results.Ok(new { entry = result.Response, overlapWarning = result.OverlapWarning }) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/unlock", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var directorId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new UnlockStaffTimeEntryCommand(id, directorId));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/relock", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RelockStaffTimeEntryCommand(id));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    private static IResult MapFailure(StaffTimeEntryFailure failure) => failure switch
    {
        StaffTimeEntryFailure.ProfileNotFound => Results.Json(
            new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffTimeEntryFailure.NotFound => Results.Json(
            new { errorKey = "errors.staff_time_entries.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffTimeEntryFailure.AlreadyClockedIn => Results.Json(
            new { errorKey = "errors.staff_time_entries.already_clocked_in" }, statusCode: StatusCodes.Status409Conflict),

        StaffTimeEntryFailure.NoOpenEntry => Results.Json(
            new { errorKey = "errors.staff_time_entries.no_open_entry" }, statusCode: StatusCodes.Status404NotFound),

        StaffTimeEntryFailure.NoFunctionConfigured => Results.Json(
            new { errorKey = "errors.staff_time_entries.no_function_configured" }, statusCode: StatusCodes.Status400BadRequest),

        StaffTimeEntryFailure.FunctionRequired => Results.Json(
            new { errorKey = "errors.staff_time_entries.function_required" }, statusCode: StatusCodes.Status400BadRequest),

        StaffTimeEntryFailure.FunctionNotConfigured => Results.Json(
            new { errorKey = "errors.staff_time_entries.function_not_configured" }, statusCode: StatusCodes.Status400BadRequest),

        StaffTimeEntryFailure.LocationNotEligible => Results.Json(
            new { errorKey = "errors.staff_time_entries.location_not_eligible" }, statusCode: StatusCodes.Status403Forbidden),

        StaffTimeEntryFailure.GroupLocationMismatch => Results.Json(
            new { errorKey = "errors.staff_time_entries.group_location_mismatch" }, statusCode: StatusCodes.Status400BadRequest),

        StaffTimeEntryFailure.Locked => Results.Json(
            new { errorKey = "errors.staff_time_entries.locked" }, statusCode: StatusCodes.Status423Locked),

        _ => Results.Json(new { errorKey = "errors.generic" }, statusCode: StatusCodes.Status400BadRequest),
    };
}
