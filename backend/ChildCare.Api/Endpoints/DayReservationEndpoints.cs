using System.Security.Claims;
using ChildCare.Application.DayReservations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013a — contracts/day-reservations-api.md.</summary>
public static class DayReservationEndpoints
{
    public static void MapDayReservationEndpoints(this WebApplication app)
    {
        var parentGroup = app.MapGroup("/api/day-reservations")
            .WithTags("DayReservations")
            .RequireAuthorization("ParentOnly");

        parentGroup.MapPost("/", async (SubmitDayReservationRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new SubmitDayReservationCommand(
                TenantUserIdOf(ctx), req.ChildId, req.Type, req.RequestedDate, req.ExchangeForDate, req.Reason));
            return result.Succeeded
                ? Results.Created($"/api/day-reservations/{result.Response!.Id}", result.Response)
                : MapFailure(result.Failure!.Value);
        });

        parentGroup.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelDayReservationCommand(TenantUserIdOf(ctx), id));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        parentGroup.MapGet("/mine", async (Guid? childId, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListMyDayReservationsQuery(TenantUserIdOf(ctx), childId));
            return Results.Ok(result.Reservations);
        });

        var directorGroup = app.MapGroup("/api/day-reservations")
            .WithTags("DayReservations")
            .RequireAuthorization("DirectorOnly");

        directorGroup.MapGet("/", async (string? status, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListPendingDayReservationsQuery(status));
            return Results.Ok(result.Reservations);
        });

        directorGroup.MapPost("/{id:guid}/approve", async (Guid id, ApproveDayReservationRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new ApproveDayReservationCommand(TenantUserIdOf(ctx), id, req.AbsenceJustified));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        directorGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectDayReservationRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new RejectDayReservationCommand(TenantUserIdOf(ctx), id, req.DirectorNotes));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapFailure(DayReservationFailure failure) => failure switch
    {
        DayReservationFailure.NotFound => Results.Json(
            new { errorKey = "errors.day_reservations.not_found" }, statusCode: StatusCodes.Status404NotFound),

        DayReservationFailure.ChildNotLinked => Results.Json(
            new { errorKey = "errors.day_reservations.child_not_linked" }, statusCode: StatusCodes.Status403Forbidden),

        DayReservationFailure.NotContractedDay => Results.Json(
            new { errorKey = "errors.day_reservations.not_contracted_day" }, statusCode: StatusCodes.Status400BadRequest),

        DayReservationFailure.ClosureDay => Results.Json(
            new { errorKey = "errors.day_reservations.closure_day" }, statusCode: StatusCodes.Status400BadRequest),

        DayReservationFailure.NotPending => Results.Json(
            new { errorKey = "errors.day_reservations.not_pending" }, statusCode: StatusCodes.Status409Conflict),

        DayReservationFailure.MissingJustifiedFlag => Results.Json(
            new { errorKey = "errors.day_reservations.missing_justified_flag" }, statusCode: StatusCodes.Status400BadRequest),

        DayReservationFailure.NoContractedLocation => Results.Json(
            new { errorKey = "errors.day_reservations.no_contracted_location" }, statusCode: StatusCodes.Status409Conflict),

        DayReservationFailure.ClosureDayConflict => Results.Json(
            new { errorKey = "errors.day_reservations.closure_day" }, statusCode: StatusCodes.Status409Conflict),

        _ => throw new InvalidOperationException($"Unhandled {nameof(DayReservationFailure)}: {failure}"),
    };
}
