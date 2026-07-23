namespace ChildCare.Contracts.Responses;

public record StaffMeResponse(
    Guid StaffProfileId,
    string FirstName,
    string LastName,
    string Role,
    IReadOnlyList<Guid> EligibleLocationIds,
    // Feature 028 (FR-005/FR-010) — lets staff-mobile decide client-side whether the clock-in
    // function picker is needed, mirroring how EligibleLocationIds already drives the same
    // decision for location.
    IReadOnlyList<string> TimeEntryFunctions);
