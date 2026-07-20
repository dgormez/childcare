using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Children;

// 031-photo-lifecycle-governance FR-008/FR-009/FR-010/FR-016/FR-017 — StaffOrDirector (per
// FR-008, "director or staff member"), never automatic. Deletion is deliberate and requires the
// child to already be deactivated; a group-activity photo is deleted only when the targeted
// child is its sole depicted child (IGroupActivityChildDerivationService), so a photo still
// depicting another child — active or not-yet-purged — is always preserved.
public record PurgeChildPhotosCommand(Guid ChildId, Guid ActorTenantUserId, string ActorRole) : IRequest<PurgePhotosResult>;

public enum PurgePhotosFailure
{
    NotFound,
    ChildStillActive,
}

public class PurgePhotosResult
{
    public bool Succeeded { get; private init; }
    public PurgePhotosFailure? Failure { get; private init; }
    public IReadOnlyList<string> DeletedObjectPaths { get; private init; } = [];
    public IReadOnlyList<string> FailedObjectPaths { get; private init; } = [];
    public int PreservedGroupPhotoCount { get; private init; }

    public static PurgePhotosResult Fail(PurgePhotosFailure failure) => new() { Succeeded = false, Failure = failure };

    public static PurgePhotosResult Complete(IReadOnlyList<string> deleted, IReadOnlyList<string> failed, int preservedGroupPhotoCount) => new()
    {
        // A non-empty FailedObjectPaths list is still Succeeded=true at the command-result level
        // (the cascade ran; the *request* succeeded) — FR-016 requires the caller to render this
        // as a failure state regardless, which the endpoint/UI layer does by inspecting
        // FailedObjectPaths, not by inventing a separate HTTP status for "partial."
        Succeeded = true,
        DeletedObjectPaths = deleted,
        FailedObjectPaths = failed,
        PreservedGroupPhotoCount = preservedGroupPhotoCount,
    };
}

public class PurgeChildPhotosCommandHandler(
    ITenantDbContext db,
    IProfilePhotoStorage profilePhotoStorage,
    IGroupActivityPhotoStorage groupActivityPhotoStorage,
    IHealthAttachmentStorage healthAttachmentStorage,
    IGroupActivityChildDerivationService derivationService,
    ILogger<PurgeChildPhotosCommandHandler> logger)
    : IRequestHandler<PurgeChildPhotosCommand, PurgePhotosResult>
{
    public async Task<PurgePhotosResult> Handle(PurgeChildPhotosCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return PurgePhotosResult.Fail(PurgePhotosFailure.NotFound);

        if (child.DeactivatedAt is null)
            return PurgePhotosResult.Fail(PurgePhotosFailure.ChildStillActive);

        var deleted = new List<string>();
        var failed = new List<string>();

        if (child.ProfilePhotoObjectPath is { } profilePhotoPath)
        {
            if (await profilePhotoStorage.DeleteAsync(profilePhotoPath, cancellationToken))
            {
                deleted.Add(profilePhotoPath);
                child.ProfilePhotoObjectPath = null;
            }
            else
            {
                failed.Add(profilePhotoPath);
            }
        }

        var healthRecords = await db.HealthRecords
            .Where(r => r.ChildId == request.ChildId && r.AttachmentObjectPath != null)
            .ToListAsync(cancellationToken);
        foreach (var record in healthRecords)
        {
            var path = record.AttachmentObjectPath!;
            if (await healthAttachmentStorage.DeleteAsync(path, cancellationToken))
            {
                deleted.Add(path);
                record.AttachmentObjectPath = null;
            }
            else
            {
                failed.Add(path);
            }
        }

        var vaccineRecords = await db.VaccineRecords
            .Where(r => r.ChildId == request.ChildId && r.AttachmentObjectPath != null)
            .ToListAsync(cancellationToken);
        foreach (var record in vaccineRecords)
        {
            var path = record.AttachmentObjectPath!;
            if (await healthAttachmentStorage.DeleteAsync(path, cancellationToken))
            {
                deleted.Add(path);
                record.AttachmentObjectPath = null;
            }
            else
            {
                failed.Add(path);
            }
        }

        // Every group-activity photo whose group any of this child's ChildGroupAssignments ever
        // overlapped — a superset that's cheap to query; the exact per-photo depicted-child
        // check (the actual FR-010 safety gate) still runs individually below.
        var candidateGroupIds = await db.ChildGroupAssignments
            .Where(a => a.ChildId == request.ChildId)
            .Select(a => a.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidatePhotos = await db.GroupActivityPhotos
            .Join(db.GroupActivities, p => p.GroupActivityId, a => a.Id, (p, a) => new { Photo = p, Activity = a })
            .Where(x => candidateGroupIds.Contains(x.Activity.GroupId))
            .ToListAsync(cancellationToken);

        var preservedGroupPhotoCount = 0;
        foreach (var candidate in candidatePhotos)
        {
            var depictedChildIds = await derivationService.GetDepictedChildIdsAsync(candidate.Activity.Id, cancellationToken);
            if (depictedChildIds.Count != 1 || depictedChildIds[0] != request.ChildId)
            {
                if (depictedChildIds.Contains(request.ChildId))
                    preservedGroupPhotoCount++;
                continue;
            }

            if (await groupActivityPhotoStorage.DeleteAsync(candidate.Photo.ObjectPath, candidate.Photo.ThumbnailObjectPath, cancellationToken))
            {
                deleted.Add(candidate.Photo.ObjectPath);
                deleted.Add(candidate.Photo.ThumbnailObjectPath);
                db.GroupActivityPhotos.Remove(candidate.Photo);
            }
            else
            {
                failed.Add(candidate.Photo.ObjectPath);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (failed.Count > 0)
        {
            logger.LogWarning(
                "Photo purge: tenant user {ActorTenantUserId} ({ActorRole}) purged child {ChildId} — {DeletedCount} object(s) deleted, {FailedCount} failed.",
                request.ActorTenantUserId, request.ActorRole, request.ChildId, deleted.Count, failed.Count);
        }
        else
        {
            logger.LogInformation(
                "Photo purge: tenant user {ActorTenantUserId} ({ActorRole}) purged child {ChildId} — {DeletedCount} object(s) deleted, {FailedCount} failed.",
                request.ActorTenantUserId, request.ActorRole, request.ChildId, deleted.Count, failed.Count);
        }

        return PurgePhotosResult.Complete(deleted, failed, preservedGroupPhotoCount);
    }
}
