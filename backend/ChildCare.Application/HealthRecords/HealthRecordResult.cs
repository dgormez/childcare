using ChildCare.Contracts.Responses;

namespace ChildCare.Application.HealthRecords;

public class HealthRecordResult
{
    public HealthRecordResponse? Response { get; private init; }
    public HealthRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static HealthRecordResult Success(HealthRecordResponse response) => new() { Response = response };
    public static HealthRecordResult Fail(HealthRecordFailure failure) => new() { Failure = failure };
}

public enum HealthRecordFailure
{
    ChildNotFound,
    NotFound,
    InvalidContentType,
}
