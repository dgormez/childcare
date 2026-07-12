using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.IncidentReports;

public record GenerateIncidentReportPdfQuery(Guid Id, string? Locale) : IRequest<GenerateIncidentReportPdfResult>;

public record GenerateIncidentReportPdfResult(bool Found, byte[] Bytes);

public class GenerateIncidentReportPdfQueryHandler(ITenantDbContext db, IIncidentReportPdfGenerator pdfGenerator)
    : IRequestHandler<GenerateIncidentReportPdfQuery, GenerateIncidentReportPdfResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<GenerateIncidentReportPdfResult> Handle(GenerateIncidentReportPdfQuery request, CancellationToken cancellationToken)
    {
        var report = await db.IncidentReports.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (report is null)
            return new GenerateIncidentReportPdfResult(false, []);

        var child = await db.Children.FirstAsync(c => c.Id == report.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == report.LocationId, cancellationToken);

        // Mirrors feature 007's contract-PDF convention: defaults to Dutch when unspecified or
        // unrecognized (FR-016).
        var locale = request.Locale is not null && SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var model = new IncidentReportPdfModel(
            $"{child.FirstName} {child.LastName}",
            location.Name,
            location.Address,
            location.Dossiernummer,
            report.OccurredAt,
            report.CreatedAt,
            report.LocationDetail,
            report.Description,
            report.InjuryType.ToWireString(),
            report.FirstAidGiven,
            report.DoctorCalled,
            report.DoctorNotes,
            report.ParentNotified,
            report.ParentNotifiedAt,
            report.ParentNotifiedHow?.ToWireString(),
            report.Witnesses,
            report.FollowUp,
            locale);

        var bytes = await pdfGenerator.GenerateAsync(model, cancellationToken);
        return new GenerateIncidentReportPdfResult(true, bytes);
    }
}
