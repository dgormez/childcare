using System.Security.Claims;
using ChildCare.Application.StaffLeaveRequests;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 027 — staff-submitted planned leave requests plus the director's "Verlofaanvragen"
/// approval queue (contracts/staff-app-api.md). Mirrors StaffScheduleEndpoints' split between a
/// DirectorOnly group and standalone StaffOrDirector routes for the same additive-AND-policy
/// reason documented there.
/// </summary>
public static class StaffLeaveRequestEndpoints
{
    public static void MapStaffLeaveRequestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/staff-leave-requests")
            .WithTags("StaffLeaveRequests")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (string? status, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListLeaveRequestsQuery(status));
            return result.Succeeded ? Results.Ok(result.Entries) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/decide", async (Guid id, DecideLeaveRequestRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new DecideLeaveRequestCommand(id, req.Approve, tenantUserId));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        app.MapPost("/api/staff-leave-requests", async (CreateLeaveRequestRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new CreateLeaveRequestCommand(tenantUserId, req.Type, req.DateFrom, req.DateTo, req.Notes));
            return result.Succeeded
                ? Results.Created($"/api/staff-leave-requests/{result.Response!.Id}", result.Response)
                : MapFailure(result.Failure!.Value);
        }).WithTags("StaffLeaveRequests").RequireAuthorization("StaffOrDirector");

        app.MapGet("/api/staff-leave-requests/me", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetMyLeaveRequestsQuery(tenantUserId));
            return result.Found
                ? Results.Ok(result.Entries)
                : Results.Json(new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("StaffLeaveRequests").RequireAuthorization("StaffOrDirector");
    }

    private static IResult MapFailure(StaffLeaveRequestFailure failure) => failure switch
    {
        StaffLeaveRequestFailure.ProfileNotFound => Results.Json(
            new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffLeaveRequestFailure.NotFound => Results.Json(
            new { errorKey = "errors.staff_leave_requests.not_found" }, statusCode: StatusCodes.Status404NotFound),

        StaffLeaveRequestFailure.AlreadyDecided => Results.Json(
            new { errorKey = "errors.staff_leave_requests.already_decided" }, statusCode: StatusCodes.Status409Conflict),

        StaffLeaveRequestFailure.InvalidDateRange => Results.Json(
            new { errorKey = "errors.staff_leave_requests.invalid_date_range" }, statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(StaffLeaveRequestFailure)}: {failure}"),
    };
}
