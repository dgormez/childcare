using MediatR;

namespace ChildCare.Application.Auth;

public record AppleSignInCommand(
    string OrganisationSlug,
    string IdentityToken,
    string? Email) : IRequest<AuthResult>;
