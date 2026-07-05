using MediatR;

namespace ChildCare.Application.Auth;

/// <summary>Non-exempt route — tenant context is already resolved by TenantMiddleware, so this
/// depends on the DI-scoped ITenantDbContext rather than resolving a schema itself.</summary>
public record LogoutCommand(string RefreshToken) : IRequest;
