using System.Security.Claims;
using ChildCare.Application.MealPreferenceRequests;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013e — contracts/monthly-menu-api.md.</summary>
public static class MealPreferenceRequestEndpoints
{
    public static void MapMealPreferenceRequestEndpoints(this WebApplication app)
    {
        var parent = app.MapGroup("/api/parent/children/{childId:guid}")
            .WithTags("MealPreferenceRequests")
            .RequireAuthorization("ParentOnly");

        parent.MapGet("/meal-preference", async (Guid childId, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetParentChildMealPreferenceQuery(tenantUserId, childId));
            return result.Authorized
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });

        parent.MapPost("/meal-preference-requests", async (Guid childId, SubmitMealPreferenceChangeRequestRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new SubmitMealPreferenceChangeRequestCommand(tenantUserId, childId, req.NewTexture, req.NewDietaryType, req.Notes));
            return result.Succeeded
                ? Results.Created($"/api/meal-preference-requests/{result.Response!.Id}", result.Response)
                : MapFailure(result.Failure!.Value);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapFailure(MealPreferenceChangeRequestFailure failure) => failure switch
    {
        MealPreferenceChangeRequestFailure.ChildNotLinked => Results.Json(
            new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden),

        MealPreferenceChangeRequestFailure.DuplicatePendingRequest => Results.Json(
            new { errorKey = "errors.meal_preference_requests.duplicate_pending" }, statusCode: StatusCodes.Status409Conflict),

        MealPreferenceChangeRequestFailure.NotPending => Results.Json(
            new { errorKey = "errors.meal_preference_requests.not_pending" }, statusCode: StatusCodes.Status409Conflict),

        _ => throw new InvalidOperationException($"Unhandled {nameof(MealPreferenceChangeRequestFailure)}: {failure}"),
    };
}
