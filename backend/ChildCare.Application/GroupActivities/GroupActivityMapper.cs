using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.GroupActivities;

// Unlike ChildEventMapper (static, synchronous — ChildEvent has no photos), this mapper resolves
// signed GCS download URLs per photo, so it's an injectable, async instance class.
public class GroupActivityMapper(IGroupActivityPhotoStorage photoStorage)
{
    public async Task<GroupActivityResponse> ToResponseAsync(
        GroupActivity activity, IReadOnlyList<GroupActivityPhoto> photos, CancellationToken cancellationToken = default)
    {
        var photoResponses = new List<GroupActivityPhotoResponse>(photos.Count);
        foreach (var photo in photos)
            photoResponses.Add(await ToPhotoResponseAsync(photo, cancellationToken));

        return new GroupActivityResponse(
            activity.Id,
            activity.GroupId,
            activity.ActivityType.ToWireString(),
            activity.Title,
            activity.Description,
            activity.OccurredAt,
            activity.RecordedBy,
            photoResponses,
            activity.CreatedAt);
    }

    public async Task<GroupActivityPhotoResponse> ToPhotoResponseAsync(
        GroupActivityPhoto photo, CancellationToken cancellationToken = default)
    {
        var downloadUrl = await photoStorage.CreateDownloadUrlAsync(photo.ObjectPath, cancellationToken);
        var thumbnailUrl = await photoStorage.CreateDownloadUrlAsync(photo.ThumbnailObjectPath, cancellationToken);
        return new GroupActivityPhotoResponse(photo.Id, downloadUrl, thumbnailUrl, photo.Caption, photo.UploadedAt);
    }
}
