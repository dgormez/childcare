using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.WaitingList;

public record TransitionWaitingListStatusCommand(Guid Id, string Status) : IRequest<WaitingListEntryResult>;

public class TransitionWaitingListStatusCommandValidator : AbstractValidator<TransitionWaitingListStatusCommand>
{
    public TransitionWaitingListStatusCommandValidator()
    {
        RuleFor(x => x.Status).Must(s => WaitingListMapper.TryParseStatus(s, out _));
    }
}

/// <summary>
/// FR-007's explicit allow-list, enforced here rather than a DB CHECK constraint since the rule
/// depends on the *current* status (research.md R4), mirroring Contract.Status's handler-level
/// transition checks (feature 007).
/// </summary>
public class TransitionWaitingListStatusCommandHandler(ITenantDbContext db, IEmailSender emailSender, ILogger<TransitionWaitingListStatusCommandHandler> logger)
    : IRequestHandler<TransitionWaitingListStatusCommand, WaitingListEntryResult>
{
    private static readonly Dictionary<WaitingListStatus, WaitingListStatus[]> AllowedTransitions = new()
    {
        [WaitingListStatus.Waiting] = [WaitingListStatus.Offered, WaitingListStatus.Withdrawn],
        [WaitingListStatus.Offered] = [WaitingListStatus.Enrolled, WaitingListStatus.Withdrawn, WaitingListStatus.Waiting],
        [WaitingListStatus.Enrolled] = [],
        [WaitingListStatus.Withdrawn] = [],
    };

    public async Task<WaitingListEntryResult> Handle(TransitionWaitingListStatusCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return WaitingListEntryResult.Fail(WaitingListFailure.NotFound);

        WaitingListMapper.TryParseStatus(request.Status, out var targetStatus);

        if (!AllowedTransitions[entry.Status].Contains(targetStatus))
            return WaitingListEntryResult.Fail(WaitingListFailure.InvalidStatusTransition);

        var fromWaitingToOffered = entry.Status == WaitingListStatus.Waiting && targetStatus == WaitingListStatus.Offered;

        entry.Status = targetStatus;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        // FR-008/FR-009: email fires only on waiting -> offered, and only when a contact email
        // is on file; a missing email doesn't fail the request. No email for any other
        // transition, including the offered -> waiting revert.
        if (fromWaitingToOffered)
        {
            if (string.IsNullOrWhiteSpace(entry.ContactEmail))
            {
                logger.LogInformation("Waiting-list entry {EntryId} marked offered with no contact email on file — no notification sent.", entry.Id);
            }
            else
            {
                var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == entry.LocationId, cancellationToken);
                await emailSender.SendWaitingListOfferedAsync(
                    entry.ContactEmail,
                    entry.ContactName,
                    $"{entry.ChildFirstName} {entry.ChildLastName}",
                    location?.Name ?? string.Empty);
            }
        }

        var isDuplicate = await db.WaitingListEntries.AnyAsync(e =>
            e.LocationId == entry.LocationId &&
            e.Id != entry.Id &&
            e.ChildFirstName == entry.ChildFirstName &&
            e.ChildLastName == entry.ChildLastName &&
            e.DateOfBirth == entry.DateOfBirth,
            cancellationToken);

        return WaitingListEntryResult.Success(WaitingListMapper.ToResponse(entry, isDuplicate));
    }
}
