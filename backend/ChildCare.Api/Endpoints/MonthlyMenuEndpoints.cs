using System.Security.Claims;
using ChildCare.Application.Common;
using ChildCare.Application.MonthlyMenus;
using ChildCare.Contracts.Requests;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013e — contracts/monthly-menu-api.md. Variant query parameter added feature
/// 013j — contracts/013j-monthly-menu-variants/monthly-menu-variants-api.md.</summary>
public static class MonthlyMenuEndpoints
{
    public static void MapMonthlyMenuEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api/locations/{locationId:guid}/monthly-menus/{year:int}/{month:int}")
            .WithTags("MonthlyMenus")
            .RequireAuthorization("DirectorOnly");

        director.MapGet("/", async (Guid locationId, int year, int month, string? variant, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMonthlyMenuQuery(locationId, year, month, ParseVariant(variant)));
            return Results.Ok(result);
        });

        director.MapPut("/", async (Guid locationId, int year, int month, string? variant, UpsertMonthlyMenuRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var updatedBy = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new UpsertMonthlyMenuCommand(locationId, year, month, ParseVariant(variant), req.Days, updatedBy));
            return Results.Ok(result);
        });

        director.MapPost("/publish", async (Guid locationId, int year, int month, string? variant, IMediator mediator) =>
        {
            var result = await mediator.Send(new PublishMonthlyMenuCommand(locationId, year, month, ParseVariant(variant)));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        director.MapPost("/unpublish", async (Guid locationId, int year, int month, string? variant, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnpublishMonthlyMenuCommand(locationId, year, month, ParseVariant(variant)));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        // Feature 013e US2 — contracts/monthly-menu-api.md. Defaults to the current Europe/Brussels
        // calendar month (mirrors GetParentGroupActivityGalleryQuery's identical default, 009b).
        var parent = app.MapGroup("/api/parent")
            .WithTags("MonthlyMenus")
            .RequireAuthorization("ParentOnly");

        parent.MapGet("/monthly-menu", async (int? year, int? month, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var today = BelgianCalendarDay.Today();
            var result = await mediator.Send(new GetParentMonthlyMenuQuery(tenantUserId, year ?? today.Year, month ?? today.Month));
            return result.Authorized
                ? Results.Ok(result.Entries)
                : Results.Json(new { errorKey = "errors.parent.not_a_contact" }, statusCode: StatusCodes.Status403Forbidden);
        });
    }

    // Absent/unrecognized query value = base menu — mirrors this codebase's convention of
    // defaulting to the "no special state" case rather than rejecting an unset optional param.
    private static DietaryType? ParseVariant(string? variant) =>
        variant is not null && DietaryTypeExtensions.TryParseWireString(variant, out var parsed) ? parsed : null;

    private static IResult MapFailure(MonthlyMenuFailure failure) => failure switch
    {
        MonthlyMenuFailure.NotFound => Results.Json(
            new { errorKey = "errors.monthly_menu.not_found" }, statusCode: StatusCodes.Status404NotFound),

        MonthlyMenuFailure.VariantNotEnabled => Results.Json(
            new { errorKey = "errors.monthly_menu.variant_not_enabled" }, statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(MonthlyMenuFailure)}: {failure}"),
    };
}
