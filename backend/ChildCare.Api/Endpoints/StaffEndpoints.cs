using System.Security.Claims;
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
/// ResetPasswordCommandHandler/AuthEndpoints.cs, feature 003); and GET /api/staff/me, which is
/// StaffOrDirector rather than DirectorOnly (feature 008) — registered as its own standalone
/// route rather than inside the DirectorOnly group, since ASP.NET Core composes group + route
/// RequireAuthorization calls additively (AND), so a more permissive per-route policy cannot
/// live inside a stricter group-level one (research.md R6, specs/008-caregiver-app-scaffold).
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
            // Feature 027 (FR-002) — null leaves ContractedDays unchanged.
            IReadOnlyList<DayOfWeek>? contractedDays = req.ContractedDays?
                .Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true))
                .ToList();

            var result = await mediator.Send(new UpdateStaffProfileCommand(id, req.FirstName, req.LastName, req.Phone, qualification, contractedDays));
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

        // Feature 008a (kiosk mode) — contracts/pin-management-api.md.
        group.MapPut("/{id:guid}/pin", async (Guid id, SetCaregiverPinRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new SetCaregiverPinCommand(id, req.Pin));
            if (result.Succeeded) return Results.NoContent();
            return result.Failure switch
            {
                PinManagementFailure.NotFound => Results.Json(
                    new { errorKey = "errors.staff.not_found" }, statusCode: StatusCodes.Status404NotFound),
                PinManagementFailure.NotUniqueAtLocation => Results.Json(
                    new { errorKey = "errors.pin.not_unique_at_location" }, statusCode: StatusCodes.Status409Conflict),
                _ => throw new InvalidOperationException($"Unhandled {nameof(PinManagementFailure)}: {result.Failure}"),
            };
        });

        group.MapDelete("/{id:guid}/pin", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteCaregiverPinCommand(id));
            return result.Succeeded
                ? Results.NoContent()
                : Results.Json(new { errorKey = "errors.staff.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        app.MapPost("/api/staff/accept-invitation", async (AcceptStaffInvitationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AcceptStaffInvitationCommand(req.OrganisationSlug, req.Token, req.Password));
            return MapResult(result, onSuccess: Results.Ok);
        }).WithTags("Staff").RequireTenantExempt();

        app.MapGet("/api/staff/me", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetStaffMeQuery(tenantUserId));
            return result.Found
                ? Results.Ok(result.Response)
                : Results.Json(new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("Staff").RequireAuthorization("StaffOrDirector");

        // Feature 027 deviation (see RegisterStaffPushTokenCommand.cs) — mirrors
        // ParentEndpoints.cs's PUT /api/parent/push-token exactly.
        app.MapPut("/api/staff/push-token", async (RegisterPushTokenRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var succeeded = await mediator.Send(new RegisterStaffPushTokenCommand(tenantUserId, req.PushToken));
            return succeeded
                ? Results.Ok()
                : Results.Json(new { errorKey = "errors.staff.profile_not_found" }, statusCode: StatusCodes.Status404NotFound);
        }).WithTags("Staff").RequireAuthorization("StaffOrDirector");
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
