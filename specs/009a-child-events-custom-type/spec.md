# Feature Specification: Child Events — Custom Type & Growth Check Rename

**Feature Branch**: `009a-child-events-custom-type`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Add a `custom` child event type — a caregiver-defined label plus free text — for anything the 11 fixed event types don't cover. Bundle a migration-safe rename of `measurement` → `growth_check` since this feature already touches ChildEventType end-to-end."

## Clarifications

### Session 2026-07-09 (pre-specify, with the product owner)

- Q: What should `custom` provide that `note` doesn't? → A: A short caregiver-supplied label (headline) plus optional longer free text, distinct from `note`'s body-only shape. The timeline shows the label as the headline instead of a generic "Note".
- Q: Should the `measurement` → `growth_check` rename be split into its own backlog item or bundled into this feature? → A: Bundled into this feature, done as a migration-safe rename (add-new-alongside-old, backfill existing rows, remove old value) rather than a find-and-replace, since `child_events` may already have live data under the old wire value.
- Q: Is `ChildEventType` genuinely a closed enum end-to-end with no string-typed gap? → A: Confirmed by re-reading the codebase (not assumed) — backend `ChildCare.Domain.Enums.ChildEventType` is a closed C# enum with an explicit `ToWireString`/`TryParseWireString` mapping (`ChildEventTypeExtensions.cs`), and `mobile/types/index.ts` defines `ChildEventType` as a closed TS string-literal union. No open/string-typed path exists on either side; `custom` and the `growth_check` rename can be added as ordinary closed-enum changes.

### Session 2026-07-09 (clarify)

- Q: Should the `custom` event's label field suggest previously-used labels (autocomplete), or stay plain free text with no suggestions? → A: Plain free text, no suggestions — matches every other free-text field in this feature (`note`'s text, `activity`'s description); no new lookup endpoint or combobox component is introduced.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver logs a one-off observation with its own title (Priority: P1)

A caregiver notices something during the day that doesn't fit any of the existing quick-action types (applied sunscreen, a visiting specialist stopped by, a child said their first word) and wants to log it with a short, specific title rather than burying it in an untitled note.

**Why this priority**: This is the entire reason the feature exists — without it, caregivers keep using `note` for everything that doesn't fit, and parents see a wall of identical "Note" entries with no way to tell them apart at a glance.

**Independent Test**: Can be fully tested by logging in as a caregiver, opening a child's quick-action sheet, selecting "Custom", entering a label (and optionally detail text), saving, and confirming the event appears on the timeline with the label as its headline — independent of any other user story.

**Acceptance Scenarios**:

