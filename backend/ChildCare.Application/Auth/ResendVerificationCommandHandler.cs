using ChildCare.Application.Common;
using MediatR;

namespace ChildCare.Application.Auth;

public class ResendVerificationCommandHandler(
    ITenantDbContext db,
    ICurrentTenantService currentTenant,
    IEmailSender emailSender,
    Microsoft.Extensions.Configuration.IConfiguration config) : IRequestHandler<ResendVerificationCommand>
{
    public async Task Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null || user.EmailVerified) return; // no-op if already verified, unchanged behavior

        VerificationTokenFactory.SetVerificationToken(user);
        await db.SaveChangesAsync(cancellationToken);

        var verifyUrl = AuthLinkBuilder.BuildVerifyUrl(config, user.EmailVerificationToken!, currentTenant.TenantSlug);
        await emailSender.SendEmailVerificationAsync(user.Email, verifyUrl);
    }
}
