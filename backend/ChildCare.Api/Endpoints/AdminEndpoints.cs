using ChildCare.Api.Middleware;
using ChildCare.Application.Invitations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapPost("/invitations", async (CreateInvitationRequest req, IMediator mediator) =>
        {
            // FluentValidation.ValidationException (CreateInvitationCommandValidator) is
            // handled centrally by the global exception handler in Program.cs, not here.
            var response = await mediator.Send(new CreateInvitationCommand(req.Email));
            return Results.Created($"/api/admin/invitations/{response.InvitationId}", response);
        })
        .RequireAuthorization("SuperAdmin")
        .RequireTenantExempt();
    }
}
