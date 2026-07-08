using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Devices;

/// <summary>Result for PairDeviceCommand.</summary>
public class DeviceResult
{
    public DevicePairingResponse? Response { get; private init; }
    public DeviceFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static DeviceResult Success(DevicePairingResponse response) => new() { Response = response };
    public static DeviceResult Fail(DeviceFailure failure) => new() { Failure = failure };
}

public enum DeviceFailure
{
    LocationNotFound,
    GroupNotFound,
}

/// <summary>Result for ExitRoomModeCommand — the override PIN is a single-target comparison
/// against DevicePairing.DirectorOverridePinHash, with its own lockout counter on that same
/// row (spec FR-005), unrelated to caregiver-PIN lockout.</summary>
public class ExitRoomModeResult
{
    public ExitRoomModeFailure? Failure { get; private init; }
    public int? AttemptsRemaining { get; private init; }
    public DateTime? LockedUntil { get; private init; }

    public bool Succeeded => Failure is null;

    public static ExitRoomModeResult Success() => new();

    public static ExitRoomModeResult Invalid(int attemptsRemaining) =>
        new() { Failure = ExitRoomModeFailure.InvalidOverridePin, AttemptsRemaining = attemptsRemaining };

    public static ExitRoomModeResult Locked(DateTime lockedUntil) =>
        new() { Failure = ExitRoomModeFailure.OverridePinLocked, LockedUntil = lockedUntil };
}

public enum ExitRoomModeFailure
{
    InvalidOverridePin,
    OverridePinLocked,
}

/// <summary>Result for RevokeDeviceCommand.</summary>
public class RevokeDeviceResult
{
    public RevokeDeviceFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static RevokeDeviceResult Success() => new();
    public static RevokeDeviceResult Fail(RevokeDeviceFailure failure) => new() { Failure = failure };
}

public enum RevokeDeviceFailure
{
    NotFound,
}
