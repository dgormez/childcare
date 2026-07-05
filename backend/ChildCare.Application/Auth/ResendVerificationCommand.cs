using MediatR;

namespace ChildCare.Application.Auth;

/// <summary>Non-exempt route — same DI-scoped ITenantDbContext pattern as LogoutCommand. The
/// verification email this sends still needs the organisation slug embedded in its link
/// (research.md R2), read from ICurrentTenantService (already resolved by TenantMiddleware).</summary>
public record ResendVerificationCommand(Guid UserId) : IRequest;
