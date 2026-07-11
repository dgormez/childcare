using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

public record UploadGroupActivityPhotoCommand(
    Guid GroupActivityId,
    Stream ImageBytes,
    long FileSizeBytes,
    string? Caption) : IRequest<GroupActivityPhotoResult>;

public class UploadGroupActivityPhotoCommandValidator : AbstractValidator<UploadGroupActivityPhotoCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // FR-003: 10MB before resize.

    public UploadGroupActivityPhotoCommandValidator()
    {
        RuleFor(x => x.GroupActivityId).NotEmpty();
        RuleFor(x => x.Caption).MaximumLength(500);
    }

    public static bool IsTooLarge(long fileSizeBytes) => fileSizeBytes > MaxFileSizeBytes;
}

public class UploadGroupActivityPhotoCommandHandler(
    ITenantDbContext db,
    IGroupActivityPhotoStorage photoStorage,
    GroupActivityMapper mapper)
    : IRequestHandler<UploadGroupActivityPhotoCommand, GroupActivityPhotoResult>
{
    private const int MaxPhotosPerActivity = 10; // FR-003

    public async Task<GroupActivityPhotoResult> Handle(UploadGroupActivityPhotoCommand request, CancellationToken cancellationToken)
    {
        if (UploadGroupActivityPhotoCommandValidator.IsTooLarge(request.FileSizeBytes))
            return GroupActivityPhotoResult.Fail(GroupActivityFailure.PhotoTooLarge);

        var activityExists = await db.GroupActivities.AnyAsync(a => a.Id == request.GroupActivityId, cancellationToken);
        if (!activityExists)
            return GroupActivityPhotoResult.Fail(GroupActivityFailure.NotFound);

        var existingPhotoCount = await db.GroupActivityPhotos
            .CountAsync(p => p.GroupActivityId == request.GroupActivityId, cancellationToken);
        if (existingPhotoCount >= MaxPhotosPerActivity)
            return GroupActivityPhotoResult.Fail(GroupActivityFailure.PhotoLimitReached);

        var photoId = Guid.NewGuid();
        var (objectPath, thumbnailObjectPath) = await photoStorage.UploadAsync(
            request.GroupActivityId, photoId, request.ImageBytes, cancellationToken);

        var photo = new GroupActivityPhoto
        {
            Id = photoId,
            GroupActivityId = request.GroupActivityId,
            ObjectPath = objectPath,
            ThumbnailObjectPath = thumbnailObjectPath,
            Caption = request.Caption,
        };

        db.GroupActivityPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);

        return GroupActivityPhotoResult.Success(await mapper.ToPhotoResponseAsync(photo, cancellationToken));
    }
}
