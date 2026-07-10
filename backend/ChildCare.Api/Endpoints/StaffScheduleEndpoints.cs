using System.Security.Claims;
using ChildCare.Application.StaffScheduling;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 012 — director-only weekly staff rota, plus a personal-account-scoped read for a
/// caregiver's own schedule. Every route is DirectorOnly except GET /api/staff-schedules/me
/// (FR-012/FR-015), which is StaffOrDirector — registered as its own standalone route rather
/// than inside the DirectorOnly group, mirroring GET /api/staff/me's precedent (feature 008,
/// research.md R3): ASP.NET Core composes group + route RequireAuthorization calls
/// additively (AND), so a more permissive per-route policy cannot live inside a stricter
/// group-level one.
/// </summary>
public static class StaffScheduleEndpoints
{
    public static void MapStaffScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/staff-schedules")
            .WithTags("StaffSchedules")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (Guid locationId, DateOnly weekStart, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListStaffScheduleQuery(locationId, weekStart));
            return result.Succeeded ? Results.Ok(result.Entries) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/", async (CreateStaffScheduleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateStaffScheduleCommand(
                req.StaffProfileId, req.LocationId, req.GroupId, req.Date, req.StartTime, req.EndTime));
            return result.Succeeded
                ? Results.Created($"/api/staff-schedules/{result.Response!.Id}", result.Response)
                : MapFailure(result.Failure!.Value);
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateStaffScheduleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateStaffScheduleCommand(id, req.LocationId, req.GroupId, req.StartTime, req.EndTime));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteStaffScheduleCommand(id));
            return result.Succeeded ? Results.NoContent() : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/absence", async (Guid id, MarkAbsenceRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkAbsenceCommand(id, req.IsAbsent, req.AbsenceReason));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/copy-week", async (CopyWeekRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CopyWeekCommand(req.LocationId, req.SourceWeekStart, req.TargetWeekStart));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapGet("/projected-on-duty", async (Guid locationId, DateOnly date, TimeOnly time, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProjectedOnDutyQuery(locationId, date, time));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        app.MapGet("/api/staff-schedules/me", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetMyScheduleQuery(tenantUserId));
            return result.Found
                ? Results.Ok(result.Entries)
                : Results.Json(new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("StaffSchedules").RequireAuthorization("StaffOrDirector");
    }

    private static IResult MapFailure(StaffScheduleFailure failure) => failure switch
    {
        StaffScheduleFailure.NotFound => Results.Json(
            new { errorKey = "errors.staff_schedules.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffScheduleFailure.StaffNotFound => Results.Json(
            new { errorKey = "errors.staff.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffScheduleFailure.LocationNotFound => Results.Json(
            new { errorKey = "errors.locations.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffScheduleFailure.GroupNotFound => Results.Json(
            new { errorKey = "errors.groups.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffScheduleFailure.NotEligible => Results.Json(
            new { errorKey = "errors.staff_schedules.not_eligible" }, statusCode: StatusCodes.Status403Forbidden),

        StaffScheduleFailure.Overlap => Results.Json(
            new { errorKey = "errors.staff_schedules.overlap" }, statusCode: StatusCodes.Status409Conflict),

        StaffScheduleFailure.Duplicate => Results.Json(
            new { errorKey = "errors.staff_schedules.duplicate" }, statusCode: StatusCodes.Status409Conflict),

        StaffScheduleFailure.PastDate => Results.Json(
            new { errorKey = "errors.staff_schedules.past_date" }, statusCode: StatusCodes.Status400BadRequest),

        StaffScheduleFailure.InvalidCopyTarget => Results.Json(
            new { errorKey = "errors.staff_schedules.invalid_copy_target" }, statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(StaffScheduleFailure)}: {failure}"),
    };
}
