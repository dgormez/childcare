using ChildCare.Contracts.Responses;

namespace ChildCare.Application.VaccineRecords;

public class VaccineRecordResult
{
    public VaccineRecordResponse? Response { get; private init; }
    public VaccineRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static VaccineRecordResult Success(VaccineRecordResponse response) => new() { Response = response };
    public static VaccineRecordResult Fail(VaccineRecordFailure failure) => new() { Failure = failure };
}

public enum VaccineRecordFailure
{
    ChildNotFound,
    NotFound,
}
