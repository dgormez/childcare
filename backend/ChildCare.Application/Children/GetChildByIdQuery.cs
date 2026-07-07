using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public record GetChildByIdQuery(Guid Id) : IRequest<ChildResult>;

public class GetChildByIdQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<GetChildByIdQuery, ChildResult>
{
    public async Task<ChildResult> Handle(GetChildByIdQuery request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
