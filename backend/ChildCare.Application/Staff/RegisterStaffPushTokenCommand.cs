using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>
/// Feature 027 deviation (see StaffProfile.cs's PushToken field comment and
/// NotificationEndpoints.cs's doc comment) — mirrors the parent side's
/// RegisterPushTokenCommand exactly, but overwrites StaffProfile.PushToken instead of
/// Contact.PushToken. Not called out as its own task in tasks.md, but required for
/// StaffScheduleNotificationService/StaffLeaveRequestNotificationService to have anything to
/// send a push to.
/// </summary>
public record RegisterStaffPushTokenCommand(Guid TenantUserId, string PushToken) : IRequest<bool>;

public class RegisterStaffPushTokenCommandValidator : AbstractValidator<RegisterStaffPushTokenCommand>
{
    public RegisterStaffPushTokenCommandValidator()
    {
        RuleFor(x => x.PushToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.push_token_required")
            .MaximumLength(200).WithMessage("errors.staff.push_token_too_long");
    }
}

public class RegisterStaffPushTokenCommandHandler(ITenantDbContext db) : IRequestHandler<RegisterStaffPushTokenCommand, bool>
{
    public async Task<bool> Handle(RegisterStaffPushTokenCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return false;

        profile.PushToken = request.PushToken;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
