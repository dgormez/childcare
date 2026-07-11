# Feature Specification: Multi-Child Events

**Feature Branch**: `009c-multi-child-events`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Allow caregivers to select multiple children before logging an event, so one submission creates one child_event record per selected child. Adds a 'Meerdere kinderen' toggle to the caregiver-tablet event-logging flow, a multi-select grid of present children, and a POST /child-events/batch endpoint that creates one child_event row per selected child with partial-success semantics."

## Product Context

### Feature Type

User-facing UI (with a supporting API-backend capability: the batch endpoint).

### Primary Consumer

Caregiver.

### Workflow Boundary

This feature belongs to the **Daily Child Care** workflow (`Workflows/dailycare.md`) — it extends feature 009's single-child event-logging flow to support batch entry for group moments.

Actors: Caregiver (creates events via device-token-authenticated tablet, per 008a kiosk mode).

Actions: Enter multi-select mode on the room roster; select multiple present children; choose an event type and fill the event form once; submit; review per-child results if any failed.

Data Flow: Caregiver tablet (room roster screen) → `POST /child-events/batch` → one `child_event` row per selected child (same table/schema as feature 009, no new entity) → batch result returned to the client.

**Correction found during specification** (same category as the premise corrections in features 009/012/012a — verified against the actual codebase, not assumed from the backlog prompt): feature 009's caregiver UI has no "child picker" step to attach a toggle "before" — the existing flow always starts from a specific child (tap a child card on the room roster → child detail screen → `QuickActionSheet` → event type → form). There is no screen where a caregiver picks an event type first and a child second. Multi-select is therefore introduced as a mode on the **room roster screen** itself (the existing children list, `mobile/app/(app)/index.tsx`), not as a toggle inserted into the single-child flow. See UX Requirements' Main flow below for the corrected sequence. This does not change the single-child flow, which remains exactly as shipped in 009.

Outputs: Per-child `child_event` records; a batch result summary (toast on full success, an inline per-child failure list on partial success).

Cross-platform Impact: Caregiver tablet only (`mobile/`) for the UI; backend-only for the new batch endpoint. No parent app or director web changes.

### User Impact

This enables a Caregiver to log the same event for multiple children in one submission, resulting in dramatically less repetitive tapping during group care moments (naps, diaper rounds, feeding rounds).

### UX Requirements

Persona: Caregiver — standing, one-handed, tablet mounted or laid flat, landscape locked (per `platform-rules.md`'s Caregiver Tablet section).

Platform: Caregiver tablet (`mobile/`, kiosk mode per 008a).

User job: "Log this same event for several children at once instead of repeating the single-child flow N times."

Success criteria: Fewer taps than N individual submissions; no risk of logging for the wrong child; a partial failure is recoverable without redoing the whole batch.

Main flow: On the room roster screen (the existing children list), the caregiver enters multi-select mode via an explicit toggle (long-press on a card is already used for marking absence, so multi-select needs its own affordance, not a gesture reuse) → present children's existing cards (photo + name, 48pt+ touch targets) become selectable, each with a checkbox-style selected state → optional "Alles selecteren" shortcut selects all present children → once ≥1 child is selected, a "Log event" action appears → tapping it opens the same event-type/quick-entry sheet the single-child flow uses (`QuickActionSheet`), restricted to the batch-eligible event types → the caregiver fills the form once → submit → summary toast, then multi-select mode exits back to the normal roster view.

Loading state: Submit button shows a spinner and is disabled while the batch call is in flight.

Empty state: If only one child is present in the room, multi-select mode still works — the grid simply shows that one selectable child, never a dead end.

Error state: On partial failure, list which children failed and why (plain-language reason, e.g. "already checked out"), with the option to retry just the failed children rather than redoing the whole batch.

Accessibility: 48pt minimum touch targets on child selection cards; selected/failed state is never conveyed by color alone — paired with a check icon (selected) or alert icon (failed), per `design-system.md`.

Offline behavior: The batch is queued as a single `offline_queue` entry and replayed as one batch call on sync — never exploded into N individual calls.

### Technical Requirements

API impact: New `POST /child-events/batch` endpoint, device-token authenticated, reusing feature 009's per-child `child_event` creation logic rather than duplicating it.

Data-model impact: None — reuses the existing `child_event` table/schema as-is; no new tables.

Security considerations: Every `child_event` row created by the batch is scoped to the submitting device token's `LocationId`/`GroupId`, exactly like the existing single-child endpoint (these values come from the device token's own claims, not from the request body, and are never per-child in either flow) — the multi-select grid is itself built only from children in the device's own room, so an out-of-scope `child_id` is not reachable through the UI. The 30-child batch cap is enforced server-side, not just client-side. Unlike the single-child endpoint (which today performs no presence/attendance check before writing an event — it only checks the child record exists), the batch endpoint adds a per-child presence check against the child's attendance record (feature 010) at submission time: a batch's selection window is materially longer than the single-child flow's, so the "checked out mid-flow" race is realistic here in a way it isn't for a single, fast per-child tap. This is a new, batch-specific validation, not a gap being closed in the existing single-child endpoint (out of scope for this feature).

