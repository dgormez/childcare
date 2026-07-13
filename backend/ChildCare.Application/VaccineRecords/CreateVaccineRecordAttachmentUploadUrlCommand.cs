using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

public record CreateVaccineRecordAttachmentUploadUrlCommand(Guid ChildId, Guid Id, string ContentType)
    : IRequest<VaccineRecordAttachmentUploadUrlResult>;

public class VaccineRecordAttachmentUploadUrlResult
{
    public string? UploadUrl { get; private init; }
    public VaccineRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static VaccineRecordAttachmentUploadUrlResult Success(string uploadUrl) => new() { UploadUrl = uploadUrl };
    public static VaccineRecordAttachmentUploadUrlResult Fail(VaccineRecordFailure failure) => new() { Failure = failure };
}

// Mirrors CreateHealthRecordAttachmentUploadUrlCommand (013c) exactly, with category:
// "vaccine-records" (research.md R4) keeping the two attachment kinds in distinct object paths
// within the same bucket/port.
public class CreateVaccineRecordAttachmentUploadUrlCommandHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<CreateVaccineRecordAttachmentUploadUrlCommand, VaccineRecordAttachmentUploadUrlResult>
{
    private static readonly HashSet<string> AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<VaccineRecordAttachmentUploadUrlResult> Handle(CreateVaccineRecordAttachmentUploadUrlCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(request.ContentType))
            return VaccineRecordAttachmentUploadUrlResult.Fail(VaccineRecordFailure.InvalidContentType);

        var record = await db.VaccineRecords
            .SingleOrDefaultAsync(v => v.Id == request.Id && v.ChildId == request.ChildId && v.DeletedAt == null, cancellationToken);
        if (record is null)
            return VaccineRecordAttachmentUploadUrlResult.Fail(VaccineRecordFailure.NotFound);

        // spec.md FR-012: the attachment location is set the moment an upload URL is issued, not
        // after upload completion — a client-side upload that never finishes just leaves the
        // record pointing at an object that doesn't exist yet, treated identically to "no
        // attachment" (spec.md Edge Cases).
        var (objectPath, uploadUrl) = await storage.CreateUploadUrlAsync(record.Id, request.ContentType, category: "vaccine-records", cancellationToken: cancellationToken);
        record.AttachmentObjectPath = objectPath;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordAttachmentUploadUrlResult.Success(uploadUrl);
    }
}
