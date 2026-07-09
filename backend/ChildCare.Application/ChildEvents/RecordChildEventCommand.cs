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
// (endpoint layer resolves them, mirrors CheckInCommand's pattern) — never client-supplied.
public record RecordChildEventCommand(
    Guid? Id,
    Guid ChildId,
    Guid LocationId,
    Guid GroupId,
    Guid RecordedByDeviceId,
    ChildEventType EventType,
    DateTime OccurredAt,
    DateTime? EndedAt,
    JsonElement Payload,
    bool VisibleToParent,
    Guid? AdministeredByStaffId) : IRequest<ChildEventResult>;

public class RecordChildEventCommandValidator : AbstractValidator<RecordChildEventCommand>
{
    public RecordChildEventCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.OccurredAt).NotEmpty();

        // Convergence finding F1/F2 — data-model.md restricts these two fields to specific
        // event types; nothing previously enforced that restriction.
        RuleFor(x => x.EndedAt)
            .Must((command, endedAt) => endedAt is null || command.EventType == ChildEventType.Sleep)
            .WithMessage("errors.child_events.ended_at_not_applicable");

        RuleFor(x => x.AdministeredByStaffId)
            .Must((command, administeredBy) =>
                administeredBy is null || command.EventType is ChildEventType.Medication or ChildEventType.Temperature)
            .WithMessage("errors.child_events.administered_by_not_applicable");

        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (var failure in ChildEventPayloadValidator.Validate(command.EventType, command.Payload, command.EndedAt))
                context.AddFailure(failure);
        });
    }
}

public class RecordChildEventCommandHandler(
    ITenantDbContext db,
    IShiftAttributionService attribution,
    ITemperatureAlertService temperatureAlerts)
    : IRequestHandler<RecordChildEventCommand, ChildEventResult>
{
    public async Task<ChildEventResult> Handle(RecordChildEventCommand request, CancellationToken cancellationToken)
    {
        // FR-013a: create is idempotent by client-generated id — a retried request after a
        // timeout whose original request actually succeeded returns the existing record.
        if (request.Id.HasValue)
        {
            var existing = await db.ChildEvents.FirstOrDefaultAsync(e => e.Id == request.Id.Value, cancellationToken);
            if (existing is not null)
                return ChildEventResult.Success(ChildEventMapper.ToResponse(existing));
        }

        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return ChildEventResult.Fail(ChildEventFailure.ChildNotFound);

        // research.md R2: reuses feature 008a's IShiftAttributionService rather than building a
        // second recorded-by resolver.
        var recordedBy = await attribution.ResolveRecordedByAsync(
            request.LocationId, request.GroupId, request.OccurredAt, cancellationToken);

        var childEvent = new ChildEvent
        {
            Id = request.Id ?? Guid.NewGuid(),
            ChildId = request.ChildId,
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            EventType = request.EventType,
            OccurredAt = request.OccurredAt,
            EndedAt = request.EndedAt,
            Payload = SleepDurationEnricher.Enrich(request.EventType, request.Payload.GetRawText(), request.OccurredAt, request.EndedAt),
            VisibleToParent = request.VisibleToParent,
            RecordedBy = recordedBy.ToList(),
            AdministeredBy = request.AdministeredByStaffId,
            RecordedByDeviceId = request.RecordedByDeviceId,
        };

        db.ChildEvents.Add(childEvent);
        await db.SaveChangesAsync(cancellationToken);

        // FR-010/FR-011/FR-011a/FR-011b: fire-and-log only, never blocks or fails the save.
        if (childEvent.EventType == ChildEventType.Temperature)
        {
            var celsius = ExtractCelsius(request.Payload);
            if (celsius > 38.0m)
                await temperatureAlerts.NotifyAsync(childEvent.ChildId, celsius, cancellationToken);
        }

        return ChildEventResult.Success(ChildEventMapper.ToResponse(childEvent));
    }

    private static decimal ExtractCelsius(JsonElement payload) =>
        payload.TryGetProperty("celsius", out var el) && el.TryGetDecimal(out var value) ? value : 0m;
}
