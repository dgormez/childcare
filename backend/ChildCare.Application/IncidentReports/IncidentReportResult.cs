using ChildCare.Contracts.Responses;

namespace ChildCare.Application.IncidentReports;

public class IncidentReportResult
{
    public IncidentReportResponse? Response { get; private init; }
    public IncidentReportFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static IncidentReportResult Success(IncidentReportResponse response) => new() { Response = response };
    public static IncidentReportResult Fail(IncidentReportFailure failure) => new() { Failure = failure };
}

public enum IncidentReportFailure
{
    ChildNotFound,
    NotFound,
    ValidationFailed,
    Locked,
}
