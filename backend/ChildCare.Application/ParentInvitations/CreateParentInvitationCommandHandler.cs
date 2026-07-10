using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.ParentInvitations;

public class CreateParentInvitationCommandHandler(
    ITenantDbContext db,
    ICurrentTenantService currentTenant,
    IEmailSender emailSender,
    IConfiguration config,
    ILogger<CreateParentInvitationCommandHandler> logger) : IRequestHandler<CreateParentInvitationCommand, ParentInvitationResult>
{
    public async Task<ParentInvitationResult> Handle(CreateParentInvitationCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken);
        if (contact is null)
            return ParentInvitationResult.Fail(ParentInvitationFailure.ContactNotFound);

        // FR-000c: a genuine 409, not a validator rule — see CreateParentInvitationCommandValidator's
        // comment for why (mirrors StaffFailure.EmailAlreadyExists' precedent).
        if (contact.TenantUserId is not null)
            return ParentInvitationResult.Fail(ParentInvitationFailure.AlreadyHasAccount);

        var email = contact.Email!.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        // Re-inviting a still-pending contact supersedes any prior still-valid invitation,
        // mirroring CreateInvitationCommandHandler's feature-001 precedent — never more than one
        // usable invitation per contact at a time.
        var stillPending = await db.ParentInvitations
            .Where(i => i.ContactId == contact.Id && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var pending in stillPending)
            pending.ExpiresAt = now;

        var tenantUser = new TenantUser
        {
            Email = email,
            PasswordHash = string.Empty, // set on accept-invitation
            Name = $"{contact.FirstName} {contact.LastName}",
            Role = UserRole.Parent,
        };

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        var expiresAt = now.AddDays(7);

        var invitation = new ParentInvitation
        {
            ContactId = contact.Id,
            Email = email,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };

        db.Users.Add(tenantUser);
        db.ParentInvitations.Add(invitation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return ParentInvitationResult.Fail(ParentInvitationFailure.AlreadyHasAccount);
        }

        try
        {
            var inviteLink = ParentLinkBuilder.BuildInviteUrl(config, token, currentTenant.TenantSlug);
            await emailSender.SendParentInvitationAsync(email, inviteLink);
        }
        catch (Exception ex)
        {
            // A failed send must not fail invitation creation — logged, director can resend.
            logger.LogError(ex, "Failed to send parent invitation email to {Email}", email);
        }

        return ParentInvitationResult.Success(new ParentInvitationResponse(invitation.Id, contact.Id, email, expiresAt));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is System.Data.Common.DbException { SqlState: "23505" };
}
