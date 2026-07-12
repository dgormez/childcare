using ChildCare.Application.ParentInvitations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Development-only endpoints that exist purely to make E2E test seeding possible for flows the
/// real API deliberately never exposes a plaintext secret for (parent invitation tokens are
/// hashed at rest and only ever leave the process via the invitation email — see
/// ChildCare.Application/ParentInvitations/ParentInvitationResult.cs's Token field doc comment).
/// Mapped only when IsDevelopment() (Program.cs) — never in Testing (xUnit drives
/// WebApplicationFactory directly, no HTTP layer to seed through) or Production. No business
/// logic here, mirrors TestSupportEndpoints.cs's "exists purely for tests" precedent.
/// </summary>
public static class E2ESupportEndpoints
{
    public static void MapE2ESupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/e2e-support").WithTags("E2ESupport");

        // Same authorization/tenant-scoping as the real POST /api/parent-invitations — a
        // director inviting a contact in their own org — just with the token attached to the
        // response so a test can complete acceptance without reading real email.
        group.MapPost("/parent-invitations", async (CreateParentInvitationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateParentInvitationCommand(req.ContactId));
            if (!result.Succeeded)
                return Results.Json(new { errorKey = "errors.e2e_support.invitation_failed" }, statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Ok(new
            {
                invitationId = result.Response!.InvitationId,
                contactId = result.Response.ContactId,
                email = result.Response.Email,
                expiresAt = result.Response.ExpiresAt,
                token = result.Token,
            });
        }).RequireAuthorization("DirectorOnly");
    }
}
