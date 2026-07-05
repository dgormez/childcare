using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Auth;

/// <summary>
/// Exempt-route command. Always succeeds regardless of whether the email matches an account
/// (SC-005 — prevents enumerating registered emails); only an unknown/not-ready organisation
/// slug is a real failure (FR-015/FR-016 — slugs aren't secret, so this distinction is safe).
/// </summary>
public class ForgotPasswordCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IEmailSender emailSender,
    IConfiguration config) : IRequestHandler<ForgotPasswordCommand, AuthActionResult>
{
    public async Task<AuthActionResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AuthActionResult.Fail(AuthFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Only allow reset for accounts that have a password (not pure OAuth accounts) — silently
        // no-op otherwise, same as an unknown email, so neither case is observable to the caller.
        if (user is not null && !string.IsNullOrEmpty(user.PasswordHash))
        {
            var token = System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            user.PasswordResetToken  = token;
            user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync(cancellationToken);

            var resetUrl = AuthLinkBuilder.BuildResetUrl(config, token, tenant.Slug);
            await emailSender.SendPasswordResetAsync(user.Email, resetUrl);
        }

        return AuthActionResult.Ok;
    }
}
