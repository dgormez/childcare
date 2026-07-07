using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public record DeactivateChildCommand(Guid Id) : IRequest<ChildResult>;

public class DeactivateChildCommandHandler(ITenantDbContext db, IEnumerable<IChildDeactivationGuard> guards, IProfilePhotoStorage photoStorage)
    : IRequestHandler<DeactivateChildCommand, ChildResult>
{
    public async Task<ChildResult> Handle(DeactivateChildCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        foreach (var guard in guards)
        {
            if (await guard.HasActiveDependentsAsync(request.Id, db, cancellationToken))
                return ChildResult.Fail(ChildFailure.HasActiveDependents);
        }

        if (child.DeactivatedAt is null)
        {
            child.DeactivatedAt = DateTime.UtcNow;
            child.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
