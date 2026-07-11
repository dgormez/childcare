using ChildCare.Application.Groups;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// `GET /api/groups` is StaffOrDirector (feature 008, kept as a separate MapGroup from the
/// DirectorOnly write route — research.md R6); every other route remains DirectorOnly and
/// non-tenant-exempt — mirrors ChildrenEndpoints.cs.
/// </summary>
public static class GroupsEndpoints
{
    public static void MapGroupsEndpoints(this WebApplication app)
    {
        // Feature 009c (research.md R2): DeviceOrStaffOrDirector rather than StaffOrDirector —
        // see ChildrenEndpoints.cs's matching comment for the full reasoning.
        var groupReads = app.MapGroup("/api/groups")
            .WithTags("Groups")
            .RequireAuthorization("DeviceOrStaffOrDirector");

        groupReads.MapGet("/", async (HttpContext ctx, IMediator mediator, Guid? locationId) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var list = await mediator.Send(new ListGroupsQuery(locationId, role, tenantUserId));
            return Results.Ok(list);
        });

        var groups = app.MapGroup("/api/groups")
            .WithTags("Groups")
            .RequireAuthorization("DirectorOnly");

        groups.MapPost("/", async (CreateGroupRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateGroupCommand(req.Name, req.LocationId));
            return MapGroupResult(result, onSuccess: r => Results.Created($"/api/groups/{r.Id}", r));
        });

        var childGroups = app.MapGroup("/api/children/{childId:guid}/groups")
            .WithTags("Groups")
            .RequireAuthorization("DirectorOnly");

        childGroups.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var history = await mediator.Send(new ListChildGroupHistoryQuery(childId));
            return Results.Ok(history);
        });

        childGroups.MapPost("/", async (Guid childId, AssignChildToGroupRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AssignChildToGroupCommand(childId, req.GroupId, req.StartDate));
            return MapAssignmentResult(result, onSuccess: r => Results.Created($"/api/children/{childId}/groups", r));
        });

        var childVaccinations = app.MapGroup("/api/children/{childId:guid}/vaccinations")
            .WithTags("Vaccinations")
            .RequireAuthorization("DirectorOnly");

        childVaccinations.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var list = await mediator.Send(new ListChildVaccinationsQuery(childId));
            return Results.Ok(list);
        });

        childVaccinations.MapPost("/", async (Guid childId, RecordVaccinationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordVaccinationCommand(childId, req.VaccineName, req.DateAdministered, req.NextDueDate));
            return MapVaccinationResult(result, onSuccess: r => Results.Created($"/api/children/{childId}/vaccinations/{r.Id}", r));
        });
    }

    private static IResult MapGroupResult(GroupResult result, Func<GroupResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapGroupFailure(result.Failure!.Value);

    private static IResult MapAssignmentResult(ChildGroupAssignmentResult result, Func<ChildGroupAssignmentResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapGroupFailure(result.Failure!.Value);

    private static IResult MapVaccinationResult(VaccinationResult result, Func<VaccinationResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapGroupFailure(result.Failure!.Value);

    private static IResult MapGroupFailure(GroupFailure failure) => failure switch
    {
        GroupFailure.NotFound => Results.Json(
            new { errorKey = "errors.group.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        GroupFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.child.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        // /speckit-checklist CHK003: reuses feature 004's location key for both "doesn't exist"
        // and "exists but is deactivated" — a group cannot be newly created against either.
        GroupFailure.LocationNotFound => Results.Json(
            new { errorKey = "errors.location.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        GroupFailure.OutOfChronologicalOrder => Results.Json(
            new { errorKey = "errors.group.out_of_chronological_order" },
            statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(GroupFailure)}: {failure}"),
    };
}
