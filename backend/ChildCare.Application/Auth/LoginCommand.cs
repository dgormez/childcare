using MediatR;

namespace ChildCare.Application.Auth;

public record LoginCommand(
    string OrganisationSlug,
    string Email,
    string Password) : IRequest<AuthResult>;
