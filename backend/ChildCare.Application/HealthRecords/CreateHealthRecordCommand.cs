using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

// RecordedBy comes from the caller's JWT (endpoint layer resolves it) — never client-supplied.
// CallerRole/CallerTenantUserId (031-photo-lifecycle-governance FR-011): now that staff (not
// just directors) can create health records, staff must be scoped to their assigned
// location(s) — reusing GetChildByIdQuery's exact StaffLocationEligibility check.
public record CreateHealthRecordCommand(
    Guid ChildId,
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    Guid RecordedBy,
    string? CallerRole = null,
    Guid? CallerTenantUserId = null) : IRequest<HealthRecordResult>;

public class CreateHealthRecordCommandValidator : AbstractValidator<CreateHealthRecordCommand>
{
    public CreateHealthRecordCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();

        RuleFor(x => x.RecordType)
            .Must(v => HealthRecordMapper.TryParseRecordType(v, out _))
            .WithMessage("errors.health_records.record_type_invalid");

        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.health_records.title_required")
            .MaximumLength(200).WithMessage("errors.health_records.title_too_long");

        RuleFor(x => x.Description)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.health_records.description_required")
            .MaximumLength(2000).WithMessage("errors.health_records.description_too_long");

        // FR-004: an optional validity window; if both are set, ValidUntil must not precede ValidFrom.
        RuleFor(x => x.ValidUntil)
            .Must((cmd, validUntil) => !validUntil.HasValue || !cmd.ValidFrom.HasValue || validUntil.Value >= cmd.ValidFrom.Value)
            .WithMessage("errors.health_records.valid_until_before_valid_from");
    }
}

public class CreateHealthRecordCommandHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<CreateHealthRecordCommand, HealthRecordResult>
{
    public async Task<HealthRecordResult> Handle(CreateHealthRecordCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return HealthRecordResult.Fail(HealthRecordFailure.ChildNotFound);

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            var isInScope = await db.ChildGroupAssignments
                .Where(a => a.ChildId == request.ChildId && a.EndDate == null)
                .Join(db.Groups, a => a.GroupId, g => g.Id, (a, g) => g.LocationId)
                .AnyAsync(locationId => eligibleLocationIds.Contains(locationId), cancellationToken);
            if (!isInScope)
                return HealthRecordResult.Fail(HealthRecordFailure.ChildNotFound);
        }

        HealthRecordMapper.TryParseRecordType(request.RecordType, out var recordType);

        var record = new HealthRecord
        {
            ChildId = request.ChildId,
            RecordType = recordType,
            Title = request.Title,
            Description = request.Description,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            RecordedBy = request.RecordedBy,
        };

        db.HealthRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return HealthRecordResult.Success(await HealthRecordMapper.ToResponseAsync(record, storage, cancellationToken));
    }
}
