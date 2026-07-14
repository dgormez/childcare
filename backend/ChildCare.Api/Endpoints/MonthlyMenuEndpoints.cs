using ChildCare.Application.MonthlyMenus;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013e — contracts/monthly-menu-api.md.</summary>
public static class MonthlyMenuEndpoints
{
    public static void MapMonthlyMenuEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api/locations/{locationId:guid}/monthly-menus/{year:int}/{month:int}")
            .WithTags("MonthlyMenus")
            .RequireAuthorization("DirectorOnly");

        director.MapGet("/", async (Guid locationId, int year, int month, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMonthlyMenuQuery(locationId, year, month));
            return Results.Ok(result);
        });

        director.MapPut("/", async (Guid locationId, int year, int month, UpsertMonthlyMenuRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var updatedBy = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new UpsertMonthlyMenuCommand(locationId, year, month, req.Days, updatedBy));
            return Results.Ok(result);
        });

        director.MapPost("/publish", async (Guid locationId, int year, int month, IMediator mediator) =>
        {
            var result = await mediator.Send(new PublishMonthlyMenuCommand(locationId, year, month));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        director.MapPost("/unpublish", async (Guid locationId, int year, int month, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnpublishMonthlyMenuCommand(locationId, year, month));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    private static IResult MapFailure(MonthlyMenuFailure failure) => failure switch
    {
        MonthlyMenuFailure.NotFound => Results.Json(
            new { errorKey = "errors.monthly_menu.not_found" }, statusCode: StatusCodes.Status404NotFound),

        _ => throw new InvalidOperationException($"Unhandled {nameof(MonthlyMenuFailure)}: {failure}"),
    };
}
