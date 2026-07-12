using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.IncidentReports;

// Authorized for DirectorOnly, or a device token whose paired location/group currently has the
// child assigned — not restricted to reports that device itself filed (FR-018, mirrors feature
// 008's medical-quick-access location/group scoping).
public record GetIncidentReportQuery(Guid Id, bool IsDirector, Guid? DeviceGroupId) : IRequest<IncidentReportResult>;

public class GetIncidentReportQueryHandler(ITenantDbContext db) : IRequestHandler<GetIncidentReportQuery, IncidentReportResult>
{
    public async Task<IncidentReportResult> Handle(GetIncidentReportQuery request, CancellationToken cancellationToken)
    {
        var report = await db.IncidentReports.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (report is null)
            return IncidentReportResult.Fail(IncidentReportFailure.NotFound);

        if (!request.IsDirector)
        {
            var deviceGroupId = request.DeviceGroupId;
            var inScope = deviceGroupId is not null && await db.ChildGroupAssignments
                .AnyAsync(a => a.ChildId == report.ChildId && a.EndDate == null && a.GroupId == deviceGroupId, cancellationToken);
            if (!inScope)
                return IncidentReportResult.Fail(IncidentReportFailure.NotFound);
        }

        // research.md R3: the detail-view read itself is the "mark reviewed" action — no
        // separate click, never reset by subsequent edits.
        if (request.IsDirector && report.ReviewedAt is null)
        {
            report.ReviewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return IncidentReportResult.Success(IncidentReportMapper.ToResponse(report));
    }
}
