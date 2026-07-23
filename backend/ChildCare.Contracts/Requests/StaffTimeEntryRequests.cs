namespace ChildCare.Contracts.Requests;

// function is a wire string (one of "kinderbegeleider"/"logistiek"/"verantwoordelijke"); null
// when the caller has exactly one configured function (FR-005 — server auto-selects it).
public record ClockInRequest(Guid LocationId, Guid? GroupId, string? Function);

public record UpdateStaffTimeEntryRequest(DateTime? ClockedOutAt, string? Function, Guid? GroupId, string? Notes);

public record UpdateStaffTimeEntryFunctionsRequest(IReadOnlyList<string> Functions);
