using ChildCare.Application.VaccineCustomEntries;
using ChildCare.Application.VaccineTypes;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013g — contracts/vaccine-catalog-api.md.</summary>
public static class VaccineTypeEndpoints
{
    public static void MapVaccineTypeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/vaccine-types", async (IMediator mediator) =>
        {
            var list = await mediator.Send(new ListVaccineTypesQuery());
            return Results.Ok(list);
        })
        .WithTags("VaccineTypes")
        .RequireAuthorization("DirectorOnly");

        app.MapGet("/api/vaccine-custom-entries", async (IMediator mediator) =>
        {
            var list = await mediator.Send(new ListTenantCustomVaccineEntriesQuery());
            return Results.Ok(list);
        })
        .WithTags("VaccineTypes")
        .RequireAuthorization("DirectorOnly");
    }
}
