namespace ChildCare.Contracts.Requests;

// VaccineTypeId (feature 013g) is appended last with a default so every pre-existing positional
// call site (this codebase's tests construct these records positionally) keeps compiling
// unchanged — JSON (de)serialization binds by property name, not position, so this has no
// effect on the wire contract (see contracts/vaccine-catalog-api.md).
public record CreateVaccineRecordRequest(
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes,
    Guid? VaccineTypeId = null);

public record UpdateVaccineRecordRequest(
    string VaccineName,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes,
    Guid? VaccineTypeId = null);

public record CreateVaccineRecordAttachmentUploadUrlRequest(string ContentType);
