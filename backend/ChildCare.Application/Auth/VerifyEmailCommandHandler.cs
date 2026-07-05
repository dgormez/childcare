using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Auth;

/// <summary>Exempt-route command — the organisation slug comes from the emailed link's query
/// parameter (research.md R2), not typed by the user.</summary>
public class VerifyEmailCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver) : IRequestHandler<VerifyEmailCommand, AuthActionResult>
{
    public async Task<AuthActionResult> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthActionResult.Fail(AuthFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token, cancellationToken);
        if (user is null || user.EmailVerificationExpiry < DateTime.UtcNow)
            return AuthActionResult.Fail(AuthFailure.TokenInvalidOrExpired);

        user.EmailVerified           = true;
        user.EmailVerificationToken  = null;
        user.EmailVerificationExpiry = null;
        await db.SaveChangesAsync(cancellationToken);

        return AuthActionResult.Ok;
    }
}
