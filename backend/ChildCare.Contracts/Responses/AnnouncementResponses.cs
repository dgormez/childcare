namespace ChildCare.Contracts.Responses;

public record AnnouncementResponse(
    Guid Id,
    Guid LocationId,
    Guid? GroupId,
    string Subject,
    string Body,
    Guid SentByTenantUserId,
    DateTime SentAt,
    int RecipientCount);

public record ParentAnnouncementResponse(
    Guid Id,
    string Subject,
    string Body,
    DateTime SentAt,
    DateTime? ReadAt);
