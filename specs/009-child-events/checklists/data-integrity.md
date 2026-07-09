# Data Integrity & Sync Requirements Checklist: Child Event Timeline

**Purpose**: Validate the quality (completeness, clarity, consistency, measurability) of
requirements covering per-type payload validation, offline sync/merge correctness, the
temperature alert threshold, same-day edit authorization, and daily-summary visibility filtering
**Created**: 2026-07-08
**Feature**: [spec.md](../spec.md), [data-model.md](../data-model.md),
[contracts/child-events-api.md](../contracts/child-events-api.md)

**Note**: This checklist tests whether the *requirements* are well-specified — not whether the
implementation works. Depth: standard (pre-implementation self-review). Audience: implementer
(this session, before `/speckit-implement`). All findings below were resolved by updating
spec.md/data-model.md/research.md/contracts (2026-07-08 checklist-follow-up clarification
session) rather than left as advisory debt, per this project's standing process rule.

## Payload Validation Per Event Type

- [x] CHK001 - Is the required-vs-optional field set for every one of the 11 event types
  explicitly enumerated in one place, rather than inferable only from prose? [Completeness,
  data-model.md Validation Rules table] — **Resolved**: already satisfied by data-model.md's
  Validation Rules table; no change needed.
- [x] CHK002 - Is the system's behavior on a payload containing fields *not* belonging to the
  selected `EventType` specified as clearly as the behavior for a *missing* required field?
  [Clarity, Spec §FR-002] — **Resolved**: already satisfied; FR-002 states both cases explicitly
  and symmetrically.
- [x] CHK003 - Are the enumerated string values for constrained fields (`sleep.quality`,
  `medication.name`, `diaper.type`, `mood.value`) each a closed, exhaustive list with no
  "other/free-text" escape hatch left ambiguous? [Clarity, Spec event-type table] — **Resolved**:
  already satisfied; `medication.name`'s `"other"` is itself a closed enum value, not a free-text
  escape hatch.
- [x] CHK004 - For `measurement`, is "any subset of the three fields is valid" reconciled with a
  concrete minimum (e.g., is an entirely empty `measurement` payload — all three fields absent —
  explicitly rejected or explicitly allowed)? [Ambiguity, data-model.md Validation Rules table] —
  **Resolved**: spec.md FR-002 and data-model.md's Validation Rules table now state a
  non-empty-subset requirement (2026-07-08 clarification).
- [x] CHK005 - Is the numeric range/precision for decimal fields (`celsius`, `kg`, `weightKg`,
  `heightCm`, `headCm`) specified anywhere, or only their type? [Gap] — **Resolved**: new
  spec.md FR-002a defines explicit min/max ranges for every numeric field, reflected in
  data-model.md's Validation Rules table.
- [x] CHK006 - Is the validation error response's granularity specified (one generic
  `invalid_payload` key vs. a per-field breakdown), and is that consistent with how other
  features in this codebase report FluentValidation failures? [Consistency, Spec §FR-002] —
  **Resolved**: spec.md Assumptions and data-model.md now state this reuses the existing
  FluentValidation pipeline behavior's standard per-field response shape, not a bespoke format.

## Offline Sync / Merge Correctness

- [x] CHK007 - Is the exact matching key used to find "the still-queued create for this sleep
  event" (client-generated id? entity_type + a secondary field?) specified precisely enough that
  two implementers would build the same lookup? [Clarity, research.md R3] — **Resolved**:
  research.md R3 now specifies the exact match (`entity_type = 'child_event' AND operation =
  'create'`, matched by the event's client-generated `id` in the payload).
- [x] CHK008 - Is the outcome specified for the case where a sleep-end arrives locally but the
  matching queued create row was *already popped for sync* (i.e., mid-flight, neither pending
  nor yet confirmed) — a race the "still queued vs. already synced" binary doesn't obviously
  cover? [Gap, Edge Case] — **Resolved**: research.md R3 specifies the full race resolution,
  including a required fix to `syncEngine.ts`'s `replay()` (now tasks.md T022a) to re-read the
  current payload before transmitting, closing the gap where a stale in-memory snapshot could
  ship without the merge.
- [x] CHK009 - Is "two tablets both ending the same sleep event" fully specified in terms of
  which timestamp wins when the two end times differ (server-received-order vs.
  client-`occurred_at`-order), or only that "server timestamp wins" without defining which
  timestamp that is? [Ambiguity, Spec Edge Cases / FR-015] — **Resolved**: spec.md FR-015 now
  states explicitly that the update received and processed *last* by the server wins (arrival
  order), not the client-reported end time.
- [x] CHK010 - Are the ordering guarantees for a large offline batch (30+ events) specified as
  applying *within one child's events* only, *within one tablet's queue* only, or globally across
  all queued rows from all tablets — the spec's wording ("in the order they originally occurred")
  doesn't disambiguate cross-tablet interleaving from single-queue ordering? [Ambiguity, Spec
  FR-014] — **Resolved**: spec.md FR-014 now states the guarantee is per-tablet-queue only;
  research.md R6 documents the same scope explicitly.
- [x] CHK011 - Is there a requirement covering what happens if the same event id is submitted
  twice (e.g., a retried request after a timeout whose original request actually succeeded
  server-side) — idempotency of `POST` isn't addressed by any current FR? [Gap] — **Resolved**:
  new spec.md FR-013a requires idempotent-by-id create semantics; reflected in
  contracts/child-events-api.md.
