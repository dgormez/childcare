using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// meal_preference_change_requests (data-model.md, feature 013e) — one row per parent-submitted
// request. RequestedBy stores the submitting parent's TenantUserId (same convention as
// DayReservation.RequestedBy). Approving one writes through to the existing MealPreference (013d)
// entity via UpsertMealPreferenceCommand rather than duplicating its data (research.md R1).
public class MealPreferenceChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public Guid RequestedBy { get; set; }

    // MealTexture enum as string; null = no change requested to texture (013d's convention).
    public string? NewTexture { get; set; }

    // DietaryType enum list as Postgres text[]; null = no change requested (013d's convention).
    public List<string>? NewDietaryType { get; set; }

    public string? Notes { get; set; }

    public MealPreferenceChangeRequestStatus Status { get; set; } = MealPreferenceChangeRequestStatus.Pending;

    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }

    // Director's optional rejection reason — kept distinct from the parent's own Notes,
    // mirroring DayReservation.Reason (requester) vs. DayReservation.DirectorNotes (decider).
    public string? DecisionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
