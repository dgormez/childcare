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

    public bool Succeeded => Failure is null;

    public static LocationResult Success(LocationResponse response) => new() { Response = response };
    public static LocationResult Fail(LocationFailure failure) => new() { Failure = failure };
}

public enum LocationFailure
{
    NotFound,
    HasActiveDependents,
}
