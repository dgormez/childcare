namespace ChildCare.Application.Staff;

/// <summary>Result for SetCaregiverPinCommand/DeleteCaregiverPinCommand — both return 204 on
/// success (contracts/pin-management-api.md), so there's no response payload to carry.</summary>
public class PinManagementResult
{
    public PinManagementFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static PinManagementResult Success() => new();
    public static PinManagementResult Fail(PinManagementFailure failure) => new() { Failure = failure };
}

public enum PinManagementFailure
{
    NotFound,
    NotUniqueAtLocation,
}
