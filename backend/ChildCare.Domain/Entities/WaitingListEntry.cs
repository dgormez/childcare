using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

public class WaitingListEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly? RequestedStartDate { get; set; }
    public int Priority { get; set; }
    public WaitingListStatus Status { get; set; } = WaitingListStatus.Waiting;
    public string? Notes { get; set; }

    // Set only via LinkChildToWaitingListEntryCommand (FR-010/FR-011), never auto-matched.
    public Guid? ChildId { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Feature 023 — public self-registration (spec.md FR-007/FR-008/FR-009). Existing rows
    // default to DirectorEntered so 012a's shipped behavior is unaffected. ReferenceCode/
    // SubmittedLocale are set only for SelfRegistered entries (data-model.md).
    public WaitingListEntrySource Source { get; set; } = WaitingListEntrySource.DirectorEntered;
    public string? ReferenceCode { get; set; }
    public string? SubmittedLocale { get; set; }

    // Feature 023 — tour-invitation state (spec.md FR-015/FR-016/FR-017). A single evolving set
    // of fields, not a history log (research.md R2) — an entry has at most one active
    // invitation at a time; re-sending overwrites TourProposedAt/TourInvitationSentAt and
    // resets TourInvitationStatus to Sent.
    public DateTime? TourProposedAt { get; set; }
    public TourInvitationStatus TourInvitationStatus { get; set; } = TourInvitationStatus.NotSent;
    public DateTime? TourInvitationSentAt { get; set; }
    public string? TourOutcome { get; set; }
}
