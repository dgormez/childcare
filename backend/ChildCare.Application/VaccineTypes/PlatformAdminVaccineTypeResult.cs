using ChildCare.Contracts.Responses;

namespace ChildCare.Application.VaccineTypes;

// contracts/platform-admin-vaccine-types-api.md (feature 013h) — shared Result/failure shape for
// every platform-admin write operation on VaccineType, mirroring WaitingListResult's pattern.
public enum PlatformAdminVaccineTypeFailure
{
    NotFound,
    AlreadyAtBoundary,
}

public class PlatformAdminVaccineTypeResult
{
    public bool Succeeded { get; init; }
    public PlatformAdminVaccineTypeFailure? Failure { get; init; }
    public PlatformAdminVaccineTypeResponse? Response { get; init; }

    public static PlatformAdminVaccineTypeResult Success(PlatformAdminVaccineTypeResponse response) => new() { Succeeded = true, Response = response };
    public static PlatformAdminVaccineTypeResult Fail(PlatformAdminVaccineTypeFailure failure) => new() { Failure = failure };
}

// Reorder returns the full, freshly-ordered list (contracts/platform-admin-vaccine-types-api.md)
// so the client doesn't need a separate re-fetch after every reorder click.
public class PlatformAdminVaccineTypeListResult
{
    public bool Succeeded { get; init; }
    public PlatformAdminVaccineTypeFailure? Failure { get; init; }
    public IReadOnlyList<PlatformAdminVaccineTypeResponse> Entries { get; init; } = [];

    public static PlatformAdminVaccineTypeListResult Success(IReadOnlyList<PlatformAdminVaccineTypeResponse> entries) => new() { Succeeded = true, Entries = entries };
    public static PlatformAdminVaccineTypeListResult Fail(PlatformAdminVaccineTypeFailure failure) => new() { Failure = failure };
}
