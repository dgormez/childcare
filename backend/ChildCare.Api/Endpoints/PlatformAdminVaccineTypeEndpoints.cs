using System.Security.Claims;
using ChildCare.Application.VaccineTypes;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013h — contracts/platform-admin-vaccine-types-api.md. Every route here
/// requires PlatformAdminOnly (research.md R1) — additive on top of DirectorOnly, never a
/// substitute for it (FR-009). 013g's existing /api/vaccine-types and /api/vaccine-custom-entries
/// (VaccineTypeEndpoints.cs) are untouched (FR-010).</summary>
public static class PlatformAdminVaccineTypeEndpoints
{
    public static void MapPlatformAdminVaccineTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/platform-admin/vaccine-types")
            .WithTags("PlatformAdminVaccineTypes")
            .RequireAuthorization("PlatformAdminOnly");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var list = await mediator.Send(new ListVaccineTypesForPlatformAdminQuery());
            return Results.Ok(list);
        });

        group.MapPost("/", async (CreateVaccineTypeRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateVaccineTypeCommand(req.Name, req.Category));
            return Results.Created($"/api/platform-admin/vaccine-types/{result.Response!.Id}", result.Response);
        });

        group.MapPatch("/{id:guid}", async (Guid id, UpdateVaccineTypeRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateVaccineTypeCommand(id, req.Name, req.Category));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/reorder", async (Guid id, ReorderVaccineTypeRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReorderVaccineTypeCommand(id, req.Direction));
            return result.Succeeded ? Results.Ok(result.Entries) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (userId, email) = ActingUserOf(ctx);
            var result = await mediator.Send(new DeactivateVaccineTypeCommand(id, userId, email));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReactivateVaccineTypeCommand(id));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    // FR-008: the acting platform-admin's own authenticated identity, never a client-supplied
    // value — resolved here from the same claims JwtService already issues (NameIdentifier,
    // Email), mirroring every other "resolved server-side" identity field's endpoint-layer
    // pattern in this codebase (e.g. AnnouncementEndpoints.TenantUserIdOf).
    private static (Guid UserId, string Email) ActingUserOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
        ctx.User.FindFirst(ClaimTypes.Email)!.Value);

    private static IResult MapFailure(PlatformAdminVaccineTypeFailure failure) => failure switch
    {
        PlatformAdminVaccineTypeFailure.NotFound => Results.Json(
            new { errorKey = "errors.vaccine_types.not_found" }, statusCode: StatusCodes.Status404NotFound),

        PlatformAdminVaccineTypeFailure.AlreadyAtBoundary => Results.Json(
            new { errorKey = "errors.validation" }, statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformAdminVaccineTypeFailure)}: {failure}"),
    };
}
