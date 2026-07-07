using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

public record ListChildrenQuery(bool IncludeDeactivated = false) : IRequest<IReadOnlyList<ChildResponse>>;

public class ListChildrenQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<ListChildrenQuery, IReadOnlyList<ChildResponse>>
{
    public async Task<IReadOnlyList<ChildResponse>> Handle(ListChildrenQuery request, CancellationToken cancellationToken)
    {
        var query = db.Children.AsQueryable();
        if (!request.IncludeDeactivated)
            query = query.Where(c => c.DeactivatedAt == null);

        var children = await query.ToListAsync(cancellationToken);

        var results = new List<ChildResponse>(children.Count);
        foreach (var child in children)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
            results.Add(ChildMapper.ToResponse(child, photoUrl));
        }

        return results;
    }
}