Performance considerations: Batch insert should avoid N sequential round-trips where reasonably avoidable, but per-child transactional isolation and partial-success correctness take priority over raw throughput at this scale (a room tops out around 30 children).

Testing requirements: Happy path (all children succeed); partial failure (one child checked out mid-flow); scope violation (child outside the device token's group); max-batch-size enforcement; offline-queue single-entry replay.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Log a group event for all present children (Priority: P1)

A caregiver is putting an entire room down for a 13:00 nap. Instead of tapping into each of 8 children one at a time and repeating the same "sleep start" entry, the caregiver toggles "Meerdere kinderen," taps "Alles selecteren," fills in the sleep-start form once, and submits — creating 8 individual sleep events in one action.

**Why this priority**: This is the core value of the feature — without it, there is no reason to build a batch flow at all.

**Independent Test**: Can be fully tested by toggling multi-select, selecting all present children in a room, submitting a `sleep` event, and verifying one `child_event` row exists per selected child with matching `occurred_at` and payload.

**Acceptance Scenarios**:

1. **Given** a caregiver is on the event-type entry screen for a room with 8 present children, **When** they toggle "Meerdere kinderen" and tap "Alles selecteren," **Then** all 8 children appear selected in the grid.
2. **Given** 8 children are selected and a `diaper` event form is filled in, **When** the caregiver submits, **Then** the system creates 8 `child_event` rows (one per child) sharing the same event type, timestamp, and payload, and the caregiver sees a success summary ("Opgeslagen voor 8 kinderen").

---

### User Story 2 - Recover from a partial failure without redoing the batch (Priority: P1)

While a caregiver is filling in a multi-select nap event for 6 children, another caregiver checks one of those children out at the front desk. When the batch submits, that one child's event fails (they're no longer present) while the other 5 succeed. The caregiver needs to see exactly which child failed and why, without having to redo the successful 5.

**Why this priority**: Partial success is the mechanism that makes batch logging safe to use in a live, concurrently-changing room — without it, a single edge case would force caregivers back to one-at-a-time entry out of distrust in the feature.

**Independent Test**: Can be fully tested by submitting a batch where one `child_id` is checked out (or made ineligible) between selection and submission, and verifying the response contains 5 successes and 1 named failure with a reason, with the 5 successful `child_event` rows persisted regardless.

**Acceptance Scenarios**:

1. **Given** a batch of 6 selected children where 1 is checked out before submission completes, **When** the caregiver submits, **Then** the system creates 5 `child_event` rows, returns the 1 failed `child_id` with a reason, and does not roll back the 5 successes.
2. **Given** a partial-failure result is shown, **When** the caregiver taps retry, **Then** only the failed child(ren) are resubmitted — the caregiver is not asked to redo the whole batch.

---

### User Story 3 - Use the multi-select flow while offline (Priority: P2)

The tablet loses connectivity mid-shift. The caregiver still needs to log a group diaper round. They complete the multi-select flow as normal; the batch is queued locally as a single offline entry and replayed as one batch call once connectivity returns.

**Why this priority**: Offline resilience is a standing requirement across the caregiver app (008), but the batch flow specifically must not degrade into N separate queue entries, which would multiply sync traffic and defeat the point of batching.

**Independent Test**: Can be fully tested by disabling network, submitting a multi-select batch, verifying exactly one `offline_queue` entry was created for the batch (not N), then re-enabling network and verifying the queued entry replays as a single batch call producing the expected per-child `child_event` rows.

**Acceptance Scenarios**:

1. **Given** the tablet is offline, **When** a caregiver submits a multi-select batch of 5 children, **Then** exactly one `offline_queue` entry is created representing the whole batch.
2. **Given** that queued batch entry, **When** connectivity is restored and the queue syncs, **Then** one `POST /child-events/batch` call is replayed and produces 5 `child_event` rows (or the appropriate partial-success result if room state changed while offline).

---

### Edge Cases

