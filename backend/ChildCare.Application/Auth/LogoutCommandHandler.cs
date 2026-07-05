using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>Revokes only the calling device's refresh token, leaving other devices' sessions
/// active — always succeeds silently if the token is already gone (unchanged behavior from the
/// old AuthService.LogoutAsync).</summary>
public class LogoutCommandHandler(ITenantDbContext db) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenEntity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);
        if (tokenEntity is null) return;

        db.RefreshTokens.Remove(tokenEntity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
