using System.Text.Json.Nodes;
using ChildCare.Domain.Enums;

namespace ChildCare.Application.ChildEvents;

/// <summary>
/// FR-004/data-model.md: <c>durationMinutes</c> is computed server-side — never client-trusted —
/// once a sleep event is completed, and stored in the payload for query efficiency.
/// </summary>
internal static class SleepDurationEnricher
{
    public static string Enrich(ChildEventType eventType, string payloadJson, DateTime occurredAt, DateTime? endedAt)
    {
        if (eventType != ChildEventType.Sleep || !endedAt.HasValue)
            return payloadJson;

        var node = JsonNode.Parse(payloadJson)!.AsObject();
        node["durationMinutes"] = (int)(endedAt.Value - occurredAt).TotalMinutes;
        return node.ToJsonString();
    }
}
