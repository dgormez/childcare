using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChildCare.Application.Invoices;

/// <summary>
/// Feature 014 — data-model.md's InvoiceLineItems JSONB shape. Serialized to/from
/// Invoice.LineItems (raw JSON text, mirrors ChildEvent.Payload's existing precedent — see
/// Invoice.cs's field comment).
/// </summary>
public record InvoiceLineItems(
    int PresentDays,
    int UnjustifiedAbsentDays,
    int DailyRateCents,
    int ClosureDaysExcluded,
    int DaysMin5u,
    int DaysMin11u,
    IReadOnlyList<InvoiceExtraCharge> ExtraCharges,
    // FR-020: placeholder for a future IKT subsidy amount. Always null in Phase 1 — never
    // computed, subtracted from any total, or displayed anywhere; exists only so a later
    // subsidy feature extends this shape instead of introducing a second JSONB schema.
    int? SubsidyAmountCents = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static InvoiceLineItems FromJson(string json) =>
        JsonSerializer.Deserialize<InvoiceLineItems>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invoice.LineItems JSON could not be deserialized.");

    public int SubtotalCents => (PresentDays + UnjustifiedAbsentDays) * DailyRateCents;

    public int TotalCents => SubtotalCents + ExtraCharges.Sum(c => c.AmountCents);
}

public record InvoiceExtraCharge(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("amountCents")] int AmountCents);
