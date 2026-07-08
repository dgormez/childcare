using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Auth;

/// <summary>
/// Link-only (FR-009), mirroring GoogleSignInCommandHandler. Apple only sends the email claim
/// on a given Apple ID's very first sign-in anywhere — resolvedEmail falls back to the
/// client-supplied Email for that case (unchanged behavior from the pre-feature-003 AuthService).
/// </summary>
public class AppleSignInCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IAppleTokenValidator appleValidator,
    IAccessTokenIssuer tokenIssuer,
    IConfiguration config) : IRequestHandler<AppleSignInCommand, AuthResult>
{
    public async Task<AuthResult> Handle(AppleSignInCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthResult.Fail(AuthFailure.OrganisationNotFound);

        var bundleId = config["Apple:BundleId"] ?? "com.dgit.childcare";
        var identity = await appleValidator.ValidateAsync(request.IdentityToken, bundleId);
        if (identity is null)
            return AuthResult.Fail(AuthFailure.InvalidCredentials);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var user = await db.Users.FirstOrDefaultAsync(u => u.AppleId == identity.Sub, cancellationToken);

        var resolvedEmail = request.Email?.Trim().ToLowerInvariant() ?? identity.Email?.ToLowerInvariant();

        user ??= resolvedEmail is not null
            ? await db.Users.FirstOrDefaultAsync(u => u.Email == resolvedEmail, cancellationToken)
            : null;

        if (user is null)
        {
            // No AppleId match and nothing to even attempt an email match against — distinct
            // from InvalidCredentials, since there was no candidate account to compare against.
            if (resolvedEmail is null)
                return AuthResult.Fail(AuthFailure.AppleEmailRequiredFirstSignIn);

            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (!AuthMethodPolicy.AppleAllowedFor(user.Role))
            return AuthResult.Fail(AuthFailure.MethodNotAllowedForRole);

        if (user.AppleId is null) user.AppleId = identity.Sub;
        if (!user.EmailVerified) user.EmailVerified = true; // Apple has already verified this email

        var refreshToken = RefreshTokenFactory.AddRefreshToken(db, user, tokenIssuer);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueAccessToken(user.Id, user.Email, tenant.Id, user.Role.ToString().ToLowerInvariant());
        return AuthResult.Success(new AuthSessionResponse(
            accessToken,
            refreshToken,
            new AuthenticatedUser(user.Id, user.Email, user.EmailVerified, user.Role.ToString().ToLowerInvariant(), user.Name)));
    }
}
