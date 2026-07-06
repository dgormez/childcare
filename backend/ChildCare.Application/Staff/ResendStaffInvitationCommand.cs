using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Staff;

public record ResendStaffInvitationCommand(Guid StaffProfileId) : IRequest<StaffResult>;

/// <summary>
/// FR-006a: resending supersedes (expires) any still-valid prior invitation for this profile
/// rather than creating a second, independently-valid one — mirrors CreateInvitationCommandHandler
/// (feature 001).
/// </summary>
public class ResendStaffInvitationCommandHandler(
    ITenantDbContext db,
    ICurrentTenantService currentTenant,
    IEmailSender emailSender,
    IConfiguration config,
    IProfilePhotoStorage photoStorage) : IRequestHandler<ResendStaffInvitationCommand, StaffResult>
{
    public async Task<StaffResult> Handle(ResendStaffInvitationCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == request.StaffProfileId, cancellationToken);
        if (profile is null)
            return StaffResult.Fail(StaffFailure.NotFound);

        var user = await db.Users.FirstAsync(u => u.Id == profile.TenantUserId, cancellationToken);

        // Covers both "already accepted" and "director opt-in, no invitation ever needed" —
        // either way, an account with working credentials has nothing meaningful to resend.
        if (!string.IsNullOrEmpty(user.PasswordHash))
            return StaffResult.Fail(StaffFailure.AccountAlreadyActive);

        var now = DateTime.UtcNow;
        var stillPending = await db.StaffInvitations
            .Where(i => i.StaffProfileId == profile.Id && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var pending in stillPending)
            pending.ExpiresAt = now;

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        db.StaffInvitations.Add(new StaffInvitation
        {
            StaffProfileId = profile.Id,
            Email = user.Email,
            TokenHash = tokenHash,
            ExpiresAt = now.AddDays(7),
        });

        await db.SaveChangesAsync(cancellationToken);

        var inviteLink = StaffLinkBuilder.BuildInviteUrl(config, token, currentTenant.TenantSlug);
        await emailSender.SendStaffInvitationAsync(user.Email, inviteLink);

        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, eligibleLocationIds, photoUrl));
    }
}
