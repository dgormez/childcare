using System.Text.Json;

namespace ChildCare.Contracts.Responses;

public record ChildEventResponse(
    Guid Id,
    Guid ChildId,
    string EventType,
    DateTime OccurredAt,
    DateTime? EndedAt,
    JsonElement Payload,
    bool VisibleToParent,
    IReadOnlyList<Guid> RecordedBy,
    Guid? AdministeredBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PagedChildEventsResponse(
    IReadOnlyList<ChildEventResponse> Items,
    // Opaque cursor for the next page (research.md R6); null = no more pages.
    string? NextCursor);

// Feature 009c — contracts/child-events-batch-api.md. Always `200`, whether every child
// succeeded or some failed (never 207) — a `child_id` never appears in both arrays.
public record ChildEventBatchResponse(
    IReadOnlyList<ChildEventBatchCreatedItem> Created,
    IReadOnlyList<ChildEventBatchErrorItem> Errors);

public record ChildEventBatchCreatedItem(Guid ChildId, Guid EventId);

public record ChildEventBatchErrorItem(Guid ChildId, string Reason);

public record DailySummaryResponse(
    int NapsCount,
    int BottlesCount,
    int DiaperChangesCount,
    string? LatestMood,
    decimal? LatestTemperatureCelsius,
    bool MedicationAdministered,
    // Feature 013: today's Activity-type event descriptions, oldest first. Photos are
    // explicitly out of scope (specs/013-parent-communication/spec.md Clarifications) — no
    // photo-attachment mechanism exists in the domain yet.
    IReadOnlyList<string> Activities,
    // Feature 009b (research.md R5): today's GroupActivity entries for the child's group,
    // consent-filtered photos (research.md R6). Named distinctly from `Activities` above — that
    // field is an unrelated, already-shipped per-child concept.
    IReadOnlyList<GroupActivitySummaryItem> GroupActivities);
