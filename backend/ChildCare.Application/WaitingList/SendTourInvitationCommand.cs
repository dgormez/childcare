using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.WaitingList;

public record SendTourInvitationCommand(Guid Id, DateTime ProposedAt) : IRequest<WaitingListEntryResult>;

public class SendTourInvitationCommandValidator : AbstractValidator<SendTourInvitationCommand>
{
    public SendTourInvitationCommandValidator()
    {
        RuleFor(x => x.ProposedAt).NotEmpty();
    }
}

/// <summary>
/// FR-015. Re-sending (an entry that already has an invitation) overwrites the previous
/// proposal and resets `TourInvitationStatus` to `Sent` rather than accumulating a history
/// (research.md R2). Locale: the entry's `SubmittedLocale` if self-registered, else the
/// location's `DefaultEnrollmentLocale` (director-entered entries have no submitted language).
/// </summary>
public class SendTourInvitationCommandHandler(
    ITenantDbContext db,
    ICurrentTenantService currentTenant,
    ITourInvitationTokenService tokenService,
    IEmailSender emailSender,
    IConfiguration config) : IRequestHandler<SendTourInvitationCommand, WaitingListEntryResult>
{
    public async Task<WaitingListEntryResult> Handle(SendTourInvitationCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return WaitingListEntryResult.Fail(WaitingListFailure.NotFound);

        if (string.IsNullOrWhiteSpace(entry.ContactEmail))
            return WaitingListEntryResult.Fail(WaitingListFailure.NoContactEmail);

        var location = await db.Locations.FirstAsync(l => l.Id == entry.LocationId, cancellationToken);
        var locale = entry.SubmittedLocale ?? location.DefaultEnrollmentLocale;

        var token = tokenService.CreateToken(entry.Id);
        var acceptUrl = EnrollmentLinkBuilder.BuildTourResponseUrl(config, token, currentTenant.TenantSlug, "accepted");
        var declineUrl = EnrollmentLinkBuilder.BuildTourResponseUrl(config, token, currentTenant.TenantSlug, "declined");

        await emailSender.SendTourInvitationAsync(
            entry.ContactEmail!, locale, $"{entry.ChildFirstName} {entry.ChildLastName}", location.Name,
            request.ProposedAt, acceptUrl, declineUrl, cancellationToken);

        entry.TourProposedAt = request.ProposedAt;
        entry.TourInvitationSentAt = DateTime.UtcNow;
        entry.TourInvitationStatus = Domain.Enums.TourInvitationStatus.Sent;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var isDuplicate = await db.WaitingListEntries.AnyAsync(e =>
            e.LocationId == entry.LocationId && e.Id != entry.Id &&
            e.ChildFirstName == entry.ChildFirstName && e.ChildLastName == entry.ChildLastName && e.DateOfBirth == entry.DateOfBirth,
            cancellationToken);

        return WaitingListEntryResult.Success(WaitingListMapper.ToResponse(entry, isDuplicate));
    }
}
