using ChildCare.Application.WaitingList;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 012a — director-only waiting list management (contracts/waiting-list-api.md).</summary>
public static class WaitingListEndpoints
{
    public static void MapWaitingListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/waiting-list")
            .WithTags("WaitingList")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (Guid locationId, string? status, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListWaitingListEntriesQuery(locationId, status));
            return result.Succeeded ? Results.Ok(result.Entries) : MapFailure(result.Failure!.Value);
        });

        group.MapGet("/occupancy", async (Guid locationId, DateOnly from, DateOnly to, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetOccupancyQuery(locationId, from, to));
            return result.Succeeded ? Results.Ok(result.Days) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/", async (CreateWaitingListEntryRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateWaitingListEntryCommand(
                req.ChildFirstName, req.ChildLastName, req.DateOfBirth, req.ContactName,
                req.ContactEmail, req.ContactPhone, req.LocationId, req.RequestedStartDate, req.Notes));
            return result.Succeeded
                ? Results.Created($"/api/waiting-list/{result.Response!.Id}", result.Response)
                : MapFailure(result.Failure!.Value);
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateWaitingListEntryRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateWaitingListEntryCommand(
                id, req.ChildFirstName, req.ChildLastName, req.DateOfBirth, req.ContactName,
                req.ContactEmail, req.ContactPhone, req.LocationId, req.RequestedStartDate, req.Notes));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/reorder", async (Guid id, ReorderWaitingListEntryRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReorderWaitingListEntryCommand(id, req.Direction));
            return result.Succeeded ? Results.Ok(result.Entries) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/status", async (Guid id, TransitionWaitingListStatusRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new TransitionWaitingListStatusCommand(id, req.Status));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/link-child", async (Guid id, LinkChildToWaitingListEntryRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new LinkChildToWaitingListEntryCommand(id, req.ChildId, req.CreateNewChild));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    private static IResult MapFailure(WaitingListFailure failure) => failure switch
    {
        WaitingListFailure.NotFound => Results.Json(
            new { errorKey = "errors.waiting_list.not_found" }, statusCode: StatusCodes.Status404NotFound),

        WaitingListFailure.LocationNotFound => Results.Json(
            new { errorKey = "errors.locations.not_found" }, statusCode: StatusCodes.Status404NotFound),

        WaitingListFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        WaitingListFailure.InvalidStatusTransition => Results.Json(
            new { errorKey = "errors.waiting_list.invalid_status_transition" }, statusCode: StatusCodes.Status409Conflict),

        WaitingListFailure.NotReorderableInCurrentStatus => Results.Json(
            new { errorKey = "errors.waiting_list.not_reorderable_in_current_status" }, statusCode: StatusCodes.Status409Conflict),

        WaitingListFailure.AlreadyAtBoundary => Results.Json(
            new { errorKey = "errors.validation" }, statusCode: StatusCodes.Status400BadRequest),

        WaitingListFailure.InvalidLinkRequest => Results.Json(
            new { errorKey = "errors.validation" }, statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(WaitingListFailure)}: {failure}"),
    };
}
