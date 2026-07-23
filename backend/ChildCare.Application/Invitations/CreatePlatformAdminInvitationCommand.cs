using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Invitations;

// research.md R16: a new command, distinct from the existing CreateInvitationCommand — that one
// backs the out-of-scope SuperAdmin ops-key endpoint (AdminEndpoints.cs) and must not change.
public record CreatePlatformAdminInvitationCommand(
    string Email, string? OrganisationNameNote, string? Locale, Guid ActingUserId, string ActingUserEmail)
    : IRequest<PlatformAdminInvitationResponse>;

public class CreatePlatformAdminInvitationCommandValidator : AbstractValidator<CreatePlatformAdminInvitationCommand>
{
    public CreatePlatformAdminInvitationCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.OrganisationNameNote).MaximumLength(200);
        RuleFor(x => x.Locale)
            .Must(l => string.IsNullOrEmpty(l) || l is "nl" or "fr" or "en")
            .WithMessage("errors.validation");
    }
}

public class CreatePlatformAdminInvitationCommandHandler(IPublicDbContext publicDb, IEmailSender emailSender, IConfiguration config)
    : IRequestHandler<CreatePlatformAdminInvitationCommand, PlatformAdminInvitationResponse>
{
    // Same 7-day validity as the existing CreateInvitationCommandHandler (feature 001) — no
    // reason for a platform-admin-issued invitation to expire on a different schedule.
    private static readonly TimeSpan InvitationValidity = TimeSpan.FromDays(7);

    public async Task<PlatformAdminInvitationResponse> Handle(CreatePlatformAdminInvitationCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var locale = string.IsNullOrEmpty(request.Locale) ? "nl" : request.Locale;
        var now = DateTime.UtcNow;

        await SupersedeExistingAsync(publicDb, email, request.ActingUserId, request.ActingUserEmail, now, cancellationToken);

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        var invitation = new Invitation
        {
            Email = email,
            TokenHash = tokenHash,
            ExpiresAt = now.Add(InvitationValidity),
            OrganisationNameNote = request.OrganisationNameNote,
            Locale = locale,
            CreatedByUserId = request.ActingUserId,
            CreatedByEmail = request.ActingUserEmail,
        };

        publicDb.Invitations.Add(invitation);
        await publicDb.SaveChangesAsync(cancellationToken);

        var registerUrl = OrganisationInvitationLinkBuilder.BuildRegisterUrl(config, token);
        await emailSender.SendOrganisationInvitationAsync(email, locale, request.OrganisationNameNote, registerUrl, cancellationToken);

        return PlatformAdminInvitationMapper.ToResponse(invitation, hasTenant: false);
    }

    // research.md R3/R14 — shared by create-duplicate-email and resend (ResendPlatformAdminInvitationCommand):
    // marks every still-usable (not Accepted, not already Revoked) invitation for this email as
    // Revoked, attributed to whichever platform-admin triggered the supersede. Accepted
    // invitations are left untouched — FR-007 forbids acting on them at all.
    internal static async Task SupersedeExistingAsync(
        IPublicDbContext publicDb, string email, Guid actingUserId, string actingUserEmail, DateTime now, CancellationToken cancellationToken)
    {
        var candidateIds = await publicDb.Invitations
            .Where(i => i.Email == email && i.RevokedAt == null)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);
        if (candidateIds.Count == 0) return;

        var acceptedIds = await publicDb.Tenants
            .Where(t => candidateIds.Contains(t.CreatedFromInvitationId))
            .Select(t => t.CreatedFromInvitationId)
            .ToListAsync(cancellationToken);

        var toSupersede = await publicDb.Invitations
            .Where(i => candidateIds.Contains(i.Id) && !acceptedIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        foreach (var superseded in toSupersede)
        {
            superseded.RevokedByUserId = actingUserId;
            superseded.RevokedByEmail = actingUserEmail;
            superseded.RevokedAt = now;
        }
    }
}
