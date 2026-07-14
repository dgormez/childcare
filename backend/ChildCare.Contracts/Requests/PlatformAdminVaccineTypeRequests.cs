namespace ChildCare.Contracts.Requests;

// contracts/platform-admin-vaccine-types-api.md (feature 013h).
public record CreateVaccineTypeRequest(string Name, string? Category);

public record UpdateVaccineTypeRequest(string Name, string? Category);

/// <summary>Direction is "up" or "down".</summary>
public record ReorderVaccineTypeRequest(string Direction);
