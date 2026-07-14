using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Locations;

/// <summary>
/// Shared success/failure result for every location command, mirroring AuthResult's shape
/// (feature 003). Mapping a failure to an HTTP status + errorKey is HTTP translation, not
/// business logic (constitution Principle III) — see contracts/locations-api.md, ERROR_KEYS.md.
/// FluentValidation failures never reach this type — they're handled by the shared
/// ValidationBehavior pipeline before a handler runs.
/// </summary>
public class LocationResult
{
    public LocationResponse? Response { get; private init; }
    public LocationFailure? Failure { get; private init; }

    // Feature 013f FR-014 — only populated for PendingRequestsWarning, mirrors
    // PublishClosureCalendarResult's CheckedInCount payload-carrying pattern (feature 011).
    public IReadOnlyDictionary<string, int>? PendingCounts { get; private init; }

    // Feature 013j FR-014 — only populated for MenuVariantRemovalWarning, same
    // confirm-despite-a-real-consequence shape as PendingCounts above.
    public IReadOnlyList<string>? VariantsRequiringConfirmation { get; private init; }

    public bool Succeeded => Failure is null;

    public static LocationResult Success(LocationResponse response) => new() { Response = response };
    public static LocationResult Fail(LocationFailure failure) => new() { Failure = failure };
    public static LocationResult Fail(LocationFailure failure, IReadOnlyDictionary<string, int> pendingCounts) => new()
    {
        Failure = failure,
        PendingCounts = pendingCounts,
    };
    public static LocationResult FailMenuVariantRemoval(IReadOnlyList<string> variantsRequiringConfirmation) => new()
    {
        Failure = LocationFailure.MenuVariantRemovalWarning,
        VariantsRequiringConfirmation = variantsRequiringConfirmation,
    };
}

public enum LocationFailure
{
    NotFound,
    HasActiveDependents,
    PendingRequestsWarning,
    MenuVariantRemovalWarning,
}
