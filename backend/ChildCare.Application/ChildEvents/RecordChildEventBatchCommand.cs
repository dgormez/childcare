using System.Text.Json;
using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

// LocationId/GroupId/RecordedByDeviceId come from the recording device's own JWT claims
// (endpoint layer resolves them) — never client-supplied, same as RecordChildEventCommand
// (research.md R3: the batch does not invent a per-child scope model).
public record ChildEventBatchItem(Guid ChildId, Guid Id);

public record RecordChildEventBatchCommand(
    IReadOnlyList<ChildEventBatchItem> Items,
    Guid LocationId,
    Guid GroupId,
    Guid RecordedByDeviceId,
    ChildEventType EventType,
    DateTime OccurredAt,
    DateTime? EndedAt,
    JsonElement Payload,
    bool VisibleToParent) : IRequest<ChildEventBatchResult>;

public class RecordChildEventBatchCommandValidator : AbstractValidator<RecordChildEventBatchCommand>
{
    public const int MaxBatchSize = 30;

    // spec.md FR-001/FR-002 — the eight event types that make sense as a shared group event;
    // temperature/medication/weight/growth_check require per-child values and stay single-child.
    private static readonly ChildEventType[] SupportedTypes =
    [
        ChildEventType.Sleep, ChildEventType.Diaper, ChildEventType.FeedingBottle,
        ChildEventType.FeedingSolid, ChildEventType.Mood, ChildEventType.Activity,
        ChildEventType.Note, ChildEventType.Custom,
    ];

    public RecordChildEventBatchCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Items.Count)
            .LessThanOrEqualTo(MaxBatchSize)
            .WithMessage("errors.child_events.batch_too_large");

        RuleFor(x => x.EventType)
            .Must(type => SupportedTypes.Contains(type))
            .WithMessage("errors.child_events.batch_type_not_supported");

        // Mirrors RecordChildEventCommandValidator's equivalent rule (feature 009).
        RuleFor(x => x.EndedAt)
            .Must((command, endedAt) => endedAt is null || command.EventType == ChildEventType.Sleep)
            .WithMessage("errors.child_events.ended_at_not_applicable");

        // The payload is shared across every child in the batch (one EventType/Payload for the
        // whole request), so it is validated once here — a failure rejects the whole batch via
        // the standard ValidationBehavior pipeline (422), before any child is processed, exactly
        // like the two rules above. See data-model.md's "Correction made while implementing":
        // this is why ChildEventBatchFailureReason has no per-child ValidationFailed value.
        RuleFor(x => x)
            .Custom((command, context) =>
            {
                if (!SupportedTypes.Contains(command.EventType))
                    return; // already reported by the EventType rule above; avoid a confusing second error
                foreach (var failure in ChildEventPayloadValidator.Validate(command.EventType, command.Payload, command.EndedAt))
                    context.AddFailure(failure);
            });
    }
}

// No ITemperatureAlertService dependency: Temperature is not in SupportedTypes above (it needs a
// per-child value and stays single-child, spec.md FR-002), so the alert path RecordChildEventCommand
// has can never trigger here — omitted rather than kept as dead code for a type this command
// rejects before ever reaching the handler.
public class RecordChildEventBatchCommandHandler(
    ITenantDbContext db,
    IShiftAttributionService attribution)
    : IRequestHandler<RecordChildEventBatchCommand, ChildEventBatchResult>
{
    public async Task<ChildEventBatchResult> Handle(RecordChildEventBatchCommand request, CancellationToken cancellationToken)
    {
        var created = new List<ChildEventBatchCreated>();
        var errors = new List<ChildEventBatchError>();
        var seenChildIds = new HashSet<Guid>();
        var today = BelgianCalendarDay.Today();

        // Resolved once, shared by every child's row — mirrors the single-child endpoint
        // (research.md R2), since attribution depends only on location/group/time, not the child.
        var recordedBy = await attribution.ResolveRecordedByAsync(
            request.LocationId, request.GroupId, request.OccurredAt, cancellationToken);

        foreach (var item in request.Items)
        {
            // research.md R5: dedupe by ChildId server-side — first occurrence wins.
            if (!seenChildIds.Add(item.ChildId))
                continue;

            // FR-013a-style idempotency (research.md R5/CHK017): a retried replay resending the
            // same client-generated id for a child that already succeeded returns that success
            // again instead of creating a duplicate ChildEvent.
            var existing = await db.ChildEvents.FirstOrDefaultAsync(e => e.Id == item.Id, cancellationToken);
            if (existing is not null)
            {
                created.Add(new ChildEventBatchCreated(item.ChildId, existing.Id));
                continue;
            }

            var childExists = await db.Children.AnyAsync(c => c.Id == item.ChildId, cancellationToken);
            if (!childExists)
            {
                errors.Add(new ChildEventBatchError(item.ChildId, ChildEventBatchFailureReason.ChildNotFound));
                continue;
            }

            // research.md R4: new, batch-specific presence check — the single-child endpoint has
            // no equivalent, since its selection-to-submit window is a few seconds, not the
            // longer window a multi-select batch can take to assemble.
            var isPresent = await db.AttendanceRecords.AnyAsync(a =>
                a.ChildId == item.ChildId &&
                a.LocationId == request.LocationId &&
                a.Date == today &&
                a.Status == AttendanceStatus.Present &&
                a.CheckOutAt == null, cancellationToken);
            if (!isPresent)
            {
                errors.Add(new ChildEventBatchError(item.ChildId, ChildEventBatchFailureReason.NotPresent));
                continue;
            }

            var childEvent = new ChildEvent
            {
                Id = item.Id,
                ChildId = item.ChildId,
                LocationId = request.LocationId,
                GroupId = request.GroupId,
                EventType = request.EventType,
                OccurredAt = request.OccurredAt,
                EndedAt = request.EndedAt,
                Payload = SleepDurationEnricher.Enrich(request.EventType, request.Payload.GetRawText(), request.OccurredAt, request.EndedAt),
                VisibleToParent = request.VisibleToParent,
                RecordedBy = recordedBy.ToList(),
                AdministeredBy = null, // batch-eligible types never carry administeredByStaffId
                RecordedByDeviceId = request.RecordedByDeviceId,
            };

            db.ChildEvents.Add(childEvent);
            // research.md R5: one SaveChangesAsync per child — this is what makes a later
            // child's failure unable to roll back an earlier child's already-committed success.
            await db.SaveChangesAsync(cancellationToken);
            created.Add(new ChildEventBatchCreated(item.ChildId, childEvent.Id));
        }

        return new ChildEventBatchResult(created, errors);
    }
}
