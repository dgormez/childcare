using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

// CallerRole/CallerTenantUserId (031-photo-lifecycle-governance FR-011): staff must be scoped
// to their assigned location(s) — reusing GetChildByIdQuery's StaffLocationEligibility check.
public record UpdateHealthRecordCommand(
    Guid ChildId,
    Guid Id,
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string? CallerRole = null,
    Guid? CallerTenantUserId = null) : IRequest<HealthRecordResult>;

public class UpdateHealthRecordCommandValidator : AbstractValidator<UpdateHealthRecordCommand>
{
    public UpdateHealthRecordCommandValidator()
    {
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

        RuleFor(x => x.ValidUntil)
            .Must((cmd, validUntil) => !validUntil.HasValue || !cmd.ValidFrom.HasValue || validUntil.Value >= cmd.ValidFrom.Value)
            .WithMessage("errors.health_records.valid_until_before_valid_from");
    }
}

public class UpdateHealthRecordCommandHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<UpdateHealthRecordCommand, HealthRecordResult>
{
    public async Task<HealthRecordResult> Handle(UpdateHealthRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.HealthRecords
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.ChildId == request.ChildId && r.DeletedAt == null, cancellationToken);
        if (record is null)
            return HealthRecordResult.Fail(HealthRecordFailure.NotFound);

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
                return HealthRecordResult.Fail(HealthRecordFailure.NotFound);
        }

        HealthRecordMapper.TryParseRecordType(request.RecordType, out var recordType);

        record.RecordType = recordType;
        record.Title = request.Title;
        record.Description = request.Description;
        record.ValidFrom = request.ValidFrom;
        record.ValidUntil = request.ValidUntil;
        record.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return HealthRecordResult.Success(await HealthRecordMapper.ToResponseAsync(record, storage, cancellationToken));
    }
}