1. **Given** a caregiver opens the quick-action sheet for a child, **When** they select "Custom", enter a label ("Sunscreen applied"), and save with no detail text, **Then** the event is saved and appears on the timeline showing "Sunscreen applied" as its headline.
2. **Given** a caregiver selects "Custom", **When** they enter both a label and longer detail text and save, **Then** the timeline shows the label as the headline with the detail text displayed beneath it.
3. **Given** a caregiver selects "Custom", **When** they attempt to save with no label entered, **Then** the system rejects the save and prompts for a label — detail text alone is never sufficient.
4. **Given** the tablet has no network connectivity, **When** a caregiver records a `custom` event, **Then** it is queued and synced exactly like every other event type (feature 008/009's existing offline behavior), with no special-casing.

---

### User Story 2 - Existing `measurement` events keep working under their new name (Priority: P2)

A director or caregiver who has already recorded growth-check (weight/height/head-circumference) events under the old `measurement` type continues to see that data correctly after this feature ships, now labeled/named `growth_check`.

**Why this priority**: This is a rename of a shipped, already-in-use capability (feature 009), not new functionality — it must not lose or corrupt any existing data, but it delivers no new value on its own, hence lower priority than the new `custom` type.

**Independent Test**: Can be fully tested by recording a `measurement`-type event before this feature's migration runs (or seeding one directly), running the migration, and confirming the event reads back correctly as `growth_check` with the same weight/height/head-circumference values — independent of User Story 1.

**Acceptance Scenarios**:

1. **Given** a `child_events` row exists with `event_type = 'measurement'` before this feature ships, **When** the migration runs, **Then** that row reads back with `event_type = 'growth_check'` and unchanged `weightKg`/`heightCm`/`headCm` payload values.
2. **Given** the migration has completed, **When** a caregiver records a new growth-check event (weight/height/head-circumference), **Then** it is saved with `event_type = 'growth_check'` — `measurement` is no longer an accepted value for new writes.
3. **Given** a caregiver opens the quick-action sheet after this feature ships, **When** they look for the growth-check entry, **Then** it is labeled as a growth check (not "Measurement") and behaves identically to how `measurement` behaved before (same optional weight/height/head fields, same "at least one field required" rule).

---

### Edge Cases

- A caregiver enters a label made entirely of whitespace — treated the same as no label (rejected, same as Acceptance Scenario 3 above).
- A caregiver enters a label at the maximum allowed length — accepted; one character beyond the maximum is rejected with the same validation error as any other malformed payload (consistent with FR-002/FR-002a's existing pattern from feature 009).
- A `custom` event is edited same-day by a caregiver, or at any time by a director — follows the exact same edit/delete rules feature 009 already established for every other event type (FR-006/FR-007), with no special case for `custom`.
- A `custom` event is marked staff-internal (`visible_to_parent = false`) — excluded from the parent daily summary/timeline exactly like any other staff-internal event (FR-018), with its label never surfacing to a parent.
- The migration runs against a tenant schema with zero existing `measurement` rows — completes as a no-op backfill with no errors.
- A client (mobile app build, or an offline-queued request generated before this feature was installed) still submits the literal wire value `measurement` after the cutover — rejected as an invalid/unrecognized event type, the same as any other unrecognized value; no dual-write compatibility window is offered past the one-time server-side backfill.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a caregiver to record a `custom` event for a child, accepting a required `label` and an optional `text` field, and MUST reject a `custom` payload with no label (or a label that is empty/whitespace-only) using the same validation-error pattern as every other event type's required fields (feature 009's `ChildEventPayloadValidator`).
- **FR-002**: The `label` field MUST be bounded to a short length (100 characters) appropriate for a timeline headline; a `custom` payload exceeding this bound MUST be rejected the same way as any other malformed payload.
- **FR-003**: The `text` field, when present, MUST accept free-form text with no special validation beyond feature 009's existing free-text handling (comparable to `note`'s `text` field or `activity`'s `description`).
- **FR-003a**: The `label` field MUST be entered as plain free text with no autocomplete or suggestion of previously-used labels — consistent with every other free-text field in this feature and in feature 009 (no new lookup/history mechanism is introduced for this type).
- **FR-004**: Every timeline and daily-summary view MUST render a `custom` event using its `label` as the headline (in place of a generic type name), with `text` (if present) shown as supporting detail — distinguishing it from `note`, which has no headline.
- **FR-005**: `custom` events MUST participate in every existing cross-cutting rule from feature 009 identically to the 11 existing types: same-day caregiver edit / any-time director edit (FR-006/FR-007), soft-delete (FR-008), `visible_to_parent` staff-internal exclusion (FR-018), offline queueing and sync (feature 008's offline infrastructure), and pagination (FR-019). No new exception paths are introduced for this type.
- **FR-006**: System MUST rename the existing `measurement` event type to `growth_check` end-to-end: the backend enum value, its wire-string mapping, all user-facing labels/i18n keys, and the mobile closed type union — with no remaining reference to `measurement` as a live, writable value after this feature ships.
- **FR-007**: System MUST migrate every existing `child_events` row with `event_type = 'measurement'` to `event_type = 'growth_check'`, preserving all other column and payload data unchanged (weight/height/head-circumference values, timestamps, authorship, visibility, soft-delete state).
- **FR-008**: After the migration completes, the system MUST reject `measurement` as an event type on any new create/update request — this is a one-time cutover (add-new, backfill, remove-old), not an ongoing dual-write compatibility window; no client is expected to keep submitting the old value once this feature ships.
- **FR-009**: The `growth_check` event type MUST retain byte-for-byte the same payload shape, field names, and validation rules `measurement` had (optional `weightKg`/`heightCm`/`headCm`, at least one required, each independently range-checked) — this is a rename only, not a schema or behavior change.
- **FR-010**: All user-facing strings introduced or changed by this feature (the `custom` type's label prompt/placeholder, the `growth_check` display name, any related error messages) MUST use the existing i18n mechanism (NL/FR/EN), with no hardcoded strings.

### Key Entities

- **Child Event (extended)**: Gains a 12th type, `custom` (payload: `{ label, text? }`), alongside the existing 11 (feature 009). The `measurement` type is renamed to `growth_check` with an unchanged payload shape. No change to the entity's other attributes (occurred_at, visible_to_parent, recorded_by, soft-delete, etc. — see feature 009's Key Entities).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can log a `custom` event with a distinct, specific title in the same quick-entry flow (bottom sheet, no more taps than any other free-text type like `note`) already used for the other 11 event types.
- **SC-002**: 100% of pre-existing `measurement` rows are readable as `growth_check` after migration, with zero data loss or value corruption, verified against the original recorded values.
- **SC-003**: A parent viewing their child's timeline can distinguish a `custom` event from a generic note at a glance, via its headline label, without opening the event for detail.
- **SC-004**: Zero caregiver-tablet or parent-facing surface still displays the string "Measurement" after this feature ships.

## Assumptions

- `custom` events are excluded from the temperature push-alert path and from `medicationAdministered`-style daily-summary flags (feature 009, FR-010/FR-017) — those remain specific to `temperature`/`medication` only; a `custom` event never triggers a push notification or a summary flag, regardless of its label/text content.
- No new director-facing web UI is introduced for either change — same as feature 009, all child-event authoring stays caregiver-tablet-only; the web admin has no child-event screen today.
- The rename's backfill runs as a one-time data migration executed as part of this feature's deployment (a SQL script per this repo's EF Core convention of never auto-migrating production — see project CLAUDE.md), not as a background job or lazy on-read conversion.
- "Remove the old value" (FR-008) means the backend no longer accepts or produces `measurement` as a wire value after the migration — it does not require deleting the `Measurement` C# enum member itself if keeping it (unmapped, unreachable) is a lower-risk implementation choice; either is acceptable as long as no client-visible behavior can produce or accept `measurement`.
- Existing feature-009 test fixtures referencing `measurement` are updated to `growth_check` as part of this feature, rather than left passing against a now-invalid value.
- No deprecation window/dual-acceptance period is offered for the old `measurement` wire value — this is accepted as a low-risk cutover because event creation always originates from a caregiver tablet running the current app build (no third-party API consumers exist), and any in-flight offline-queued `measurement` writes are expected to be rare edge cases handled per the Edge Cases section above (rejected, correctable by a director).
