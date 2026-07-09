using System.Text.Json;
using ChildCare.Domain.Enums;
using FluentValidation.Results;

namespace ChildCare.Application.ChildEvents;

/// <summary>
/// Per-EventType payload shape/range validation (data-model.md Validation Rules table, FR-002/
/// FR-002a). A plain static helper rather than an AbstractValidator itself, so the exact same
/// logic can be reused two ways: as a custom rule inside RecordChildEventCommandValidator (runs
/// through the standard MediatR ValidationBehavior pipeline, constitution Principle III) for
/// create, and called directly from UpdateChildEventCommandHandler to re-validate a merged
/// payload against the *existing* event's EventType — something a stateless pipeline validator
/// can't do for update, since UpdateChildEventCommand doesn't carry EventType.
/// </summary>
public static class ChildEventPayloadValidator
{
    private static readonly IReadOnlyDictionary<ChildEventType, string[]> AllowedFields = new Dictionary<ChildEventType, string[]>
    {
        [ChildEventType.Sleep] = ["quality", "durationMinutes"],
        [ChildEventType.Temperature] = ["celsius"],
        [ChildEventType.Medication] = ["name", "doseDescription", "reason", "nextDoseNotBefore"],
        [ChildEventType.FeedingBottle] = ["ml"],
        [ChildEventType.FeedingSolid] = ["description"],
        [ChildEventType.Diaper] = ["type", "notes"],
        [ChildEventType.Mood] = ["value"],
        [ChildEventType.Activity] = ["description"],
        [ChildEventType.Note] = ["text"],
        [ChildEventType.Weight] = ["kg"],
        [ChildEventType.GrowthCheck] = ["weightKg", "heightCm", "headCm"],
        [ChildEventType.Custom] = ["label", "text"],
    };

    private static readonly string[] MedicationNames = ["perdolan", "nurofen", "antibiotics", "other"];
    private static readonly string[] DiaperTypes = ["wet", "dirty", "both"];
    private static readonly string[] MoodValues = ["great", "good", "okay", "difficult"];
    private static readonly string[] SleepQualities = ["good", "okay", "restless"];
    private const int CustomLabelMaxLength = 100;

    /// <param name="endedAt">
    /// Only relevant for <see cref="ChildEventType.Sleep"/> — <c>quality</c> is required only
    /// once the event is completed (data-model.md: "quality only on completion").
    /// </param>
    public static IReadOnlyList<ValidationFailure> Validate(ChildEventType eventType, JsonElement payload, DateTime? endedAt = null)
    {
        var failures = new List<ValidationFailure>();

        if (payload.ValueKind != JsonValueKind.Object)
        {
            failures.Add(new ValidationFailure("payload", "errors.child_events.invalid_payload"));
            return failures;
        }

        var allowed = AllowedFields[eventType];
        foreach (var prop in payload.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
                failures.Add(new ValidationFailure(prop.Name, "errors.child_events.unexpected_field"));
        }

        switch (eventType)
        {
            case ChildEventType.Sleep:
                if (endedAt.HasValue)
                    RequireEnum(payload, "quality", SleepQualities, failures);
                break;

            case ChildEventType.Temperature:
                RequireDecimalInRange(payload, "celsius", 30.0m, 42.0m, failures);
                break;

            case ChildEventType.Medication:
                RequireEnum(payload, "name", MedicationNames, failures);
                RequireString(payload, "doseDescription", failures);
                RequireString(payload, "reason", failures);
                break;

            case ChildEventType.FeedingBottle:
                RequireInt(payload, "ml", failures);
                break;

            case ChildEventType.FeedingSolid:
                RequireString(payload, "description", failures);
                break;

            case ChildEventType.Diaper:
                RequireEnum(payload, "type", DiaperTypes, failures);
                break;

            case ChildEventType.Mood:
                RequireEnum(payload, "value", MoodValues, failures);
                break;

            case ChildEventType.Activity:
                RequireString(payload, "description", failures);
                break;

            case ChildEventType.Note:
                RequireString(payload, "text", failures);
                break;

            case ChildEventType.Weight:
                RequireDecimalInRange(payload, "kg", 0m, 30m, failures);
                break;

            case ChildEventType.GrowthCheck:
                ValidateGrowthCheck(payload, failures);
                break;

            case ChildEventType.Custom:
                RequireString(payload, "label", failures);
                RequireMaxLength(payload, "label", CustomLabelMaxLength, failures);
                break;
        }

        return failures;
    }

    private static void ValidateGrowthCheck(JsonElement payload, List<ValidationFailure> failures)
    {
        var weightKg = OptionalDecimal(payload, "weightKg");
        var heightCm = OptionalDecimal(payload, "heightCm");
        var headCm = OptionalDecimal(payload, "headCm");

        // "Any subset is valid" means any *non-empty* subset (2026-07-08 clarification, FR-002,
        // unchanged by the measurement -> growth_check rename, feature 009a's FR-009).
        if (weightKg is null && heightCm is null && headCm is null)
            failures.Add(new ValidationFailure("growthCheck", "errors.child_events.empty_growth_check"));

        InRangeIfPresent("weightKg", weightKg, 0m, 30m, failures);
        InRangeIfPresent("heightCm", heightCm, 30m, 120m, failures);
        InRangeIfPresent("headCm", headCm, 25m, 60m, failures);
    }

    private static void RequireString(JsonElement payload, string field, List<ValidationFailure> failures)
    {
        if (!payload.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(el.GetString()))
            failures.Add(new ValidationFailure(field, "errors.child_events.field_required"));
    }

    private static void RequireMaxLength(JsonElement payload, string field, int maxLength, List<ValidationFailure> failures)
    {
        if (payload.TryGetProperty(field, out var el) && el.ValueKind == JsonValueKind.String
            && el.GetString() is { } value && value.Length > maxLength)
            failures.Add(new ValidationFailure(field, "errors.child_events.value_too_long"));
    }

    private static void RequireInt(JsonElement payload, string field, List<ValidationFailure> failures)
    {
        if (!payload.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out _))
            failures.Add(new ValidationFailure(field, "errors.child_events.field_required"));
    }

    private static void RequireEnum(JsonElement payload, string field, string[] allowedValues, List<ValidationFailure> failures)
    {
        if (!payload.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.String || el.GetString() is not { } value)
        {
            failures.Add(new ValidationFailure(field, "errors.child_events.field_required"));
            return;
        }

        if (!allowedValues.Contains(value))
            failures.Add(new ValidationFailure(field, "errors.child_events.invalid_enum_value"));
    }

    private static void RequireDecimalInRange(JsonElement payload, string field, decimal min, decimal max, List<ValidationFailure> failures)
    {
        if (!payload.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetDecimal(out var value))
        {
            failures.Add(new ValidationFailure(field, "errors.child_events.field_required"));
            return;
        }

        if (value < min || value > max)
            failures.Add(new ValidationFailure(field, "errors.child_events.value_out_of_range"));
    }

    private static decimal? OptionalDecimal(JsonElement payload, string field) =>
        payload.TryGetProperty(field, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var value)
            ? value
            : null;

    private static void InRangeIfPresent(string field, decimal? value, decimal min, decimal max, List<ValidationFailure> failures)
    {
        if (value.HasValue && (value < min || value > max))
            failures.Add(new ValidationFailure(field, "errors.child_events.value_out_of_range"));
    }
}
