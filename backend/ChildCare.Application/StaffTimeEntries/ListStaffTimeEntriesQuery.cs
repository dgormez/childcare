using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

public record ListStaffTimeEntriesQuery(Guid StaffProfileId, DateOnly From, DateOnly To) : IRequest<IReadOnlyList<StaffTimeEntryResponse>>;

public class ListStaffTimeEntriesQueryHandler(ITenantDbContext db) : IRequestHandler<ListStaffTimeEntriesQuery, IReadOnlyList<StaffTimeEntryResponse>>
{
    public async Task<IReadOnlyList<StaffTimeEntryResponse>> Handle(ListStaffTimeEntriesQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var entries = await db.StaffTimeEntries
            .Where(e => e.StaffProfileId == request.StaffProfileId && e.ClockedInAt >= from && e.ClockedInAt <= to)
            .OrderByDescending(e => e.ClockedInAt)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return entries.Select(e => StaffTimeEntryMapper.ToResponse(e, now)).ToList();
    }
}
