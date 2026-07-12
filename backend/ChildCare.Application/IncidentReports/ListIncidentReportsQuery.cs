using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.IncidentReports;

// DirectorOnly cross-KDV inspection view (FR-009). Default page size 25, secondary sort by Id
// for stable pagination across entries sharing the same OccurredAt.
public record ListIncidentReportsQuery(
    Guid? ChildId,
    Guid? LocationId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<PagedIncidentReportsResponse>;

public class ListIncidentReportsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListIncidentReportsQuery, PagedIncidentReportsResponse>
{
    public async Task<PagedIncidentReportsResponse> Handle(ListIncidentReportsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;

        var query = db.IncidentReports.AsNoTracking().AsQueryable();

        if (request.ChildId is Guid childId)
            query = query.Where(r => r.ChildId == childId);
        if (request.LocationId is Guid locationId)
            query = query.Where(r => r.LocationId == locationId);
        if (request.From is DateTime from)
            query = query.Where(r => r.OccurredAt >= from);
        if (request.To is DateTime to)
            query = query.Where(r => r.OccurredAt <= to);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.OccurredAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedIncidentReportsResponse(
            items.Select(IncidentReportMapper.ToResponse).ToList(),
            page,
            pageSize,
            totalCount);
    }
}
