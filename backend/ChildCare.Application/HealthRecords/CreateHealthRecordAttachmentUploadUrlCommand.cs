using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

// CallerRole/CallerTenantUserId (031-photo-lifecycle-governance FR-011): staff must be scoped
// to their assigned location(s) — reusing GetChildByIdQuery's StaffLocationEligibility check.
public record CreateHealthRecordAttachmentUploadUrlCommand(
    Guid ChildId, Guid Id, string ContentType, string? CallerRole = null, Guid? CallerTenantUserId = null)
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

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            var isInScope = await db.ChildGroupAssignments
                .Where(a => a.ChildId == request.ChildId && a.EndDate == null)
                .Join(db.Groups, a => a.GroupId, g => g.Id, (a, g) => g.LocationId)
                .AnyAsync(locationId => eligibleLocationIds.Contains(locationId), cancellationToken);
            if (!isInScope)
                return HealthRecordAttachmentUploadUrlResult.Fail(HealthRecordFailure.NotFound);
        }

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
