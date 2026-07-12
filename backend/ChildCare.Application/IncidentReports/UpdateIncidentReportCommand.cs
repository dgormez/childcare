using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.IncidentReports;

// Auth: DirectorOnly, or a device token whose paired location matches the report's LocationId
// (FR-007 — location-scoped, not restricted to the specific device that filed it).
public record UpdateIncidentReportCommand(
    Guid Id,
    bool IsDirector,
    Guid? DeviceLocationId,
    DateTime? OccurredAt,
    string? LocationDetail,
    string? Description,
    string? InjuryType,
    string? FirstAidGiven,
    bool? DoctorCalled,
    string? DoctorNotes,
    bool? ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    string? Witnesses,
    string? FollowUp) : IRequest<IncidentReportResult>;

public class UpdateIncidentReportCommandValidator : AbstractValidator<UpdateIncidentReportCommand>
{
    public UpdateIncidentReportCommandValidator()
    {
        RuleFor(x => x.InjuryType)
            .Must(v => v is null || IncidentInjuryTypeExtensions.TryParseWireString(v, out _))
            .WithMessage("errors.incident_reports.injury_type_required");

        RuleFor(x => x.ParentNotifiedHow)
            .Must(v => v is null || ParentNotifiedHowExtensions.TryParseWireString(v, out _))
            .WithMessage("errors.incident_reports.invalid_parent_notified_how");
    }
}

public class UpdateIncidentReportCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateIncidentReportCommand, IncidentReportResult>
{
    // Every field this request shape carries other than FollowUp — used to detect whether a
    // locked (>24h) report's request includes anything beyond FollowUp (FR-005/FR-006).
    private static bool HasLockedFieldChange(UpdateIncidentReportCommand r) =>
        r.OccurredAt is not null || r.LocationDetail is not null || r.Description is not null
        || r.InjuryType is not null || r.FirstAidGiven is not null || r.DoctorCalled is not null
        || r.DoctorNotes is not null || r.ParentNotified is not null || r.ParentNotifiedAt is not null
        || r.ParentNotifiedHow is not null || r.Witnesses is not null;

    public async Task<IncidentReportResult> Handle(UpdateIncidentReportCommand request, CancellationToken cancellationToken)
    {
        var report = await db.IncidentReports.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (report is null)
            return IncidentReportResult.Fail(IncidentReportFailure.NotFound);

        if (!request.IsDirector && report.LocationId != request.DeviceLocationId)
            return IncidentReportResult.Fail(IncidentReportFailure.NotFound);

        var isLocked = DateTime.UtcNow - report.CreatedAt > TimeSpan.FromHours(24);
        if (isLocked && HasLockedFieldChange(request))
            return IncidentReportResult.Fail(IncidentReportFailure.Locked);

        if (!isLocked)
        {
            if (request.OccurredAt is DateTime occurredAt) report.OccurredAt = occurredAt;
            if (request.LocationDetail is not null) report.LocationDetail = request.LocationDetail;
            if (request.Description is not null) report.Description = request.Description;
            if (request.InjuryType is not null && IncidentInjuryTypeExtensions.TryParseWireString(request.InjuryType, out var injuryType))
                report.InjuryType = injuryType;
            if (request.FirstAidGiven is not null) report.FirstAidGiven = request.FirstAidGiven;
            if (request.DoctorCalled is bool doctorCalled) report.DoctorCalled = doctorCalled;
            if (request.DoctorNotes is not null) report.DoctorNotes = request.DoctorNotes;
            if (request.ParentNotified is bool parentNotified) report.ParentNotified = parentNotified;
            if (request.ParentNotifiedAt is DateTime parentNotifiedAt) report.ParentNotifiedAt = parentNotifiedAt;
            if (request.ParentNotifiedHow is not null && ParentNotifiedHowExtensions.TryParseWireString(request.ParentNotifiedHow, out var how))
                report.ParentNotifiedHow = how;
            if (request.Witnesses is not null) report.Witnesses = request.Witnesses;
        }

        // FR-006: FollowUp is the one field never subject to the lock, at any age.
        if (request.FollowUp is not null)
            report.FollowUp = request.FollowUp;

        // ReviewedAt is deliberately never touched here (spec Clarifications) — edits never
        // reset the reviewed indicator.
        report.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return IncidentReportResult.Success(IncidentReportMapper.ToResponse(report));
    }
}
