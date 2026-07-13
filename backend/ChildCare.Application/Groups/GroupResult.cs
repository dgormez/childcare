using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Groups;

public class GroupResult
{
    public GroupResponse? Response { get; private init; }
    public GroupFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static GroupResult Success(GroupResponse response) => new() { Response = response };
    public static GroupResult Fail(GroupFailure failure) => new() { Failure = failure };
}

public enum GroupFailure
{
    NotFound,
    ChildNotFound,
    LocationNotFound,
    OutOfChronologicalOrder,
}

/// <summary>Separate result for endpoints returning a ChildGroupAssignmentResponse.</summary>
public class ChildGroupAssignmentResult
{
    public ChildGroupAssignmentResponse? Response { get; private init; }
    public GroupFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ChildGroupAssignmentResult Success(ChildGroupAssignmentResponse response) => new() { Response = response };
    public static ChildGroupAssignmentResult Fail(GroupFailure failure) => new() { Failure = failure };
}
