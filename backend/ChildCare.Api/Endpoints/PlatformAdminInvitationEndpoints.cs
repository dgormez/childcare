using System.Security.Claims;
using ChildCare.Application.Invitations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 032 — contracts/platform-admin-portal-api.md. Every route here requires
/// PlatformAdminOnly (mirrors PlatformAdminVaccineTypeEndpoints.cs, feature 013h) — additive on
/// top of DirectorOnly, never a substitute for it (FR-014).</summary>
public static class PlatformAdminInvitationEndpoints
{
    public static void MapPlatformAdminInvitationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/platform-admin/invitations")
            .WithTags("PlatformAdminInvitations")
            .RequireAuthorization("PlatformAdminOnly");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var list = await mediator.Send(new ListPlatformAdminInvitationsQuery());
            return Results.Ok(list);
        });

        group.MapPost("/", async (CreatePlatformAdminInvitationRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (userId, email) = ActingUserOf(ctx);
            var response = await mediator.Send(new CreatePlatformAdminInvitationCommand(
                req.Email, req.OrganisationNameNote, req.Locale, userId, email));
            return Results.Created($"/api/platform-admin/invitations/{response.Id}", response);
        });

        group.MapPost("/{id:guid}/resend", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (userId, email) = ActingUserOf(ctx);
            var result = await mediator.Send(new ResendPlatformAdminInvitationCommand(id, userId, email));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/revoke", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (userId, email) = ActingUserOf(ctx);
            var result = await mediator.Send(new RevokePlatformAdminInvitationCommand(id, userId, email));
            return result.Succeeded ? Results.Ok(result.Response) : MapFailure(result.Failure!.Value);
        });
    }

    // FR-008: the acting platform-admin's own authenticated identity, never a client-supplied
    // value — same claims JwtService already issues (NameIdentifier, Email), mirroring
    // PlatformAdminVaccineTypeEndpoints.ActingUserOf.
    private static (Guid UserId, string Email) ActingUserOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
        ctx.User.FindFirst(ClaimTypes.Email)!.Value);

    private static IResult MapFailure(PlatformAdminInvitationFailure failure) => failure switch
    {
        PlatformAdminInvitationFailure.NotFound => Results.Json(
            new { errorKey = "errors.invitations.not_found" }, statusCode: StatusCodes.Status404NotFound),

        PlatformAdminInvitationFailure.AlreadyAccepted => Results.Json(
            new { errorKey = "errors.invitations.already_accepted" }, statusCode: StatusCodes.Status409Conflict),

        _ => throw new InvalidOperationException($"Unhandled {nameof(PlatformAdminInvitationFailure)}: {failure}"),
    };
}
