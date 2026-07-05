using MediatR;

namespace ChildCare.Application.Auth;

public record ResetPasswordCommand(
    string OrganisationSlug,
    string Token,
    string NewPassword) : IRequest<AuthActionResult>;
