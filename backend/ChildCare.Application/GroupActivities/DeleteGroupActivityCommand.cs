using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

// DirectorOnly (spec.md FR-011) — hard delete, not soft delete (spec.md Assumptions: this is a
// moderation action against inappropriate content, not something worth retaining in an audit
// view). Deletes GCS objects before DB rows so a request racing the delete never resolves a
// dangling photo (spec.md Edge Cases).
public record DeleteGroupActivityCommand(Guid GroupActivityId) : IRequest<GroupActivityDeleteResult>;

public class GroupActivityDeleteResult
{
    public bool Succeeded { get; private init; }
    public GroupActivityFailure? Failure { get; private init; }

    public static GroupActivityDeleteResult Success() => new() { Succeeded = true };
    public static GroupActivityDeleteResult Fail(GroupActivityFailure failure) => new() { Succeeded = false, Failure = failure };
}

public class DeleteGroupActivityCommandHandler(ITenantDbContext db, IGroupActivityPhotoStorage photoStorage)
    : IRequestHandler<DeleteGroupActivityCommand, GroupActivityDeleteResult>
{
    public async Task<GroupActivityDeleteResult> Handle(DeleteGroupActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.GroupActivities.FirstOrDefaultAsync(a => a.Id == request.GroupActivityId, cancellationToken);
        if (activity is null)
            return GroupActivityDeleteResult.Fail(GroupActivityFailure.NotFound);

        var photos = await db.GroupActivityPhotos
            .Where(p => p.GroupActivityId == request.GroupActivityId)
            .ToListAsync(cancellationToken);

        foreach (var photo in photos)
            await photoStorage.DeleteAsync(photo.ObjectPath, photo.ThumbnailObjectPath, cancellationToken);

        db.GroupActivityPhotos.RemoveRange(photos);
        db.GroupActivities.Remove(activity);
        await db.SaveChangesAsync(cancellationToken);

        return GroupActivityDeleteResult.Success();
    }
}
