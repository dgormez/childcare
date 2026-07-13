namespace ChildCare.Contracts.Responses;

public record ChildHealthSummaryResponse(
    Guid ChildId,
    IReadOnlyList<HealthRecordResponse> ActiveHealthRecords,
    IReadOnlyList<ChildHealthSummaryVaccineFlag> DueSoonVaccines);

public record ChildHealthSummaryVaccineFlag(string VaccineName, DateOnly NextDueDate, bool IsOverdue);
