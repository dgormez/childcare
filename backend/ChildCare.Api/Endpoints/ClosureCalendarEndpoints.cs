using System.Security.Claims;
using ChildCare.Application.ClosureCalendar;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 011 — director-only closure calendar management.</summary>
public static class ClosureCalendarEndpoints
{
    public static void MapClosureCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/closures")
            .WithTags("Closures")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (Guid locationId, int year, IMediator mediator) =>
        {
            if (year is < 1 or > 9999)
                return ValidationFailure("year", "errors.validation");

            var result = await mediator.Send(new ListClosureDaysQuery(locationId, year));
            return result.Succeeded ? Results.Ok(result.Closures) : MapFailure(result.Failure!.Value);
        });

        // Feature 027 deviation (see ListStaffVisibleClosureDatesQuery.cs) — mirrors
        // LocationEndpoints.cs's /names pattern: a separate StaffOrDirector-scoped MapGroup for
        // the same "/api/closures" path, since a more permissive route can't live inside the
        // DirectorOnly group above.
        app.MapGroup("/api/closures")
            .WithTags("Closures")
            .RequireAuthorization("StaffOrDirector")
            .MapGet("/dates", async (DateOnly from, DateOnly to, IMediator mediator) =>
            {
                if (to < from)
                    return ValidationFailure("to", "errors.validation");

                var dates = await mediator.Send(new ListStaffVisibleClosureDatesQuery(from, to));
                return Results.Ok(dates);
            });

        group.MapPost("/", async (CreateClosureDayRequest req, HttpContext ctx, IMediator mediator) =>
        {
            if (!ClosureCalendarMapper.TryParseClosureType(req.ClosureType, out _))
                return ValidationFailure("closureType", "errors.validation");

            var directorId = DirectorIdOf(ctx);
            var result = await mediator.Send(new CreateClosureDayCommand(
                req.LocationId, req.Date, req.Label, req.ClosureType, req.NotifyParents, directorId));
            return MapResult(result, onSuccess: r => Results.Created($"/api/closures/{r.Id}", r));
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateClosureDayRequest req, HttpContext ctx, IMediator mediator) =>
        {
            if (!ClosureCalendarMapper.TryParseClosureType(req.ClosureType, out _))
                return ValidationFailure("closureType", "errors.validation");

            var result = await mediator.Send(new UpdateClosureDayCommand(id, req.Label, req.ClosureType, req.NotifyParents, DirectorIdOf(ctx)));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/publish", async (Guid id, PublishClosureDayRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var directorId = DirectorIdOf(ctx);
            var result = await mediator.Send(new PublishClosureDayCommand(id, req.ConfirmExistingAttendance, directorId));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : MapFailure(result.Failure!.Value, result.CheckedInCount);
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var directorId = DirectorIdOf(ctx);
            var result = await mediator.Send(new CancelClosureDayCommand(id, directorId));
            if (result.Succeeded && result.RemovedDraft)
                return Results.NoContent();
            return result.Succeeded
                ? Results.Ok(result.Response)
                : MapFailure(result.Failure!.Value);
        });

        group.MapGet("/billable-exclusions", async (Guid locationId, DateOnly from, DateOnly to, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListBillableClosureDatesQuery(locationId, from, to));
            return Results.Ok(result);
        });
    }

    private static Guid DirectorIdOf(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapResult(ClosureCalendarResult result, Func<ClosureDayResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value, result.CheckedInCount);

    private static IResult ValidationFailure(string field, string key) => Results.Json(
        new { errorKey = "errors.validation", fieldErrors = new Dictionary<string, string> { [field] = key } },
        statusCode: StatusCodes.Status422UnprocessableEntity);

    private static IResult MapFailure(ClosureCalendarFailure failure, int checkedInCount = 0) => failure switch
    {
        ClosureCalendarFailure.NotFound => Results.Json(
            new { errorKey = "errors.closures.not_found" }, statusCode: StatusCodes.Status404NotFound),

        ClosureCalendarFailure.LocationNotFound => Results.Json(
            new { errorKey = "errors.location.not_found" }, statusCode: StatusCodes.Status404NotFound),

        ClosureCalendarFailure.DuplicateDate => Results.Json(
            new { errorKey = "errors.closures.duplicate_date" }, statusCode: StatusCodes.Status409Conflict),

        ClosureCalendarFailure.PastDate => Results.Json(
            new { errorKey = "errors.closures.past_date" }, statusCode: StatusCodes.Status400BadRequest),

        ClosureCalendarFailure.NotEditable => Results.Json(
            new { errorKey = "errors.closures.not_editable" }, statusCode: StatusCodes.Status400BadRequest),

        ClosureCalendarFailure.NotPublishable => Results.Json(
            new { errorKey = "errors.closures.not_publishable" }, statusCode: StatusCodes.Status409Conflict),

        ClosureCalendarFailure.AttendanceConfirmationRequired => Results.Json(
            new { errorKey = "errors.closures.attendance_confirmation_required", checkedInCount },
            statusCode: StatusCodes.Status409Conflict),

        _ => throw new InvalidOperationException($"Unhandled {nameof(ClosureCalendarFailure)}: {failure}"),
    };
}
