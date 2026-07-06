using ChildCare.Api.Middleware;
using ChildCare.Application.Staff;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Every route is DirectorOnly and non-tenant-exempt (TenantMiddleware must run) except
/// POST /api/staff/accept-invitation, which is anonymous and tenant-exempt — it has no JWT for
/// TenantMiddleware to resolve a tenant from, so it resolves its own schema from a
/// client-supplied organisation slug instead (found during implementation, mirrors
/// ResetPasswordCommandHandler/AuthEndpoints.cs, feature 003).
/// </summary>
public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (IMediator mediator, bool includeDeactivated = false) =>
        {
            var staff = await mediator.Send(new ListStaffQuery(includeDeactivated));
            return Results.Ok(staff);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetStaffByIdQuery(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/", async (CreateStaffProfileRequest req, IMediator mediator) =>
        {
            var role = Enum.Parse<UserRole>(req.Role, ignoreCase: true);
            QualificationLevel? qualification = req.QualificationLevel is null
                ? null
                : Enum.Parse<QualificationLevel>(req.QualificationLevel, ignoreCase: true);

            var result = await mediator.Send(new CreateStaffProfileCommand(
                req.FirstName, req.LastName, req.Email, req.Phone, qualification, role, req.ExistingTenantUserId));
            return MapResult(result, onSuccess: r => Results.Created($"/api/staff/{r.Id}", r));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateStaffProfileRequest req, IMediator mediator) =>
        {
            QualificationLevel? qualification = req.QualificationLevel is null
                ? null
                : Enum.Parse<QualificationLevel>(req.QualificationLevel, ignoreCase: true);

            var result = await mediator.Send(new UpdateStaffProfileCommand(id, req.FirstName, req.LastName, req.Phone, qualification));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeactivateStaffProfileCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReactivateStaffProfileCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/resend-invitation", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ResendStaffInvitationCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPut("/{id:guid}/locations/{locationId:guid}", async (Guid id, Guid locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new AssignLocationEligibilityCommand(id, locationId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapDelete("/{id:guid}/locations/{locationId:guid}", async (Guid id, Guid locationId, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnassignLocationEligibilityCommand(id, locationId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/photo/upload-url", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RequestPhotoUploadUrlCommand(id));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.staff.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        app.MapPost("/api/staff/accept-invitation", async (AcceptStaffInvitationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AcceptStaffInvitationCommand(req.OrganisationSlug, req.Token, req.Password));
            return MapResult(result, onSuccess: Results.Ok);
        }).WithTags("Staff").RequireTenantExempt();
    }

    private static IResult MapResult(StaffResult result, Func<StaffResponse, IResult> onSuccess)
    {
        if (result.Succeeded)
            return onSuccess(result.Response!);

        return result.Failure switch
        {
            StaffFailure.NotFound => Results.Json(
                new { errorKey = "errors.staff.not_found" },
                statusCode: StatusCodes.Status404NotFound),

            StaffFailure.LocationNotFound => Results.Json(
                new { errorKey = "errors.location.not_found" },
                statusCode: StatusCodes.Status404NotFound),

            StaffFailure.TenantUserNotFound => Results.Json(
                new { errorKey = "errors.staff.tenant_user_not_found" },
                statusCode: StatusCodes.Status404NotFound),

            StaffFailure.EmailAlreadyExists => Results.Json(
                new { errorKey = "errors.staff.email_already_exists" },
                statusCode: StatusCodes.Status409Conflict),

            StaffFailure.HasActiveDependents => Results.Json(
                new { errorKey = "errors.staff.has_active_dependents" },
                statusCode: StatusCodes.Status409Conflict),

            StaffFailure.AccountAlreadyActive => Results.Json(
                new { errorKey = "errors.staff.account_already_active" },
                statusCode: StatusCodes.Status409Conflict),

            StaffFailure.InvitationInvalidOrExpired => Results.Json(
                new { errorKey = "errors.staff.invitation_invalid_or_expired" },
                statusCode: StatusCodes.Status400BadRequest),

            StaffFailure.OrganisationNotFound => Results.Json(
                new { errorKey = "errors.auth.organisation_not_found" },
                statusCode: StatusCodes.Status404NotFound),

            _ => throw new InvalidOperationException($"Unhandled {nameof(StaffFailure)}: {result.Failure}"),
        };
    }
}
