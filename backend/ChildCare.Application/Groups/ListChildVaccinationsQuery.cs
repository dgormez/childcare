using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record ListChildVaccinationsQuery(Guid ChildId) : IRequest<IReadOnlyList<VaccinationResponse>>;

public class ListChildVaccinationsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListChildVaccinationsQuery, IReadOnlyList<VaccinationResponse>>
{
    public async Task<IReadOnlyList<VaccinationResponse>> Handle(ListChildVaccinationsQuery request, CancellationToken cancellationToken)
    {
        var records = await db.VaccinationRecords
            .Where(v => v.ChildId == request.ChildId)
            .OrderByDescending(v => v.DateAdministered)
            .ToListAsync(cancellationToken);

        return records.Select(GroupMapper.ToVaccinationResponse).ToList();
    }
}
