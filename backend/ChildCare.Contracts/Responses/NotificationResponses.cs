namespace ChildCare.Contracts.Responses;

public record NotificationResponse(
    Guid Id,
    string Type,
    Guid SourceId,
    string TitleKey,
    string BodyKey,
    string ArgumentsJson,
    DateTime CreatedAt,
    DateTime? ReadAt);