- [x] CHK012 - Are requirements defined for what the caregiver-facing UI shows if a queued event
  ultimately fails validation server-side on sync (not a 409, not a 401, a genuine 400) rather
  than just the generic "pending sync" / "synced" states? [Gap, Non-Functional/UX] —
  **Resolved**: new spec.md FR-014a requires a distinct "needs review" on-device state,
  director-correctable.

## Temperature Alert Threshold

- [x] CHK013 - Is the boundary condition at exactly 38.0°C unambiguous (spec says "above 38.0°C"
  / "> 38.0°C" consistently across spec.md, data-model.md, and contracts — confirm no drift to
  "≥" anywhere)? [Consistency, Spec FR-010, data-model.md] — **Resolved**: confirmed consistent
  ("> 38.0°C" / "above 38.0°C") across all three documents; no change needed.
- [x] CHK014 - Is the notification retry/failure behavior specified when the push dispatch call
  itself fails (network error to Expo's service, not "zero recipients") — distinct from FR-011's
  "no eligible contacts" case? [Gap, Spec FR-011] — **Resolved**: new spec.md FR-011a covers
  transport-level dispatch failure distinctly from FR-011's zero-recipients case.
- [x] CHK015 - Is there a requirement bounding how many times an alert fires if the same child
  has multiple qualifying temperature events close together (e.g., two readings both >38.0°C
  within the same hour) — one alert per event, or de-duplicated? [Gap, Edge Case] —
  **Resolved**: new spec.md FR-011b states one independent attempt per qualifying event, no
  de-duplication.
- [x] CHK016 - Is "authorised to pick up" in FR-010 traceable to a single, unambiguous existing
  field (`ChildContact.CanPickup`), with no alternate interpretation (e.g., primary contact vs.
  any can-pickup contact) left open? [Clarity, Spec FR-010, data-model.md Relationships] —
  **Resolved**: already satisfied; data-model.md's Relationships section ties this to
  `ChildContact.CanPickup` unambiguously.

## Same-Day Edit Authorization

- [x] CHK017 - Is "same calendar day" anchored to a specific timezone reference (location's
  timezone vs. server UTC vs. device-local clock) with enough precision to implement
  consistently, especially near local midnight? [Clarity, Spec Assumptions, research.md R4] —
  **Resolved**: new research.md R8 and spec.md Assumptions fix a single `Europe/Brussels`
  reference for both the edit window and the daily summary.
- [x] CHK018 - Is the caregiver's location scope for FR-006 defined for a caregiver assigned to
  *multiple* locations (feature 005's dual-location caregiver) — same-day edit rights at every
  assigned location, or only the location the event's child currently belongs to? [Gap,
  Ambiguity, Spec FR-006] — **Resolved**: spec.md FR-006 and research.md R4 now specify
  eligibility is against the event's own `LocationId` (new data-model.md field), checked per the
  caregiver's `StaffLocationEligibility`, independent of where they're currently checked in.
- [x] CHK019 - Does a requirement exist for what happens when a caregiver's location assignment
  changes *during* the same day as an event they're now trying to edit (assigned this morning,
  reassigned this afternoon) — is eligibility evaluated at edit-time or unaffected by
  reassignment? [Gap, Edge Case] — **Resolved**: spec.md FR-006/research.md R4 now specify
  eligibility is evaluated live at edit-time, not a record-time snapshot.
- [x] CHK020 - Is the boundary behavior specified for an edit attempted in the last moments of
  "today" that completes after midnight has passed server-side (race between client submission
  time and server evaluation time)? [Gap, Edge Case] — **Resolved**: new spec.md Assumptions
  entry specifies evaluation against the server's clock at request-receipt time.

## Daily Summary Visibility Filtering

- [x] CHK021 - Is "excluding staff-internal events" specified as applying identically to *every*
  aggregate field (counts AND latest-values), or could a reader interpret it as counts-only,
  leaving latest-value fields (e.g., `latestMood`) ambiguous about whether a staff-internal mood
  entry could still surface as the "latest"? [Ambiguity, Spec FR-018, data-model.md Daily
  Summary] — **Resolved**: spec.md FR-018 and data-model.md's Daily Summary section now state
  the exclusion applies uniformly, including to latest-value fields.
- [x] CHK022 - Is the summary's date-boundary (which calendar day an event belongs to for
  aggregation purposes) explicitly tied to the same day-boundary definition used for same-day
  edit authorization (CHK017), or could the two features silently use different day boundaries?
  [Consistency, Gap] — **Resolved**: new spec.md FR-018a ties both to the same `Europe/Brussels`
  reference (research.md R8).
- [x] CHK023 - Is there a requirement covering a soft-deleted event's treatment in the daily
  summary as explicitly as its treatment in the timeline list endpoint — data-model.md states
  the summary filters `DeletedAt IS NULL` but spec.md's FR-018 only mentions `visibleToParent`
  filtering, not deletion, as the exclusion criterion? [Consistency, Spec FR-018 vs. data-model.md]
  — **Resolved**: spec.md FR-018 now explicitly names soft-deleted events as an exclusion
  criterion alongside staff-internal ones.
- [x] CHK024 - Is "medicationAdministered" defined precisely enough to distinguish "any
  medication event recorded" from "any medication event with a confirmed administrator" — the
  spec doesn't state whether an unattributed (skipped-confirmation) medication event still counts
  toward this flag? [Ambiguity, data-model.md Daily Summary] — **Resolved**: spec.md FR-017 and
  data-model.md's Daily Summary section now state this flag is independent of `AdministeredBy`
  attribution.

## Notes

- All 24 items resolved by updating spec.md/data-model.md/research.md/contracts in the
  2026-07-08 checklist-follow-up session — none left as deferred advisory debt.
