using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

public record CreateHealthRecordAttachmentUploadUrlCommand(Guid ChildId, Guid Id, string ContentType)
    : IRequest<HealthRecordAttachmentUploadUrlResult>;

public class HealthRecordAttachmentUploadUrlResult
{
    public string? UploadUrl { get; private init; }
    public HealthRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static HealthRecordAttachmentUploadUrlResult Success(string uploadUrl) => new() { UploadUrl = uploadUrl };
    public static HealthRecordAttachmentUploadUrlResult Fail(HealthRecordFailure failure) => new() { Failure = failure };
}

public class CreateHealthRecordAttachmentUploadUrlCommandHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<CreateHealthRecordAttachmentUploadUrlCommand, HealthRecordAttachmentUploadUrlResult>
{
    private static readonly HashSet<string> AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<HealthRecordAttachmentUploadUrlResult> Handle(CreateHealthRecordAttachmentUploadUrlCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(request.ContentType))
            return HealthRecordAttachmentUploadUrlResult.Fail(HealthRecordFailure.InvalidContentType);

        var record = await db.HealthRecords
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.ChildId == request.ChildId && r.DeletedAt == null, cancellationToken);
        if (record is null)
            return HealthRecordAttachmentUploadUrlResult.Fail(HealthRecordFailure.NotFound);

        // FR-006: the attachment location is set the moment an upload URL is issued, not after
        // upload completion — a client-side upload that never finishes just leaves the record
        // pointing at an object that doesn't exist yet (spec.md Assumptions on partial-success).
        var (objectPath, uploadUrl) = await storage.CreateUploadUrlAsync(record.Id, request.ContentType, cancellationToken: cancellationToken);
        record.AttachmentObjectPath = objectPath;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return HealthRecordAttachmentUploadUrlResult.Success(uploadUrl);
    }
}
