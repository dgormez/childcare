namespace ChildCare.Contracts.Responses;

public record GroupActivityPhotoResponse(
    Guid Id,
    string? DownloadUrl,
    string? ThumbnailDownloadUrl,
    string? Caption,
    DateTime UploadedAt);

public record GroupActivityResponse(
    Guid Id,
    Guid GroupId,
    string ActivityType,
    string Title,
    string? Description,
    DateTime OccurredAt,
    IReadOnlyList<Guid> RecordedBy,
    IReadOnlyList<GroupActivityPhotoResponse> Photos,
    DateTime CreatedAt);

// Group timeline (research.md R4): a discriminated union of ChildEvent and GroupActivity
// entries for one (GroupId, date), ordered by OccurredAt. Kind distinguishes which shape the
// client should expect at ChildEvent/GroupActivity (mutually exclusive — exactly one is set).
public record GroupTimelineEntryResponse(
    string Kind, // "child_event" | "group_activity"
    DateTime OccurredAt,
    ChildEventResponse? ChildEvent,
    GroupActivityResponse? GroupActivity);

public record GroupTimelineResponse(IReadOnlyList<GroupTimelineEntryResponse> Entries);

// Parent daily-summary extension (research.md R5) — distinct shape from DailySummaryResponse's
// existing string-only `Activities` field (feature 013's per-child Activity-type descriptions).
public record GroupActivitySummaryItem(
    Guid Id,
    string ActivityType,
    string Title,
    string? Description,
    DateTime OccurredAt,
    IReadOnlyList<GroupActivityPhotoResponse> Photos);

public record GalleryItemResponse(
    Guid ActivityId,
    Guid GroupId,
    GroupActivityPhotoResponse Photo,
    DateTime OccurredAt);

public record GalleryResponse(IReadOnlyList<GalleryItemResponse> Items, bool HasConsent);
