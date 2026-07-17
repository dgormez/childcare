namespace ChildCare.Contracts.Requests;

// Feature 016 — contracts/developmental-milestones-api.md. Status is validated against the
// fixed emerging/achieved/not_yet set in the Application layer (FR-012), not here.
public record RecordMilestoneObservationRequest(
    Guid MilestoneId,
    string Status,
    DateOnly ObservedAt,
    string? Notes);
