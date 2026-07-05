using MediatR;

namespace ChildCare.Application.Auth;

public record RefreshTokenCommand(
    string OrganisationSlug,
    string RefreshToken) : IRequest<AuthResult>;
