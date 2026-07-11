namespace ChildCare.Contracts.Requests;

public record CreateGroupActivityRequest(
    // Client-generated, so an offline-created activity keeps the same id across the sync
    // boundary (idempotent create, mirrors RecordChildEventRequest's FR-013a precedent).
    Guid? Id,
    string ActivityType,
    string Title,
    string? Description,
    // Client-captured at creation time (no manual time picker per spec.md's Assumptions, but
    // still client-supplied — not server-assigned — so an offline-queued activity keeps its
    // real capture moment rather than being timestamped at sync time; mirrors
    // RecordChildEventRequest.OccurredAt).
    DateTime OccurredAt);
