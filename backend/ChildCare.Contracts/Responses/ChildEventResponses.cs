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

public record DailySummaryResponse(
    int NapsCount,
    int BottlesCount,
    int DiaperChangesCount,
    string? LatestMood,
    decimal? LatestTemperatureCelsius,
    bool MedicationAdministered);
