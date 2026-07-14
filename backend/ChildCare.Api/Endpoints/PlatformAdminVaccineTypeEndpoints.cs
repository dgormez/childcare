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
    }

    private static IResult MapFailure(PlatformAdminVaccineTypeFailure failure) => failure switch
    {
        PlatformAdminVaccineTypeFailure.NotFound => Results.Json(
            new { errorKey = "errors.vaccine_types.not_found" }, statusCode: StatusCodes.Status404NotFound),

        PlatformAdminVaccineTypeFailure.AlreadyAtBoundary => Results.Json(
            new { errorKey = "errors.validation" }, statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformAdminVaccineTypeFailure)}: {failure}"),
    };
}
