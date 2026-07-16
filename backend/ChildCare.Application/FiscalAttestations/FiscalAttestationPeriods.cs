using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChildCare.Application.FiscalAttestations;

/// <summary>
/// Feature 015 — data-model.md's FiscalAttestationPeriod JSONB shape. Serialized to/from
/// FiscalAttestation.Periods (raw JSON text, mirrors Invoice.LineItems' existing precedent, 014).
/// Up to 4 entries per attestation (spec.md FR-004) — DailyRateCents is null only when a
/// >4-period consolidation merge occurred (research.md R3 step 4).
/// </summary>
public record FiscalAttestationPeriod(
    [property: JsonPropertyName("periodStart")] DateOnly PeriodStart,
    [property: JsonPropertyName("periodEnd")] DateOnly PeriodEnd,
    [property: JsonPropertyName("days")] int Days,
    [property: JsonPropertyName("amountCents")] int AmountCents,
    [property: JsonPropertyName("dailyRateCents")] int? DailyRateCents);

public static class FiscalAttestationPeriods
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(IReadOnlyList<FiscalAttestationPeriod> periods) =>
        JsonSerializer.Serialize(periods, JsonOptions);

    public static IReadOnlyList<FiscalAttestationPeriod> FromJson(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<FiscalAttestationPeriod>>(json, JsonOptions) ?? [];
}
