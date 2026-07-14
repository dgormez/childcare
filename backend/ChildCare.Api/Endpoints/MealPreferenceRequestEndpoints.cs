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

        var director = app.MapGroup("/api/meal-preference-requests")
            .WithTags("MealPreferenceRequests")
            .RequireAuthorization("DirectorOnly");

        director.MapGet("/", async (string? status, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListMealPreferenceChangeRequestsQuery(status ?? "pending"));
            return Results.Ok(result);
        });

        director.MapPost("/{id:guid}/approve", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new ApproveMealPreferenceChangeRequestCommand(tenantUserId, id));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        director.MapPost("/{id:guid}/reject", async (Guid id, RejectMealPreferenceChangeRequestRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new RejectMealPreferenceChangeRequestCommand(tenantUserId, id, req.Reason));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
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

        MealPreferenceChangeRequestFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound),

        _ => throw new InvalidOperationException($"Unhandled {nameof(MealPreferenceChangeRequestFailure)}: {failure}"),
    };
}
