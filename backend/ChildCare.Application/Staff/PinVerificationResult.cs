using ChildCare.Domain.Entities;

namespace ChildCare.Application.Staff;

/// <summary>
/// Result of VerifyPinCommand — shared by check-in, check-out, and sensitive-action
/// confirmation (spec Clarifications, research.md R2/R6).
/// </summary>
public class PinVerificationResult
{
    public StaffProfile? StaffProfile { get; private init; }
    public PinVerificationFailure? Failure { get; private init; }
    public int? AttemptsRemaining { get; private init; }
    public DateTime? LockedUntil { get; private init; }

    public bool Succeeded => Failure is null;

    public static PinVerificationResult Success(StaffProfile staffProfile) => new() { StaffProfile = staffProfile };

    public static PinVerificationResult NotEligible() => new() { Failure = PinVerificationFailure.NotEligible };

    public static PinVerificationResult Invalid(int attemptsRemaining) =>
        new() { Failure = PinVerificationFailure.Invalid, AttemptsRemaining = attemptsRemaining };

    public static PinVerificationResult Locked(DateTime lockedUntil) =>
        new() { Failure = PinVerificationFailure.Locked, LockedUntil = lockedUntil };
}

public enum PinVerificationFailure
{
    /// <summary>staffId is deactivated or not eligible at the device's location (FR-004/024/025).</summary>
    NotEligible,

    /// <summary>Incorrect PIN — never distinguishes "wrong PIN" from any other reason.</summary>
    Invalid,

    /// <summary>This staffId's PIN is in its sliding-window lockout (FR-012).</summary>
    Locked,
}
