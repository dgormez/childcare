namespace ChildCare.Contracts.Responses;

public record VaccineRecordResponse(
    Guid Id,
    Guid ChildId,
    string VaccineName,
    Guid? VaccineTypeId,
    string? AttachmentDownloadUrl,
    int? DoseNumber,
    DateOnly AdministeredOn,
    DateOnly? NextDueDate,
    string? AdministeredBy,
    string? Notes,
    Guid? RecordedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateVaccineRecordAttachmentUploadUrlResponse(string UploadUrl, int ExpiresInSeconds);

public record VaccinationsDueSoonResponse(
    Guid ChildId,
    string ChildName,
    Guid LocationId,
    string VaccineName,
    DateOnly NextDueDate,
    bool IsOverdue);
