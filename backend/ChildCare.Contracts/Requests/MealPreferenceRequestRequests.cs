namespace ChildCare.Contracts.Requests;

// Feature 013e — contracts/monthly-menu-api.md. At least one of NewTexture/NewDietaryType is
// required (enforced by the command validator — a request that changes neither is meaningless).
public record SubmitMealPreferenceChangeRequestRequest(
    string? NewTexture,
    List<string>? NewDietaryType,
    string? Notes);

public record RejectMealPreferenceChangeRequestRequest(
    string? Reason);
