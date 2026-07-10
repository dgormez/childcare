namespace ChildCare.Contracts.Responses;

public record MessageResponse(
    Guid Id,
    Guid ThreadId,
    Guid SenderId,
    string SenderName,
    string Body,
    DateTime SentAt,
    DateTime? ReadAt);

public record MessageThreadResponse(
    Guid Id,
    string Subject,
    Guid? ChildId,
    string? ChildName,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    bool HasUnread,
    IReadOnlyList<MessageResponse> Messages);

public record MessageThreadSummaryResponse(
    Guid Id,
    string Subject,
    Guid? ChildId,
    string? ChildName,
    DateTime LastActivityAt,
    bool HasUnread,
    int UnreadFromParentCount);
