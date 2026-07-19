using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// One row per resolved recipient of a BulkEmailSend (feature 020, data-model.md R6) — mirrors
// AnnouncementRecipient/ClosureNotificationDelivery's per-recipient audit-row shape. Backs the
// director's post-send delivery-outcome summary (FR-012).
public class BulkEmailRecipient
{
    public Guid                    Id              { get; set; } = Guid.NewGuid();
    public Guid                    BulkEmailSendId { get; set; }
    public Guid                    ContactId       { get; set; }
    public BulkEmailDeliveryStatus Status          { get; set; }

    // Exception type name only when Status == ProviderFailure, never the raw provider message
    // (matches ClosureNotificationDelivery.Error's convention, CLAUDE.md's error-handling rule).
    public string?                 Error           { get; set; }

    public DateTime                CreatedAt       { get; set; } = DateTime.UtcNow;
}
