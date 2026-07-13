using ChildCare.Api.Auth;
using ChildCare.Application.MealPreferences;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013d — contracts/meal-list-api.md.</summary>
public static class MealListEndpoints
{
    public static void MapMealListEndpoints(this WebApplication app)
    {
        // Never reachable by the parent app (FR-012) — DeviceOrStaffOrDirector only, same
        // policy ChildrenEndpoints/GroupsEndpoints use for the equivalent caregiver-or-director
        // read split (research.md R4).
        var reads = app.MapGroup("/api/locations/{locationId:guid}/meal-list")
            .WithTags("MealList")
            .RequireAuthorization("DeviceOrStaffOrDirector");

        reads.MapGet("/", async (Guid locationId, DateOnly date, bool? includeExpected, HttpContext ctx, IMediator mediator) =>
        {
            // A device token always carries a GroupId claim; a Director/Staff user-JWT never
            // does — presence of the claim is what distinguishes the two caller kinds here,
            // regardless of exact role (research.md R4).
            var deviceGroupId = ctx.User.FindFirst(DeviceTokenClaims.GroupId)?.Value is { } groupIdClaim
                ? Guid.Parse(groupIdClaim)
                : (Guid?)null;

            var result = await mediator.Send(new GetMealListQuery(locationId, date, deviceGroupId, includeExpected ?? false));
            return Results.Ok(result);
        });

        var preferences = app.MapGroup("/api/children/{childId:guid}/meal-preferences")
            .WithTags("MealList")
            .RequireAuthorization("DirectorOnly");

        // Additive read — the child-profile edit form needs current values to pre-fill, since
        // the PUT below is a partial-upsert (research.md, GetMealPreferenceQuery.cs).
        preferences.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMealPreferenceQuery(childId));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        preferences.MapPut("/", async (Guid childId, UpsertMealPreferenceRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var updatedBy = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new UpsertMealPreferenceCommand(
                childId, req.Texture, req.DietaryType, req.PortionSize, req.AdditionalNotes, updatedBy));

            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });
    }
}
