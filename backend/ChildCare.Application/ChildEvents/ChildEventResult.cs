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

// Feature 009c — data-model.md ChildEventBatchFailureReason. In-memory only, not persisted.
// No ValidationFailed value: the batch's payload is shared across every child (not per-child),
// so a payload validation failure rejects the whole request via the standard FluentValidation
// pipeline (422, before any child is processed) exactly like batch_too_large/
// batch_type_not_supported — it can never appear as one child's result among others that
// succeeded. Discovered while implementing the handler (an earlier plan-phase draft had listed
// it as a third per-child reason); data-model.md/contracts corrected to match.
public enum ChildEventBatchFailureReason
{
    ChildNotFound,
    NotPresent,
}

public record ChildEventBatchCreated(Guid ChildId, Guid EventId);

public record ChildEventBatchError(Guid ChildId, ChildEventBatchFailureReason Reason);

public class ChildEventBatchResult
{
    public IReadOnlyList<ChildEventBatchCreated> Created { get; }
    public IReadOnlyList<ChildEventBatchError> Errors { get; }

    public ChildEventBatchResult(IReadOnlyList<ChildEventBatchCreated> created, IReadOnlyList<ChildEventBatchError> errors)
    {
        Created = created;
        Errors = errors;
    }

    public static ChildEventBatchResult Empty { get; } = new([], []);
}
