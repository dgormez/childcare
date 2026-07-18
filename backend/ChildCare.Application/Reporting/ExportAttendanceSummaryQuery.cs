using ChildCare.Application.Common;
using MediatR;

namespace ChildCare.Application.Reporting;

public class ExportAttendanceSummaryResult
{
    public bool Succeeded { get; init; }
    public byte[]? Content { get; init; }

    public static ExportAttendanceSummaryResult Success(byte[] content) => new() { Succeeded = true, Content = content };
    public static ExportAttendanceSummaryResult Fail() => new();
}

/// <summary>
/// FR-007/FR-008/FR-022: CSV or PDF export of the monthly attendance summary, reusing
/// GetAttendanceSummaryQuery's shared aggregation (research.md R5) so both formats — and the
/// on-screen view — always agree exactly (spec.md SC-002). Computed fresh on every request, no
/// caching, so a re-export after correcting an AttendanceRecord reflects it immediately.
/// </summary>
public record ExportAttendanceSummaryQuery(Guid? LocationId, DateOnly Month, string Format, string Locale = "nl")
    : IRequest<ExportAttendanceSummaryResult>;

public class ExportAttendanceSummaryQueryHandler(
    IMediator mediator,
    IAttendanceSummaryCsvWriter csvWriter,
    IAttendanceSummaryPdfGenerator pdfGenerator)
    : IRequestHandler<ExportAttendanceSummaryQuery, ExportAttendanceSummaryResult>
{
    private static readonly string[] SupportedLocales = ["nl", "fr", "en"];

    public async Task<ExportAttendanceSummaryResult> Handle(ExportAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        if (request.Format is not ("csv" or "pdf"))
            return ExportAttendanceSummaryResult.Fail();

        var summary = await mediator.Send(new GetAttendanceSummaryQuery(request.LocationId, request.Month), cancellationToken);
        var locale = SupportedLocales.Contains(request.Locale) ? request.Locale : "nl";

        var content = request.Format == "csv"
            ? csvWriter.Write(summary)
            : pdfGenerator.Generate(summary, locale);

        return ExportAttendanceSummaryResult.Success(content);
    }
}
