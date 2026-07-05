using MediatR;

namespace ChildCare.Application.Auth;

public record ForgotPasswordCommand(
    string OrganisationSlug,
    string Email) : IRequest<AuthActionResult>;
