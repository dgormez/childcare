using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invitations;

public record ListPlatformAdminInvitationsQuery : IRequest<IReadOnlyList<PlatformAdminInvitationResponse>>;

public class ListPlatformAdminInvitationsQueryHandler(IPublicDbContext publicDb)
    : IRequestHandler<ListPlatformAdminInvitationsQuery, IReadOnlyList<PlatformAdminInvitationResponse>>
{
    public async Task<IReadOnlyList<PlatformAdminInvitationResponse>> Handle(ListPlatformAdminInvitationsQuery request, CancellationToken cancellationToken)
    {
        var invitations = await publicDb.Invitations
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var acceptedIds = (await publicDb.Tenants
            .Select(t => t.CreatedFromInvitationId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return invitations
            .Select(i => PlatformAdminInvitationMapper.ToResponse(i, hasTenant: acceptedIds.Contains(i.Id)))
            .ToList();
    }
}
