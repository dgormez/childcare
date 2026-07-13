namespace ChildCare.Contracts.Requests;

public record CreateVaccineRecordRequest(
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes);

public record UpdateVaccineRecordRequest(
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes);
