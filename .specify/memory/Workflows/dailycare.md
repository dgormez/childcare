# Daily Care Event Workflow

## Purpose

Capture what happens during a child's day and communicate it appropriately.

**Implemented by feature `009-child-events`** (2026-07-08) — the first feature to build this
workflow end-to-end. Details below reflect the actual shipped behavior, not just the original
design intent.

### Trigger

A caregiver observes a child event, from the caregiver tablet's quick-action sheet.

The 11 supported event types: `sleep`, `temperature`, `medication`, `feeding_bottle`,
`feeding_solid`, `diaper`, `mood`, `activity`, `note`, `weight`, `measurement` — one JSONB-backed
`child_events` table (constitution's Development Workflow section), not a table per type.

### Actors

- Caregiver
- Parent (daily-summary API consumer only — no parent app exists yet to display it)
- Director
- System (temperature-alert dispatch, recorded-by attribution resolution)

### Flow

1. Caregiver taps a child's card → quick-action sheet → selects an event type → minimal-tap
   entry (2 taps for routine types — diaper/mood/feeding_bottle — per FR-021).
2. `recorded_by` is resolved server-side from who's currently checked in via feature 008a's
   room-shift register (`IShiftAttributionService`), not asserted by the client.
3. System validates the payload against that event type's shape (FluentValidation,
   `ChildEventPayloadValidator`) — a payload with fields outside its type's set, a missing
   required field, or an out-of-range numeric value is rejected (`422 errors.validation`).
4. Event is stored and appears immediately on the child's timeline — works fully offline via
   feature 008's offline-queue/sync-engine (`entity_type = 'child_event'`); a sleep event's end
   merges into its still-queued create rather than becoming a second row.
5. A same-day event can be edited/deleted from the same location's paired tablet (any caregiver
   there, since routine tablet actions are device-token authenticated, not per-caregiver — see
   `health-safety.md`'s note on this same distinction for medication attribution). Deletes are
   soft (retained, excluded from reads).
6. A daily-summary query (`GET /api/child-events/daily-summary`) aggregates counts (naps,
   bottles, diaper changes) and latest values (mood, temperature, medication-given) per
   child/date, excluding staff-internal (`visible_to_parent = false`) and deleted events — built
   now as a backend capability for the eventual parent-app feature to consume directly.

### Applications

Caregiver Tablet:

- Quick-action bottom sheet (not a full-screen modal), icon-based (`lucide-react-native`),
  64pt touch targets for the highest-frequency actions.
- Sleep lifecycle: start (1 tap, no fields) → in-progress badge → end with a quality selection.
- Offline-first: every write queues and syncs automatically; a genuine server-side rejection
  (not a transient failure) surfaces as a distinct "needs review" state, not endless retry.

Parent Mobile:

- Not built yet — the daily-summary endpoint exists ahead of any parent-facing UI.

Director Web:

- Not built yet — a director's any-day correction capability exists at the API layer
  (`PATCH`/`DELETE` accept a director JWT via a `DeviceOrDirector` policy) with no screen in
  this app to drive it.

### Design Principles

- Recording should take seconds — 2 taps for routine types after the sheet opens (FR-021).
- Information should feel human — event data, not raw logs.
- Parents see a story, not database records (once a parent app exists to render it).
- Corrections favor the room (any caregiver, same day) over individual ownership — matches
  feature 008a's shared-room reality (2+ caregivers per BKR ratio).
