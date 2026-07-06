using ChildCare.Application.Common;
using ChildCare.Application.Locations;
using ChildCare.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public class UpdateStaffProfileCommandValidator : AbstractValidator<UpdateStaffProfileCommand>
{
    public UpdateStaffProfileCommandValidator(ITenantDbContext db)
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.firstname_required")
            .MaximumLength(100).WithMessage("errors.staff.firstname_too_long");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.lastname_required")
            .MaximumLength(100).WithMessage("errors.staff.lastname_too_long");

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.phone_required")
            .MaximumLength(30).WithMessage("errors.staff.phone_too_long")
            .Matches(CreateLocationCommandValidator.PhonePattern).WithMessage("errors.staff.phone_invalid");

        // FR-003/FR-009: the target role is whatever role the profile's linked account already
        // has — role itself is never editable here (FR-002/FR-009). A missing profile Id is
        // left to the handler to report as NotFound, not a validation failure.
        RuleFor(x => x.QualificationLevel)
            .MustAsync(async (command, qualificationLevel, ct) =>
            {
                var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
                if (profile is null)
                    return true;

                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == profile.TenantUserId, ct);
                return user is null || user.Role != UserRole.Staff || qualificationLevel is not null;
            })
            .WithMessage("errors.staff.qualification_required");
    }
}
