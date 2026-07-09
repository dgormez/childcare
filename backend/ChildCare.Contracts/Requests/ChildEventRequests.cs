using System.Text.Json;

namespace ChildCare.Contracts.Requests;

public record RecordChildEventRequest(
    // Client-generated, so an offline-created event keeps the same id across the sync boundary
    // (FR-013a idempotency) — server generates one if omitted (online-only client use).
    Guid? Id,
    Guid ChildId,
    string EventType,
    DateTime OccurredAt,
    DateTime? EndedAt,
    JsonElement Payload,
    bool VisibleToParent,
    // Set only after a successful POST /api/room-shifts/confirm-administrator call
    // (research.md R2); null means skipped or not applicable to this event type.
    Guid? AdministeredByStaffId);

public record UpdateChildEventRequest(
    DateTime? EndedAt,
    JsonElement? Payload,
    bool? VisibleToParent,
    Guid? AdministeredByStaffId);
