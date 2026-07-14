namespace ChildCare.Contracts.Responses;

// contracts/platform-admin-vaccine-types-api.md (feature 013h) — the platform-admin management
// view of a catalog entry, including audit fields 013g's tenant-facing VaccineTypeResponse never
// exposes.
public record PlatformAdminVaccineTypeResponse(
    Guid Id,
    string Name,
    string? Category,
    int SortOrder,
    bool IsActive,
    string? DeactivatedByEmail,
    DateTime? DeactivatedAt);
