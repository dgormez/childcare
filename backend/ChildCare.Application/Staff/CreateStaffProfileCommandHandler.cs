using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Staff;

/// <summary>
/// Implements both creation paths in one transaction (research.md R5, R6). Two genuine failure
/// modes beyond FluentValidation are handled here rather than in the validator, since both
/// depend on a database round-trip whose outcome can change between validation and save
/// (concurrent requests): a unique-constraint violation on email (FR-008, two directors
/// inviting the same address at once) and a missing/wrong-role ExistingTenantUserId.
/// </summary>
public class CreateStaffProfileCommandHandler(
    ITenantDbContext db,
    ICurrentTenantService currentTenant,
    IEmailSender emailSender,
    IConfiguration config,
    ILogger<CreateStaffProfileCommandHandler> logger) : IRequestHandler<CreateStaffProfileCommand, StaffResult>
{
    public async Task<StaffResult> Handle(CreateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.ExistingTenantUserId is Guid existingId)
            return await CreateForExistingDirectorAsync(existingId, request, cancellationToken);

        return await CreateNewStaffAccountAsync(request, cancellationToken);
    }

    private async Task<StaffResult> CreateForExistingDirectorAsync(Guid existingId, CreateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await db.Users.FirstOrDefaultAsync(
            u => u.Id == existingId && u.Role == UserRole.Director, cancellationToken);
        if (existingUser is null)
            return StaffResult.Fail(StaffFailure.TenantUserNotFound);

        var profile = new StaffProfile
        {
            TenantUserId = existingUser.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            QualificationLevel = request.QualificationLevel,
        };

        db.StaffProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, existingUser, [], null));
    }

    private async Task<StaffResult> CreateNewStaffAccountAsync(CreateStaffProfileCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var tenantUser = new TenantUser
        {
            Email = email,
            PasswordHash = string.Empty, // set on accept-invitation (research.md R5)
            Name = $"{request.FirstName} {request.LastName}",
            Role = UserRole.Staff,
        };

        var profile = new StaffProfile
        {
            TenantUserId = tenantUser.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            QualificationLevel = request.QualificationLevel,
        };

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        var invitation = new StaffInvitation
        {
            StaffProfileId = profile.Id,
            Email = email,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        db.Users.Add(tenantUser);
        db.StaffProfiles.Add(profile);
        db.StaffInvitations.Add(invitation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return StaffResult.Fail(StaffFailure.EmailAlreadyExists);
        }

        try
        {
            var inviteLink = StaffLinkBuilder.BuildInviteUrl(config, token, currentTenant.TenantSlug);
            await emailSender.SendStaffInvitationAsync(email, inviteLink);
        }
        catch (Exception ex)
        {
            // FR-006: a failed send must not fail profile creation — logged, director can resend.
            logger.LogError(ex, "Failed to send staff invitation email to {Email}", email);
        }

        return StaffResult.Success(StaffMapper.ToResponse(profile, tenantUser, [], null));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is DbException { SqlState: "23505" };
}
