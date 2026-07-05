using MediatR;

namespace ChildCare.Application.Auth;

public record GoogleSignInCommand(
    string OrganisationSlug,
    string IdToken) : IRequest<AuthResult>;
