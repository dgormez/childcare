using System.Security.Claims;
using ChildCare.Api.Auth;
using ChildCare.Application.DevelopmentalMilestones;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 016 — contracts/developmental-milestones-api.md.</summary>
public static class DevelopmentalMilestoneEndpoints
{
    public static void MapDevelopmentalMilestoneEndpoints(this WebApplication app)
    {
        // Shared catalog — open to any authenticated caller (caregiver device, director/staff
        // JWT, or parent JWT), no tenant/child scoping needed since it carries no per-child data.
        app.MapGet("/api/developmental-domains", async (IMediator mediator) =>
        {
            var domains = await mediator.Send(new ListDevelopmentalMilestonesQuery());
            return Results.Ok(domains);
        })
        .WithTags("DevelopmentalMilestones")
        .RequireAuthorization();

        var deviceGroup = app.MapGroup("/api/children/{childId:guid}/milestone-observations")
            .WithTags("DevelopmentalMilestones")
            .RequireAuthorization("DeviceAuthenticated")
            .AddEndpointFilter<DeviceTokenRotationFilter>();

        deviceGroup.MapPost("/", async (Guid childId, RecordMilestoneObservationRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (locationId, groupId) = DeviceClaimsOf(ctx);
            var result = await mediator.Send(new RecordMilestoneObservationCommand(
                childId, req.MilestoneId, locationId, groupId, req.Status, req.ObservedAt, req.Notes));

            if (result.Succeeded)
                return Results.Created($"/api/children/{childId}/milestone-observations/{result.Response!.Id}", result.Response);

            return result.Failure switch
            {
                MilestoneObservationFailure.ChildNotFound => Results.Json(
                    new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),
                MilestoneObservationFailure.MilestoneNotFound => Results.Json(
                    new { errorKey = "errors.milestones.milestone_not_found" }, statusCode: StatusCodes.Status404NotFound),
                _ => throw new InvalidOperationException($"Unhandled {nameof(MilestoneObservationFailure)}: {result.Failure}"),
            };
        });

        // DeviceOrStaffOrDirector, not StaffOrDirector alone (research.md R6 correction,
        // mirrors ChildrenEndpoints/GroupsEndpoints' own precedent): the caregiver tablet also
        // needs read access here, to show a confirmation/history view immediately after
        // recording an observation (tasks.md T029), not just director-web.
        var portfolioReadGroup = app.MapGroup("/api/children/{childId:guid}/milestone-portfolio")
            .WithTags("DevelopmentalMilestones")
            .RequireAuthorization("DeviceOrStaffOrDirector");

        portfolioReadGroup.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetChildMilestonePortfolioQuery(childId));
            return result.ChildFound
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        // PDF export stays director-only (FR-008 pairs it with the parent variant below, not
        // the caregiver tablet) — a separate group since it needs a narrower policy than the
        // JSON portfolio route above.
        var directorPdf = app.MapGroup("/api/children/{childId:guid}/milestone-portfolio")
            .WithTags("DevelopmentalMilestones")
            .RequireAuthorization("StaffOrDirector");

        directorPdf.MapGet("/pdf", async (Guid childId, string? locale, IMediator mediator) =>
        {
            var result = await mediator.Send(new GenerateMilestonePortfolioPdfQuery(childId, locale));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.File(result.Bytes, "application/pdf", $"milestone-portfolio-{childId}.pdf");
        });

        var parent = app.MapGroup("/api/parent/children/{childId:guid}/milestone-portfolio")
            .WithTags("DevelopmentalMilestones")
            .RequireAuthorization("ParentOnly");

        parent.MapGet("/", async (Guid childId, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetParentMilestonePortfolioQuery(tenantUserId, childId));
            return result.Authorized
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.milestones.forbidden" }, statusCode: StatusCodes.Status403Forbidden);
        });

        parent.MapGet("/pdf", async (Guid childId, string? locale, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GenerateParentMilestonePortfolioPdfQuery(tenantUserId, childId, locale));
            if (!result.Authorized)
                return Results.Json(new { errorKey = "errors.milestones.forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            return Results.File(result.Bytes, "application/pdf", $"milestone-portfolio-{childId}.pdf");
        });
    }

    private static (Guid LocationId, Guid GroupId) DeviceClaimsOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.LocationId)!.Value),
        Guid.Parse(ctx.User.FindFirst(DeviceTokenClaims.GroupId)!.Value));

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
