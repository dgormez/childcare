using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DevelopmentalMilestones;

// LocationId/GroupId come from the recording device's own JWT claims (endpoint layer resolves
// them, mirrors RecordChildEventCommand's pattern) — never client-supplied. No corresponding
// update/delete command exists anywhere for this entity — immutability is structural
// (research.md R3, FR-003), not a policy check on an editable resource.
public record RecordMilestoneObservationCommand(
    Guid ChildId,
    Guid MilestoneId,
    Guid LocationId,
    Guid GroupId,
    string Status,
    DateOnly ObservedAt,
    string? Notes) : IRequest<MilestoneObservationResult>;

public class RecordMilestoneObservationCommandValidator : AbstractValidator<RecordMilestoneObservationCommand>
{
    public RecordMilestoneObservationCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.MilestoneId).NotEmpty();

        // FR-012: rejected by validation before it reaches the database.
        RuleFor(x => x.Status)
            .Must(s => MilestoneObservationStatusExtensions.TryParseWireString(s, out _))
            .WithMessage("errors.milestones.invalid_status");

        RuleFor(x => x.ObservedAt)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("errors.milestones.observed_at_in_future");

        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("errors.milestones.notes_too_long");
    }
}

public class RecordMilestoneObservationCommandHandler(
    ITenantDbContext db,
    IPublicDbContext publicDb,
    IShiftAttributionService attribution)
    : IRequestHandler<RecordMilestoneObservationCommand, MilestoneObservationResult>
{
    public async Task<MilestoneObservationResult> Handle(RecordMilestoneObservationCommand request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return MilestoneObservationResult.Fail(MilestoneObservationFailure.ChildNotFound);

        var milestoneExists = await publicDb.DevelopmentalMilestones.AnyAsync(m => m.Id == request.MilestoneId, cancellationToken);
        if (!milestoneExists)
            return MilestoneObservationResult.Fail(MilestoneObservationFailure.MilestoneNotFound);

        MilestoneObservationStatusExtensions.TryParseWireString(request.Status, out var status);

        var observedAtUtc = request.ObservedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var observedBy = await attribution.ResolveRecordedByAsync(request.LocationId, request.GroupId, observedAtUtc, cancellationToken);

        var observation = new ChildMilestoneObservation
        {
            ChildId = request.ChildId,
            MilestoneId = request.MilestoneId,
            Status = status,
            ObservedAt = request.ObservedAt,
            ObservedBy = observedBy.ToList(),
            Notes = request.Notes,
        };

        db.ChildMilestoneObservations.Add(observation);
        await db.SaveChangesAsync(cancellationToken);

        return MilestoneObservationResult.Success(new MilestoneObservationResponse(
            observation.Id, observation.Status.ToWireString(), observation.ObservedAt, observation.Notes, observation.CreatedAt));
    }
}

public class MilestoneObservationResult
{
    public MilestoneObservationResponse? Response { get; private init; }
    public MilestoneObservationFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static MilestoneObservationResult Success(MilestoneObservationResponse response) => new() { Response = response };
    public static MilestoneObservationResult Fail(MilestoneObservationFailure failure) => new() { Failure = failure };
}

public enum MilestoneObservationFailure
{
    ChildNotFound,
    MilestoneNotFound,
}
