using ChildCare.Contracts.Responses;

namespace ChildCare.Application.RoomShifts;

/// <summary>Shared failure vocabulary for check-in/check-out/confirm-administrator — mirrors
/// PinVerificationFailure plus the story-specific conflict cases.</summary>
public enum RoomShiftFailure
{
    NotEligible,
    Invalid,
    Locked,
    AlreadyCheckedIn,
    NotCheckedIn,
}

public class CheckInResult
{
    public CheckInResponse? Response { get; private init; }
    public RoomShiftFailure? Failure { get; private init; }
    public int? AttemptsRemaining { get; private init; }
    public DateTime? LockedUntil { get; private init; }

    public bool Succeeded => Failure is null;

    public static CheckInResult Success(CheckInResponse response) => new() { Response = response };
    public static CheckInResult NotEligible() => new() { Failure = RoomShiftFailure.NotEligible };
    public static CheckInResult AlreadyCheckedIn() => new() { Failure = RoomShiftFailure.AlreadyCheckedIn };
    public static CheckInResult Invalid(int attemptsRemaining) =>
        new() { Failure = RoomShiftFailure.Invalid, AttemptsRemaining = attemptsRemaining };
    public static CheckInResult Locked(DateTime lockedUntil) =>
        new() { Failure = RoomShiftFailure.Locked, LockedUntil = lockedUntil };
}

public class CheckOutResult
{
    public CheckOutResponse? Response { get; private init; }
    public RoomShiftFailure? Failure { get; private init; }
    public int? AttemptsRemaining { get; private init; }
    public DateTime? LockedUntil { get; private init; }

    public bool Succeeded => Failure is null;

    public static CheckOutResult Success(CheckOutResponse response) => new() { Response = response };
    public static CheckOutResult NotEligible() => new() { Failure = RoomShiftFailure.NotEligible };
    public static CheckOutResult NotCheckedIn() => new() { Failure = RoomShiftFailure.NotCheckedIn };
    public static CheckOutResult Invalid(int attemptsRemaining) =>
        new() { Failure = RoomShiftFailure.Invalid, AttemptsRemaining = attemptsRemaining };
    public static CheckOutResult Locked(DateTime lockedUntil) =>
        new() { Failure = RoomShiftFailure.Locked, LockedUntil = lockedUntil };
}

public class ConfirmAdministratorResult
{
    public ConfirmAdministratorResponse? Response { get; private init; }
    public RoomShiftFailure? Failure { get; private init; }
    public int? AttemptsRemaining { get; private init; }
    public DateTime? LockedUntil { get; private init; }

    public bool Succeeded => Failure is null;

    public static ConfirmAdministratorResult Success(ConfirmAdministratorResponse response) => new() { Response = response };
    public static ConfirmAdministratorResult NotEligible() => new() { Failure = RoomShiftFailure.NotEligible };
    public static ConfirmAdministratorResult NotCheckedIn() => new() { Failure = RoomShiftFailure.NotCheckedIn };
    public static ConfirmAdministratorResult Invalid(int attemptsRemaining) =>
        new() { Failure = RoomShiftFailure.Invalid, AttemptsRemaining = attemptsRemaining };
    public static ConfirmAdministratorResult Locked(DateTime lockedUntil) =>
        new() { Failure = RoomShiftFailure.Locked, LockedUntil = lockedUntil };
}

public class RoomShiftCorrectionResult
{
    public RoomShiftCorrectionResponse? Response { get; private init; }
    public bool Succeeded => Response is not null;

    public static RoomShiftCorrectionResult Success(RoomShiftCorrectionResponse response) => new() { Response = response };
    public static RoomShiftCorrectionResult NotFound() => new();
}
