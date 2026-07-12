using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.IncidentReports;

// LocationId/GroupId come from the filing device's own claims (endpoint layer resolves them,
// mirrors RecordChildEventCommand's pattern) — never client-supplied (FR-019).
public record FileIncidentReportCommand(
    Guid ChildId,
    Guid LocationId,
    Guid GroupId,
    DateTime? OccurredAt,
    string? LocationDetail,
    string Description,
    string InjuryType,
    string? FirstAidGiven,
    bool DoctorCalled,
    string? DoctorNotes,
    bool ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    string? Witnesses,
    string? FollowUp) : IRequest<IncidentReportResult>;

public class FileIncidentReportCommandValidator : AbstractValidator<FileIncidentReportCommand>
{
    public FileIncidentReportCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();

        // FR-002: description and injuryType are the two required fields; everything else is
        // optional regardless of injuryType (Acceptance Scenario 5).
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("errors.incident_reports.description_required");

        RuleFor(x => x.InjuryType)
            .Must(v => IncidentInjuryTypeExtensions.TryParseWireString(v, out _))
            .WithMessage("errors.incident_reports.injury_type_required");

        RuleFor(x => x.ParentNotifiedHow)
            .Must(v => v is null || ParentNotifiedHowExtensions.TryParseWireString(v, out _))
            .WithMessage("errors.incident_reports.invalid_parent_notified_how");
    }
}

public class FileIncidentReportCommandHandler(ITenantDbContext db, IShiftAttributionService attribution)
    : IRequestHandler<FileIncidentReportCommand, IncidentReportResult>
{
    public async Task<IncidentReportResult> Handle(FileIncidentReportCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return IncidentReportResult.Fail(IncidentReportFailure.ChildNotFound);

        var occurredAt = request.OccurredAt ?? DateTime.UtcNow;

        // research.md R1: reuses feature 009's IShiftAttributionService rather than a new
        // resolver — never a client-submitted value (FR-004), empty (not blocking) if nobody
        // was checked in.
        var reportedBy = await attribution.ResolveRecordedByAsync(
            request.LocationId, request.GroupId, occurredAt, cancellationToken);

        IncidentInjuryTypeExtensions.TryParseWireString(request.InjuryType, out var injuryType);
        ParentNotifiedHow? parentNotifiedHow = request.ParentNotifiedHow is not null
            && ParentNotifiedHowExtensions.TryParseWireString(request.ParentNotifiedHow, out var parsedHow)
                ? parsedHow
                : null;

        var report = new IncidentReport
        {
            ChildId = request.ChildId,
            LocationId = request.LocationId,
            OccurredAt = occurredAt,
            LocationDetail = request.LocationDetail,
            Description = request.Description,
            InjuryType = injuryType,
            FirstAidGiven = request.FirstAidGiven,
            DoctorCalled = request.DoctorCalled,
            DoctorNotes = request.DoctorNotes,
            ParentNotified = request.ParentNotified,
            ParentNotifiedAt = request.ParentNotifiedAt,
            ParentNotifiedHow = parentNotifiedHow,
            ReportedBy = reportedBy.ToList(),
            Witnesses = request.Witnesses,
            FollowUp = request.FollowUp,
        };

        db.IncidentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return IncidentReportResult.Success(IncidentReportMapper.ToResponse(report));
    }
}
