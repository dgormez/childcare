using MediatR;

namespace ChildCare.Application.Auth;

public record VerifyEmailCommand(
    string OrganisationSlug,
    string Token) : IRequest<AuthActionResult>;
