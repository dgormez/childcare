using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

public record ListChildVaccineRecordsQuery(Guid ChildId) : IRequest<IReadOnlyList<VaccineRecordResponse>>;

public class ListChildVaccineRecordsQueryHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<ListChildVaccineRecordsQuery, IReadOnlyList<VaccineRecordResponse>>
{
    public async Task<IReadOnlyList<VaccineRecordResponse>> Handle(ListChildVaccineRecordsQuery request, CancellationToken cancellationToken)
    {
        var records = await db.VaccineRecords
            .AsNoTracking()
            .Where(v => v.ChildId == request.ChildId && v.DeletedAt == null)
            .OrderByDescending(v => v.AdministeredOn)
            .ThenByDescending(v => v.Id)
            .ToListAsync(cancellationToken);

        var responses = new List<VaccineRecordResponse>(records.Count);
        foreach (var record in records)
            responses.Add(await VaccineRecordMapper.ToResponseAsync(record, storage, cancellationToken));

        return responses;
    }
}
