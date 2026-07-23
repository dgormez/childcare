using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.StaffDocuments;

public enum StaffDocumentFailure
{
    NotFound,
    StaffNotFound,
    InvalidDocumentType,
}

public class StaffDocumentResult
{
    public bool Succeeded { get; init; }
    public StaffDocumentFailure? Failure { get; init; }
    public StaffDocumentResponse? Response { get; init; }

    public static StaffDocumentResult Success(StaffDocumentResponse response) => new() { Succeeded = true, Response = response };
    public static StaffDocumentResult Fail(StaffDocumentFailure failure) => new() { Succeeded = false, Failure = failure };
}

public static class StaffDocumentMapper
{
    public static StaffDocumentResponse ToResponse(StaffDocument document, string? downloadUrl) => new(
        document.Id,
        document.StaffProfileId,
        document.DocumentType.ToWireString(),
        document.Title,
        downloadUrl,
        document.ValidFrom,
        document.ValidUntil,
        document.CreatedAt);
}
