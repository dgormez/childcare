using System.Security.Claims;
using ChildCare.Application.Children;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Write routes are DirectorOnly; the two GET routes are StaffOrDirector (feature 008) — kept
/// as a separate MapGroup registration under the same "/api/children" prefix rather than
/// inside the DirectorOnly group, since ASP.NET Core composes group + route
/// RequireAuthorization calls additively (AND), so a more permissive per-route policy cannot
/// live inside a stricter group-level one (research.md R6, specs/008-caregiver-app-scaffold).
/// Non-tenant-exempt throughout (TenantMiddleware must run) — mirrors features 004/005's
/// endpoint-group pattern.
/// </summary>
public static class ChildrenEndpoints
{
    public static void MapChildrenEndpoints(this WebApplication app)
    {
        // Feature 009c (research.md R2): DeviceOrStaffOrDirector rather than StaffOrDirector —
        // a kiosk-paired tablet's device token has no role claim, but the caregiver room
        // roster (this feature's multi-select grid, and the pre-existing single-child flow)
        // needs to read this route under a pure device-token session.
        var reads = app.MapGroup("/api/children")
            .WithTags("Children")
            .RequireAuthorization("DeviceOrStaffOrDirector");

        reads.MapGet("/", async (HttpContext ctx, IMediator mediator, bool includeDeactivated = false, Guid? groupId = null) =>
        {
            var (role, tenantUserId) = CallerIdentity(ctx);
            var children = await mediator.Send(new ListChildrenQuery(includeDeactivated, groupId, role, tenantUserId));
            return Results.Ok(children);
        });

        reads.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = CallerIdentity(ctx);
            var result = await mediator.Send(new GetChildByIdQuery(id, role, tenantUserId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        // Feature 013c FR-013/FR-015: caregiver read-only health/allergy summary, reusing the
        // same eligibility scoping as GetChildByIdQuery above.
        reads.MapGet("/{id:guid}/health-summary", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = CallerIdentity(ctx);
            var result = await mediator.Send(new GetChildHealthSummaryQuery(id, role, tenantUserId));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        var group = app.MapGroup("/api/children")
            .WithTags("Children")
            .RequireAuthorization("DirectorOnly");

        group.MapPost("/", async (CreateChildRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateChildCommand(
                req.FirstName, req.LastName, req.DateOfBirth,
                ParseEnum<Gender>(req.Gender), req.Nationality,
                req.AllergiesDescription, ParseEnum<AllergySeverity>(req.AllergySeverity),
                req.MedicalConditions, req.DietaryRestrictions,
                req.GpName, req.GpPhone, req.PediatricianName, req.PediatricianPhone,
                req.HealthInsuranceNumber, req.Kindcode));
            return MapResult(result, onSuccess: r => Results.Created($"/api/children/{r.Id}", r));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateChildRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateChildCommand(
                id, req.FirstName, req.LastName, req.DateOfBirth,
                ParseEnum<Gender>(req.Gender), req.Nationality,
                req.AllergiesDescription, ParseEnum<AllergySeverity>(req.AllergySeverity),
                req.MedicalConditions, req.DietaryRestrictions,
                req.GpName, req.GpPhone, req.PediatricianName, req.PediatricianPhone,
                req.HealthInsuranceNumber, req.Kindcode));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeactivateChildCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReactivateChildCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/photo/upload-url", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RequestChildPhotoUploadUrlCommand(id));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is null ? null : Enum.Parse<TEnum>(value, ignoreCase: true);

    // Feature 008: extracts the caller's role/id from the JWT so read queries can apply
    // Staff-role location-scoping (research.md R6) — Director callers pass these through
    // unused by the query's scoping branch.
    internal static (string? Role, Guid? TenantUserId) CallerIdentity(HttpContext ctx)
    {
        var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
        var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return (role, idClaim is null ? null : Guid.Parse(idClaim));
    }

    private static IResult MapResult(ChildResult result, Func<ChildResponse, IResult> onSuccess)
    {
        if (result.Succeeded)
            return onSuccess(result.Response!);

        return result.Failure switch
        {
            ChildFailure.NotFound => Results.Json(
                new { errorKey = "errors.child.not_found" },
                statusCode: StatusCodes.Status404NotFound),

            ChildFailure.HasActiveDependents => Results.Json(
                new { errorKey = "errors.child.has_active_dependents" },
                statusCode: StatusCodes.Status409Conflict),

            _ => throw new InvalidOperationException($"Unhandled {nameof(ChildFailure)}: {result.Failure}"),
        };
    }
}
