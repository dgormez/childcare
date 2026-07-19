namespace ChildCare.Domain.Entities;

// One row per director-initiated bulk send (feature 020, data-model.md). Realizes spec.md's
// conceptual "BulkEmailAttachment" as columns here (1:1, mirrors HealthRecord.AttachmentObjectPath's
// existing single-column precedent) rather than a separate child table.
public class BulkEmailSend
{
    public Guid     Id                     { get; set; } = Guid.NewGuid();
    public Guid     LocationId             { get; set; }

    // Null = whole-location scope; set = group-scoped (spec.md FR-001), mirrors Announcement.GroupId.
    public Guid?    GroupId                { get; set; }

    public string   Subject                { get; set; } = string.Empty;
    public string   Body                   { get; set; } = string.Empty;

    public string?  AttachmentObjectPath   { get; set; }
    public string?  AttachmentFileName     { get; set; }
    public string?  AttachmentContentType  { get; set; }

    public Guid     SentByTenantUserId     { get; set; }
    public DateTime SentAt                 { get; set; } = DateTime.UtcNow;
}
