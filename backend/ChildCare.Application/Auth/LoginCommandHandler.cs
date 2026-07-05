using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>
/// Replaces feature 002's "default tenant" shim (research.md R7) with real slug-based
/// resolution (research.md R1, FR-008/FR-016). This is an exempt-route command — no tenant
/// context exists yet when it runs, so it resolves the schema itself via
/// ITenantDbContextResolver.ForSchema(...) rather than depending on the DI-scoped
/// ITenantDbContext (which would be unset/wrong here, mirroring the constraint the old
/// AuthService documented, research.md R7).
/// </summary>
public class LoginCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IAccessTokenIssuer tokenIssuer) : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthResult.Fail(AuthFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // SC-005: an unknown email and a wrong password for a known email are indistinguishable.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        var refreshToken = RefreshTokenFactory.AddRefreshToken(db, user, tokenIssuer);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueAccessToken(user.Id, user.Email, tenant.Id, user.Role.ToString().ToLowerInvariant());
        return AuthResult.Success(new AuthSessionResponse(
            accessToken,
            refreshToken,
            new AuthenticatedUser(user.Id, user.Email, user.EmailVerified, user.Role.ToString().ToLowerInvariant())));
    }
}
