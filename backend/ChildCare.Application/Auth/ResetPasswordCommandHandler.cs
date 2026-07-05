using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>Exempt-route command — organisation slug comes from the emailed link (research.md
/// R2). Invalidates every refresh token for the account on success (FR-006).</summary>
public class ResetPasswordCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver) : IRequestHandler<ResetPasswordCommand, AuthActionResult>
{
    public async Task<AuthActionResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthActionResult.Fail(AuthFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var user = await db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token, cancellationToken);
        if (user is null || user.PasswordResetExpiry < DateTime.UtcNow)
            return AuthActionResult.Fail(AuthFailure.TokenInvalidOrExpired);

        user.PasswordHash        = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken  = null;
        user.PasswordResetExpiry = null;

        // Invalidate all active sessions across all devices.
        var tokens = db.RefreshTokens.Where(t => t.TenantUserId == user.Id);
        db.RefreshTokens.RemoveRange(tokens);

        await db.SaveChangesAsync(cancellationToken);
        return AuthActionResult.Ok;
    }
}
