using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Shared success/failure result for every contract command, mirroring ChildResult/GroupResult.
/// FluentValidation failures never reach this type — they're handled by the shared
/// ValidationBehavior pipeline before a handler runs.
/// </summary>
public class ContractResult
{
    public ContractResponse? Response { get; private init; }
    public ContractFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static ContractResult Success(ContractResponse response) => new() { Response = response };
    public static ContractResult Fail(ContractFailure failure) => new() { Failure = failure };
}

public enum ContractFailure
{
    NotFound,
    ChildNotFound,
    LocationNotFound,
    NotDraft,
    NotActive,
    AlreadyActiveAtLocation,
    DayOverlap,
    AmendmentStartDateInvalid,
    TerminationDateInvalid,
}
