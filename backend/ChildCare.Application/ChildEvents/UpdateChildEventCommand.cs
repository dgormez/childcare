using System.Text.Json;
using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

// FR-006/FR-007: isDirector/requestingDeviceLocationId come from the caller's auth claims
// (endpoint layer resolves them), never client-supplied in the request body.
public record UpdateChildEventCommand(
    Guid Id,
    bool IsDirector,
    Guid? RequestingDeviceLocationId,
    DateTime? EndedAt,
    JsonElement? Payload,
    bool? VisibleToParent,
    Guid? AdministeredByStaffId) : IRequest<ChildEventResult>;

public class UpdateChildEventCommandValidator : AbstractValidator<UpdateChildEventCommand>
{
    public UpdateChildEventCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class UpdateChildEventCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateChildEventCommand, ChildEventResult>
{
    public async Task<ChildEventResult> Handle(UpdateChildEventCommand request, CancellationToken cancellationToken)
    {
        var childEvent = await db.ChildEvents.FirstOrDefaultAsync(
            e => e.Id == request.Id && e.DeletedAt == null, cancellationToken);
        if (childEvent is null)
            return ChildEventResult.Fail(ChildEventFailure.NotFound);

        if (!ChildEventEditWindowPolicy.CanModify(childEvent, request.IsDirector, request.RequestingDeviceLocationId))
            return ChildEventResult.Fail(ChildEventFailure.EditWindowExpired);

        // Convergence finding F1/F2 — same data-model.md restrictions RecordChildEventCommand
        // enforces, applied here against the *existing* event's EventType (this command doesn't
        // carry one of its own).
        if (request.EndedAt.HasValue && childEvent.EventType != ChildEventType.Sleep)
            throw new ValidationException([new ValidationFailure("endedAt", "errors.child_events.ended_at_not_applicable")]);

        if (request.AdministeredByStaffId.HasValue && childEvent.EventType is not (ChildEventType.Medication or ChildEventType.Temperature))
        {
            throw new ValidationException([
                new ValidationFailure("administeredByStaffId", "errors.child_events.administered_by_not_applicable"),
            ]);
        }

        if (request.VisibleToParent.HasValue)
            childEvent.VisibleToParent = request.VisibleToParent.Value;

        if (request.AdministeredByStaffId.HasValue)
            childEvent.AdministeredBy = request.AdministeredByStaffId;

        var payloadChanged = request.Payload.HasValue;
        var mergedPayloadJson = payloadChanged ? request.Payload!.Value.GetRawText() : childEvent.Payload;

        if (request.EndedAt.HasValue)
            childEvent.EndedAt = request.EndedAt;

        // FR-002/FR-002a: re-validate whenever EndedAt or Payload changes — completing a sleep
        // event makes `quality` newly required (data-model.md) even if this PATCH only set
        // `endedAt` and never touched the payload at all. This command doesn't carry EventType,
        // so it can't be validated by the stateless FluentValidation pipeline the way
        // RecordChildEventCommand is (research.md R1) — thrown here instead, the same global
        // exception handler still produces the identical 422 { errorKey: "errors.validation",
        // fieldErrors } shape.
        if (payloadChanged || request.EndedAt.HasValue)
        {
            var mergedPayloadElement = JsonDocument.Parse(mergedPayloadJson).RootElement;
            var failures = ChildEventPayloadValidator.Validate(childEvent.EventType, mergedPayloadElement, childEvent.EndedAt);
            if (failures.Count > 0)
                throw new ValidationException(failures);

            childEvent.Payload = SleepDurationEnricher.Enrich(
                childEvent.EventType, mergedPayloadJson, childEvent.OccurredAt, childEvent.EndedAt);
        }

        childEvent.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ChildEventResult.Success(ChildEventMapper.ToResponse(childEvent));
    }
}