- What happens when a caregiver selects a child who is then checked out by another caregiver before the batch is submitted? The server checks the child's attendance record (feature 010) at submission time and returns a per-child error for that child; the rest of the batch still succeeds.
- What happens when a `child_id` in the batch does not belong to the submitting device token's own room? This is not reachable through the UI, since the selectable grid is built only from the device's own room roster (same source as the existing single-child flow); a direct API call with a foreign `child_id` fails that child as not found in this room, without affecting the rest of the batch.
- What happens when a caregiver selects more than 30 children? The client caps selection at 30 and disables further selection with a brief explanation; the server independently rejects any batch exceeding 30 `child_ids`.
- What happens when a caregiver tries to use multi-select for an individual-only event type (temperature, medication, weight/growth_check)? Multi-select mode does not offer those event types — the caregiver only ever sees the existing single-child flow for them.
- What happens when the room has only one child present? Multi-select mode still works — the grid shows that single selectable child, with no dead end.
- What happens when every child in a batch fails (e.g. the whole room already checked out)? The caregiver sees a full-failure result with per-child reasons and a retry option; no misleading "partial success" toast is shown.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The caregiver tablet's room roster screen MUST offer a multi-select mode that lets the caregiver select multiple present children before choosing an event type, restricted to event types that support multi-child logging (`sleep`, `diaper`, `feeding_bottle`, `feeding_solid`, `mood`, `activity`, `note`, `custom`).
- **FR-002**: Event types that require per-child values (`temperature`, `medication`, `weight`/`growth_check`) MUST NOT be offered once a multi-child selection is active — those event types always use the existing single-child flow.
- **FR-003**: While in multi-select mode, the system MUST show the room's present children as selectable cards (photo + name, reusing the roster's existing cards), with a shortcut to select all present children at once.
- **FR-004**: The system MUST allow the caregiver to fill in the event form (fields, timestamp) exactly once for the whole batch — the form is identical to the single-child flow.
- **FR-005**: On submission, the client MUST send one request containing the list of selected `child_ids` plus the single event payload to a batch endpoint.
- **FR-006**: The system MUST create one independent `child_event` record per selected child, sharing the same event type, timestamp, and payload.
- **FR-007**: A failure creating the event for one child MUST NOT prevent or roll back event creation for any other child in the same batch (per-child transactional isolation).
- **FR-008**: The system MUST reject any batch submission (client-side and server-side) containing more than 30 `child_ids`.
- **FR-009**: The system MUST validate, per child, that the child has an active attendance record (present, not yet checked out) at submission time; a child failing this check is reported as a per-child failure, not a whole-batch failure. Every event in the batch is written under the submitting device token's own location/group, exactly as the single-child flow already does — this is not a per-child value.
- **FR-010**: The system MUST return, for each submission, the set of child_ids that succeeded and the set that failed with a human-readable reason per failure.
- **FR-011**: On full success, the caregiver MUST see a confirmation summary (e.g. "Opgeslagen voor 8 kinderen").
- **FR-012**: On partial or full failure, the caregiver MUST see which children failed and why, and MUST be able to retry only the failed children without resubmitting the ones that already succeeded.
- **FR-013**: The single-child event-logging flow MUST remain unchanged and available regardless of this feature — multi-select is opt-in per logging session, never the default.
- **FR-014**: While offline, a multi-select batch submission MUST be queued as exactly one offline-queue entry representing the whole batch, and MUST be replayed as exactly one batch call on sync (never split into per-child queue entries).
- **FR-015**: All caregiver-facing strings introduced by this feature MUST be available via the app's existing i18n keys (NL/FR/EN).

### Key Entities

- **Child Event** (existing, feature 009): No schema changes. Each row created by a batch submission is a standalone record identical in shape to one created by the single-child flow — the batch endpoint is a creation-time convenience, not a new relationship or grouping entity. Rows created together from the same batch share event type, `occurred_at`, and payload but are not linked to each other in the data model.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can log the same event for 8 children in one submission, in fewer taps than the 8 individual submissions the single-child flow would require.
- **SC-002**: When one child in a batch fails (e.g. already checked out), the other children's events are still recorded — 0% data loss for the succeeding children.
- **SC-003**: A caregiver can identify exactly which children failed in a partial-failure result and retry only those, without needing to redo the successful portion of the batch.
- **SC-004**: A batch submitted while offline produces exactly one offline-queue entry and, once synced, the same per-child result as if it had been submitted online.
- **SC-005**: No batch of more than 30 children is ever accepted, whether the oversized selection originates from the client or a direct API call.

## Assumptions

- "Children currently in the room" for the multi-select grid means children with an active attendance/check-in record (feature 010) in the caregiver's device-token room/group (008a) — the same population already shown on the room roster screen (`mobile/app/(app)/index.tsx`), filtered to present children and made multi-selectable, rather than a new picker screen.
- The batch endpoint reuses feature 009's existing per-child validation and creation logic (event-type field rules, `EndedAt`/`AdministeredBy` type restrictions, etc.) rather than re-implementing it — this feature only adds the fan-out over multiple children and the partial-success response shape.
- "Reasonably efficient" batch insert (Technical Requirements) means avoiding unnecessary N+1 query patterns where trivial to avoid; it does not require a bespoke bulk-SQL path, since 30 rows is a small enough batch that per-child correctness (isolation, per-child validation) is the higher priority.
- Retry-failed-only (FR-012) is a client-side convenience: the retry simply resubmits a new batch containing only the previously-failed `child_ids` — no server-side "resume batch" state is needed.
