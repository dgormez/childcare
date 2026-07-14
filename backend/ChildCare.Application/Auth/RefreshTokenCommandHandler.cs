using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>Exempt-route command (no session exists yet) — resolves the organisation from the
/// client-supplied slug (FR-016), same pattern as LoginCommandHandler.</summary>
public class RefreshTokenCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IAccessTokenIssuer tokenIssuer) : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthResult.Fail(AuthFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var tokenEntity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);
        if (tokenEntity is null || tokenEntity.ExpiresAt < DateTime.UtcNow)
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        var user = await db.Users.FindAsync([tokenEntity.TenantUserId], cancellationToken);
        if (user is null)
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        // Rotate: remove the used token and issue a fresh one.
        db.RefreshTokens.Remove(tokenEntity);
        var newRefreshToken = RefreshTokenFactory.AddRefreshToken(db, user, tokenIssuer);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueAccessToken(user.Id, user.Email, tenant.Id, user.Role.ToString().ToLowerInvariant(), user.IsPlatformAdmin);
        return AuthResult.Success(new AuthSessionResponse(
            accessToken,
            newRefreshToken,
            new AuthenticatedUser(user.Id, user.Email, user.EmailVerified, user.Role.ToString().ToLowerInvariant(), user.Name)));
    }
}
