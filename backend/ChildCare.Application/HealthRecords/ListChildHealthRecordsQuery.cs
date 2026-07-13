using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

public record ListChildHealthRecordsQuery(Guid ChildId) : IRequest<IReadOnlyList<HealthRecordResponse>>;

public class ListChildHealthRecordsQueryHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<ListChildHealthRecordsQuery, IReadOnlyList<HealthRecordResponse>>
{
    public async Task<IReadOnlyList<HealthRecordResponse>> Handle(ListChildHealthRecordsQuery request, CancellationToken cancellationToken)
    {
        var records = await db.HealthRecords
            .AsNoTracking()
            .Where(r => r.ChildId == request.ChildId && r.DeletedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        var responses = new List<HealthRecordResponse>(records.Count);
        foreach (var record in records)
            responses.Add(await HealthRecordMapper.ToResponseAsync(record, storage, cancellationToken));

        return responses;
    }
}
