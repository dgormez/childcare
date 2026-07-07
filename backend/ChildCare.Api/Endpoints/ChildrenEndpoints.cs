using ChildCare.Application.Children;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Every route is DirectorOnly and non-tenant-exempt (TenantMiddleware must run) — mirrors
/// features 004/005's endpoint-group pattern.
/// </summary>
public static class ChildrenEndpoints
{
    public static void MapChildrenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/children")
            .WithTags("Children")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (IMediator mediator, bool includeDeactivated = false) =>
        {
            var children = await mediator.Send(new ListChildrenQuery(includeDeactivated));
            return Results.Ok(children);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetChildByIdQuery(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/", async (CreateChildRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateChildCommand(
                req.FirstName, req.LastName, req.DateOfBirth,
                ParseEnum<Gender>(req.Gender), req.Nationality,
                req.AllergiesDescription, ParseEnum<AllergySeverity>(req.AllergySeverity),
                req.MedicalConditions, req.DietaryRestrictions,
                req.GpName, req.GpPhone, req.HealthInsuranceNumber, req.Kindcode));
            return MapResult(result, onSuccess: r => Results.Created($"/api/children/{r.Id}", r));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateChildRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateChildCommand(
                id, req.FirstName, req.LastName, req.DateOfBirth,
                ParseEnum<Gender>(req.Gender), req.Nationality,
                req.AllergiesDescription, ParseEnum<AllergySeverity>(req.AllergySeverity),
                req.MedicalConditions, req.DietaryRestrictions,
                req.GpName, req.GpPhone, req.HealthInsuranceNumber, req.Kindcode));
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
