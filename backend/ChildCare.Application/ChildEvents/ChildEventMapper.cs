using System.Text.Json;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.ChildEvents;

internal static class ChildEventMapper
{
    public static ChildEventResponse ToResponse(ChildEvent e) => new(
        e.Id,
        e.ChildId,
        e.EventType.ToWireString(),
        e.OccurredAt,
        e.EndedAt,
        JsonDocument.Parse(e.Payload).RootElement.Clone(),
        e.VisibleToParent,
        e.RecordedBy,
        e.AdministeredBy,
        e.CreatedAt,
        e.UpdatedAt);
}
