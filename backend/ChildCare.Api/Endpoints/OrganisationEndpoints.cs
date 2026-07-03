using ChildCare.Application.Organisations;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organisations").WithTags("Organisations");

        group.MapPost("/register", async (RegisterOrganisationRequest req, IMediator mediator) =>
        {
            // FluentValidation.ValidationException (RegisterOrganisationCommandValidator) is
            // handled centrally by the global exception handler in Program.cs, not here.
            var result = await mediator.Send(new RegisterOrganisationCommand(
                req.InvitationToken,
                req.OrganisationName,
                req.DirectorName,
                req.Email,
                req.Password));

            if (result.Succeeded)
                return Results.Created("/api/organisations/register", result.Response);

            // research.md R5: not-found/expired/already-used are all the same generic 404 —
            // deliberately indistinguishable, so a caller can't enumerate invitation state.
            return result.Failure switch
            {
                RegisterOrganisationFailure.InvitationNotFound => Results.Json(
                    new { errorKey = "errors.invitation.not_found" },
                    statusCode: StatusCodes.Status404NotFound),

                RegisterOrganisationFailure.EmailMismatch => Results.Json(
                    new { errorKey = "errors.validation", fieldErrors = new { email = "errors.registration.email_mismatch" } },
                    statusCode: StatusCodes.Status422UnprocessableEntity),

                _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterOrganisationFailure)}: {result.Failure}"),
            };
        });
    }
}
