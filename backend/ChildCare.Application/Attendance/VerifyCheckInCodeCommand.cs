using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

// Feature 021 — spec.md FR-009. LocationId/GroupId come from the scanning device's own JWT
// claims (endpoint layer resolves them), never client-supplied — same convention as
// CheckInCommand.
public record VerifyCheckInCodeCommand(string Code, Guid LocationId, Guid GroupId) : IRequest<VerifyCheckInCodeResult>;

public class VerifyCheckInCodeCommandValidator : AbstractValidator<VerifyCheckInCodeCommand>
{
    public VerifyCheckInCodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
    }
}

public enum VerifyCheckInCodeFailure
{
    InvalidCode,
    CodeExpired,
    AlreadyUsed,
    WrongLocation,

    // The underlying CheckInCommand/CheckOutCommand dispatch failed — see AttendanceFailure for
    // the specific reason (e.g. closure day). Never a QR-specific failure of its own, per
    // FR-008/FR-014's parity requirement (research.md R5).
    AttendanceCommandFailed,
}

public class VerifyCheckInCodeResult
{
    public AttendanceRecordResponse? Response { get; private init; }
    public string? Direction { get; private init; }
    // FR-008/User Story 2 — the tablet's scan confirmation shows the child's name/photo, not
    // just an id; resolved here rather than pushing a second lookup onto the caregiver tablet.
    public string? ChildFirstName { get; private init; }
    public string? ChildLastName { get; private init; }
    public string? ChildPhotoDownloadUrl { get; private init; }
    public VerifyCheckInCodeFailure? Failure { get; private init; }
    public AttendanceFailure? AttendanceFailure { get; private init; }
    public bool Succeeded => Failure is null;

    public static VerifyCheckInCodeResult Success(
        AttendanceRecordResponse response, string direction, string childFirstName, string childLastName, string? childPhotoDownloadUrl) =>
        new()
        {
            Response = response,
            Direction = direction,
            ChildFirstName = childFirstName,
            ChildLastName = childLastName,
            ChildPhotoDownloadUrl = childPhotoDownloadUrl,
        };

    public static VerifyCheckInCodeResult Fail(VerifyCheckInCodeFailure failure) => new() { Failure = failure };

    public static VerifyCheckInCodeResult FailAttendance(AttendanceFailure attendanceFailure) => new()
    {
        Failure = VerifyCheckInCodeFailure.AttendanceCommandFailed,
        AttendanceFailure = attendanceFailure,
    };
}

/// <summary>
/// Feature 021, research.md R5 — a thin router in front of feature 010's existing
/// CheckInCommand/CheckOutCommand state machine. Verification order (signature → cooldown →
/// expiry → wrong-location → dispatch) matches contracts/qr-checkin-api.md exactly; no
/// attendance-toggle logic is re-derived here, so FR-008/FR-014 parity is structural.
/// </summary>
public class VerifyCheckInCodeCommandHandler(
    ITenantDbContext db,
    ICheckInCodeService codeService,
    IMediator mediator,
    IProfilePhotoStorage photoStorage) : IRequestHandler<VerifyCheckInCodeCommand, VerifyCheckInCodeResult>
{
    public async Task<VerifyCheckInCodeResult> Handle(VerifyCheckInCodeCommand request, CancellationToken cancellationToken)
    {
        var verification = codeService.Verify(request.Code);
        if (!verification.Succeeded)
        {
            return verification.Failure switch
            {
                CheckInCodeVerificationFailure.AlreadyUsed => VerifyCheckInCodeResult.Fail(VerifyCheckInCodeFailure.AlreadyUsed),
                CheckInCodeVerificationFailure.Expired => VerifyCheckInCodeResult.Fail(VerifyCheckInCodeFailure.CodeExpired),
                _ => VerifyCheckInCodeResult.Fail(VerifyCheckInCodeFailure.InvalidCode),
            };
        }

        var childId = verification.ChildId!.Value;
        var nonce = verification.Nonce!;

        var enrolledAtThisLocation = await db.Contracts
            .AnyAsync(c => c.ChildId == childId && c.LocationId == request.LocationId && c.Status == ContractStatus.Active, cancellationToken);
        if (!enrolledAtThisLocation)
            return VerifyCheckInCodeResult.Fail(VerifyCheckInCodeFailure.WrongLocation);

        var today = BelgianCalendarDay.Today();
        var existing = await db.AttendanceRecords.AsNoTracking().FirstOrDefaultAsync(
            r => r.ChildId == childId && r.LocationId == request.LocationId && r.Date == today,
            cancellationToken);

        // FR-009: mirrors CheckInCommand's own not-currently-checked-in vs. present branch.
        var isCurrentlyPresent = existing is { Status: AttendanceStatus.Present, CheckOutAt: null };

        if (isCurrentlyPresent)
        {
            var checkOutResult = await mediator.Send(new CheckOutCommand(childId, request.LocationId, today), cancellationToken);
            if (!checkOutResult.Succeeded)
                return VerifyCheckInCodeResult.FailAttendance(checkOutResult.Failure!.Value);

            codeService.MarkConsumed(nonce);
            var (firstNameOut, lastNameOut, photoUrlOut) = await ResolveChildDisplayAsync(childId, cancellationToken);
            return VerifyCheckInCodeResult.Success(checkOutResult.Response!, "check-out", firstNameOut, lastNameOut, photoUrlOut);
        }

        var checkInResult = await mediator.Send(new CheckInCommand(childId, request.LocationId, request.GroupId, today), cancellationToken);
        if (!checkInResult.Succeeded)
            return VerifyCheckInCodeResult.FailAttendance(checkInResult.Failure!.Value);

        codeService.MarkConsumed(nonce);
        var (firstNameIn, lastNameIn, photoUrlIn) = await ResolveChildDisplayAsync(childId, cancellationToken);
        return VerifyCheckInCodeResult.Success(checkInResult.Response!, "check-in", firstNameIn, lastNameIn, photoUrlIn);
    }

    private async Task<(string FirstName, string LastName, string? PhotoUrl)> ResolveChildDisplayAsync(Guid childId, CancellationToken cancellationToken)
    {
        var child = await db.Children.AsNoTracking().FirstAsync(c => c.Id == childId, cancellationToken);
        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return (child.FirstName, child.LastName, photoUrl);
    }
}
