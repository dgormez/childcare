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

// Feature 009c — contracts/child-events-batch-api.md. `Items` carries a client-generated `Id`
// per child (research.md R5) so a retried offline-queue replay is idempotent per child, mirroring
// RecordChildEventRequest's own `Id` field/behavior rather than inventing a new mechanism.
public record RecordChildEventBatchRequest(
    IReadOnlyList<ChildEventBatchItemRequest> Items,
    string EventType,
    DateTime OccurredAt,
    DateTime? EndedAt,
    JsonElement Payload,
    bool VisibleToParent);

public record ChildEventBatchItemRequest(Guid ChildId, Guid Id);
