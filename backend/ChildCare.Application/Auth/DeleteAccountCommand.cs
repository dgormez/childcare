using MediatR;

namespace ChildCare.Application.Auth;

/// <summary>Non-exempt route — same DI-scoped ITenantDbContext pattern as LogoutCommand.</summary>
public record DeleteAccountCommand(Guid UserId) : IRequest<bool>;
