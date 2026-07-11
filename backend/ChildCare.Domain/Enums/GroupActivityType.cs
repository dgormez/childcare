namespace ChildCare.Domain.Enums;

// Group-level shared moments (data-model.md) — distinct from ChildEventType.Activity, which is
// per-child. Six fixed types per spec.md FR-002.
public enum GroupActivityType
{
    Outdoor,
    Creative,
    Music,
    Story,
    Celebration,
    Other,
}
