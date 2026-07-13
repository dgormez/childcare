using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

// RecordedBy comes from the caller's JWT (endpoint layer resolves it) — never client-supplied.
public record CreateHealthRecordCommand(
    Guid ChildId,
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    Guid RecordedBy) : IRequest<HealthRecordResult>;

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
