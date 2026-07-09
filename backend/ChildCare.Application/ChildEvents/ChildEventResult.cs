using ChildCare.Contracts.Responses;

namespace ChildCare.Application.ChildEvents;

public class ChildEventResult
{
    public ChildEventResponse? Response { get; private init; }
    public ChildEventFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ChildEventResult Success(ChildEventResponse response) => new() { Response = response };
    public static ChildEventResult Fail(ChildEventFailure failure) => new() { Failure = failure };
}

public enum ChildEventFailure
{
    ChildNotFound,
    NotFound,
    EditWindowExpired,
}
