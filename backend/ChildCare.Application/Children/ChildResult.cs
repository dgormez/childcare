using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Children;

/// <summary>
/// Shared success/failure result for every child command, mirroring StaffResult (feature 005).
/// FluentValidation failures never reach this type — they're handled by the shared
/// ValidationBehavior pipeline before a handler runs.
/// </summary>
public class ChildResult
{
    public ChildResponse? Response { get; private init; }
    public ChildFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ChildResult Success(ChildResponse response) => new() { Response = response };
    public static ChildResult Fail(ChildFailure failure) => new() { Failure = failure };
}

public enum ChildFailure
{
    NotFound,
    HasActiveDependents,
    NrnAlreadyInUse,
}
