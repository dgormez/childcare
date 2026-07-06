using ChildCare.Application.Common;
using ChildCare.Application.Locations;
using ChildCare.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

public class CreateStaffProfileCommandValidator : AbstractValidator<CreateStaffProfileCommand>
{
    public CreateStaffProfileCommandValidator(ITenantDbContext db)
    {
        RuleFor(x => x.FirstName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.firstname_required")
            .MaximumLength(100).WithMessage("errors.staff.firstname_too_long");

        RuleFor(x => x.LastName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.lastname_required")
            .MaximumLength(100).WithMessage("errors.staff.lastname_too_long");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.email_required")
            .MaximumLength(254).WithMessage("errors.staff.email_too_long")
            .EmailAddress().WithMessage("errors.staff.email_invalid");

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.staff.phone_required")
            .MaximumLength(30).WithMessage("errors.staff.phone_too_long")
            .Matches(CreateLocationCommandValidator.PhonePattern).WithMessage("errors.staff.phone_invalid");

        // FR-003: required when the *target* role is Staff — either the new account's own Role,
        // or (opt-in path) the existing account's Role. A missing ExistingTenantUserId account
        // is left to the handler to report as TenantUserNotFound, not a validation failure.
        RuleFor(x => x.QualificationLevel)
            .MustAsync(async (command, qualificationLevel, ct) =>
            {
                var targetRole = command.Role;
                if (command.ExistingTenantUserId is Guid existingId)
                {
                    var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == existingId, ct);
                    if (existing is not null)
                        targetRole = existing.Role;
                }

                return targetRole != UserRole.Staff || qualificationLevel is not null;
            })
            .WithMessage("errors.staff.qualification_required");
    }
}
