using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public record ReactivateChildCommand(Guid Id) : IRequest<ChildResult>;

public class ReactivateChildCommandHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<ReactivateChildCommand, ChildResult>
{
    public async Task<ChildResult> Handle(ReactivateChildCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        if (child.DeactivatedAt is not null)
        {
            child.DeactivatedAt = null;
            child.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
