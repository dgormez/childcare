using ChildCare.Application.Organisations;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 032 — contracts/platform-admin-portal-api.md. Read-only (FR-013): no
/// POST/PATCH/DELETE on this resource at all — a directory, not an administrative control
/// surface over existing organisations.</summary>
public static class PlatformAdminOrganisationEndpoints
{
    public static void MapPlatformAdminOrganisationEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/platform-admin/organisations")
            .WithTags("PlatformAdminOrganisations")
            .RequireAuthorization("PlatformAdminOnly")
            .MapGet("/", async (IMediator mediator) =>
            {
                var list = await mediator.Send(new ListPlatformAdminOrganisationsQuery());
                return Results.Ok(list);
            });
    }
}
