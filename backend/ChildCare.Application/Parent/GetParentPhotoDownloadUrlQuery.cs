using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Parent;

// 031-photo-lifecycle-governance FR-012/FR-013 — "profile" or "group-activity" only. Health/
// vaccine attachments are deliberately excluded: parents have no visibility into health records
// anywhere in this API today, and neither spec.md's User Story 2 nor FR-012 mention medical
// documents, only photos (contracts/photo-lifecycle-api.md).
public enum ParentPhotoType { Profile, GroupActivity }

public record GetParentPhotoDownloadUrlQuery(Guid TenantUserId, ParentPhotoType PhotoType, Guid ObjectRef) : IRequest<ParentPhotoDownloadResult>;

public enum ParentPhotoDownloadFailure { Forbidden, NotFound }

public class ParentPhotoDownloadResult
{
    // 15-minute TTL matches DownloadUrlDuration in the Gcs*Storage classes signing the URL.
    private static readonly TimeSpan UrlDuration = TimeSpan.FromMinutes(15);

    public bool Succeeded { get; private init; }
    public string? DownloadUrl { get; private init; }
    public DateTime? ExpiresAt { get; private init; }
    public ParentPhotoDownloadFailure? Failure { get; private init; }

    public static ParentPhotoDownloadResult Ok(string downloadUrl) => new()
    {
        Succeeded = true,
        DownloadUrl = downloadUrl,
        ExpiresAt = DateTime.UtcNow.Add(UrlDuration),
    };

    public static ParentPhotoDownloadResult Fail(ParentPhotoDownloadFailure failure) => new() { Succeeded = false, Failure = failure };
}

public class GetParentPhotoDownloadUrlQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IProfilePhotoStorage profilePhotoStorage,
    IGroupActivityPhotoStorage groupActivityPhotoStorage,
    IGroupActivityChildDerivationService derivationService)
    : IRequestHandler<GetParentPhotoDownloadUrlQuery, ParentPhotoDownloadResult>
{
    public async Task<ParentPhotoDownloadResult> Handle(GetParentPhotoDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.Forbidden);

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        return request.PhotoType switch
        {
            ParentPhotoType.Profile => await HandleProfileAsync(request.ObjectRef, childIds, cancellationToken),
            ParentPhotoType.GroupActivity => await HandleGroupActivityAsync(request.ObjectRef, childIds, cancellationToken),
            _ => ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.NotFound),
        };
    }

    private async Task<ParentPhotoDownloadResult> HandleProfileAsync(Guid childId, List<Guid> childIds, CancellationToken cancellationToken)
    {
        if (!childIds.Contains(childId))
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.Forbidden);

        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childId, cancellationToken);
        if (child?.ProfilePhotoObjectPath is null)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.NotFound);

        var fileName = $"{child.FirstName}-{child.LastName}-photo.jpg";
        var url = await profilePhotoStorage.CreateAttachmentDownloadUrlAsync(child.ProfilePhotoObjectPath, fileName, cancellationToken);
        return ParentPhotoDownloadResult.Ok(url);
    }

    private async Task<ParentPhotoDownloadResult> HandleGroupActivityAsync(Guid photoId, List<Guid> childIds, CancellationToken cancellationToken)
    {
        if (childIds.Count == 0)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.Forbidden);

        var photo = await db.GroupActivityPhotos.FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        if (photo is null)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.NotFound);

        var activity = await db.GroupActivities.FirstOrDefaultAsync(a => a.Id == photo.GroupActivityId, cancellationToken);
        if (activity is null)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.NotFound);

        // Same two gates GetParentGroupActivityGalleryQuery applies: the parent's child must be
        // among the derived depicted children, and an active contract at the activity's location
        // must have photos_internal consent (research.md R6 / contracts/group-activities-api.md).
        var depictedChildIds = await derivationService.GetDepictedChildIdsAsync(activity.Id, cancellationToken);
        if (!depictedChildIds.Any(childIds.Contains))
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.Forbidden);

        var hasConsent = await db.Contracts.AnyAsync(
            c => childIds.Contains(c.ChildId) && c.Status == ContractStatus.Active
                && c.LocationId == activity.LocationId && c.Consent.PhotosInternal,
            cancellationToken);
        if (!hasConsent)
            return ParentPhotoDownloadResult.Fail(ParentPhotoDownloadFailure.Forbidden);

        var url = await groupActivityPhotoStorage.CreateAttachmentDownloadUrlAsync(photo.ObjectPath, $"{activity.Title}-photo.jpg", cancellationToken);
        return ParentPhotoDownloadResult.Ok(url);
    }
}
