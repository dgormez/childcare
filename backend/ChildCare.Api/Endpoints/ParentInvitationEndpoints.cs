using ChildCare.Api.Middleware;
using ChildCare.Application.ParentInvitations;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// POST /api/parent-invitations is DirectorOnly. POST /api/parent-invitations/accept is
/// anonymous and tenant-exempt — mirrors StaffEndpoints.cs's accept-invitation route (feature
/// 005): it has no JWT for TenantMiddleware to resolve a tenant from, so it resolves its own
/// schema from a client-supplied organisation slug instead.
/// </summary>
public static class ParentInvitationEndpoints
{
    public static void MapParentInvitationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/parent-invitations", async (CreateParentInvitationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateParentInvitationCommand(req.ContactId));
            return MapResult(result, onSuccess: r => Results.Created($"/api/parent-invitations/{r.InvitationId}", r));
        }).WithTags("ParentInvitations").RequireAuthorization("DirectorOnly");

        app.MapPost("/api/parent-invitations/accept", async (AcceptParentInvitationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AcceptParentInvitationCommand(req.OrganisationSlug, req.Token, req.Password));
            return result.Succeeded
                ? Results.Ok()
                : MapFailure(result.Failure!.Value);
        }).WithTags("ParentInvitations").RequireTenantExempt();
    }

    private static IResult MapResult(ParentInvitationResult result, Func<ParentInvitationResponse, IResult> onSuccess)
        => result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(ParentInvitationFailure failure) => failure switch
    {
        ParentInvitationFailure.ContactNotFound => Results.Json(
            new { errorKey = "errors.contact.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ParentInvitationFailure.AlreadyHasAccount => Results.Json(
            new { errorKey = "errors.parent_invitation.already_has_account" },
            statusCode: StatusCodes.Status409Conflict),

        ParentInvitationFailure.OrganisationNotFound => Results.Json(
            new { errorKey = "errors.auth.organisation_not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ParentInvitationFailure.InvitationInvalidOrExpired => Results.Json(
            new { errorKey = "errors.invitation.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        _ => throw new InvalidOperationException($"Unhandled {nameof(ParentInvitationFailure)}: {failure}"),
    };
}
