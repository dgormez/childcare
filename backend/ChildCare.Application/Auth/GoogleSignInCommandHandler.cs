using ChildCare.Application.Common;
using ChildCare.Application.ParentInvitations;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>
/// Link-only (FR-009): validates the Google ID token server-side (IGoogleTokenValidator,
/// research.md R7), then links to an existing TenantUser found by GoogleId or email — never
/// creates one. This is the fix for the pre-feature-003 behavior (AuthService.GoogleSignInAsync)
/// that auto-created an account on no-match, an open-registration path discovered during
/// /speckit-plan's codebase review.
/// </summary>
public class GoogleSignInCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IGoogleTokenValidator googleValidator,
    IAccessTokenIssuer tokenIssuer) : IRequestHandler<GoogleSignInCommand, AuthResult>
{
    public async Task<AuthResult> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthResult.Fail(AuthFailure.OrganisationNotFound);

        var identity = await googleValidator.ValidateAsync(request.IdToken);
        if (identity is null)
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == identity.Sub, cancellationToken)
                ?? await db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email, cancellationToken);

        if (user is null)
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        if (!AuthMethodPolicy.GoogleAllowedFor(user.Role))
            return AuthResult.Fail(AuthFailure.MethodNotAllowedForRole);

        if (user.GoogleId is null) user.GoogleId = identity.Sub;
        if (!user.EmailVerified) user.EmailVerified = true; // Google has already verified this email

        // FR-000b: a Parent-role user completing registration via Google sign-in (rather than
        // the password accept-invitation flow) still needs their Contact linked and existing
        // threads backfilled — see ParentAccountLinker's doc comment for the bug this fixes.
        await ParentAccountLinker.LinkIfUnlinkedParentAsync(db, user, cancellationToken);

        var refreshToken = RefreshTokenFactory.AddRefreshToken(db, user, tokenIssuer);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueAccessToken(user.Id, user.Email, tenant.Id, user.Role.ToString().ToLowerInvariant());
        return AuthResult.Success(new AuthSessionResponse(
            accessToken,
            refreshToken,
            new AuthenticatedUser(user.Id, user.Email, user.EmailVerified, user.Role.ToString().ToLowerInvariant(), user.Name)));
    }
}
