# Feature Specification: Child Event Timeline

**Feature Branch**: `009-child-events`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Build the child event timeline — the daily log that caregivers record and parents see in real time. child_events table (sleep, temperature, medication, feeding_bottle, feeding_solid, diaper, mood, activity, note, weight, measurement event types), full CRUD with same-day caregiver edit / any-time director edit, soft-delete, visible_to_parent flag, photo attachment, caregiver tablet quick-entry UI on top of feature 008/008a's scaffold and offline queue, temperature push-notification threshold, query-time parent daily summary aggregation."

## Clarifications

### Session 2026-07-08

- Q: Temperature-alert recipients (FR-010/011) depend on a contact having a "registered device" — but no parent-facing app or token-registration path exists yet. Should this feature build an interim push-token registration mechanism for contacts, or ship the alert logic as-is with zero deliverable recipients until the parent app ships? → A: Ship logic only, no recipients yet — no interim registration path is built by this feature.
- Q: Should FR-006's same-day edit/delete permission extend to any caregiver assigned to the location, or only the original recorder? → A: Any caregiver at the location, same day — matches the shared-room shift model from feature 008a.
- Q: FR-009 (photo attachment) had no dedicated user story. Should it ship as part of this feature or be deferred? → A: Deferred to a follow-up feature — removed from this feature's committed scope.
- Q: Should the system enforce (warn/block) against logging a new medication dose before `next_dose_not_before`? → A: Informational only — the field is recorded and displayed, with no enforcement logic.

### Session 2026-07-08 (checklist follow-up)

- Q: Is an entirely empty `measurement` payload (all three optional fields absent) valid? → A: No — at least one of `weightKg`/`heightCm`/`headCm` is required; "any subset is valid" means any non-empty subset.
- Q: Are numeric readings (`celsius`, `kg`, `weightKg`, `heightCm`, `headCm`) bounded to a plausible physiological range? → A: Yes — each has a defined min/max (see FR-002a); out-of-range values are rejected the same way a malformed payload is.
- Q: Does the temperature threshold apply per-event with no de-duplication? → A: Yes — every qualifying temperature event triggers its own notification attempt; no cooldown/de-duplication window.
- Q: What timezone/day boundary applies to "same calendar day" (edit window) and "one calendar day" (daily summary)? → A: A single fixed `Europe/Brussels` reference, since the product serves only Belgian KDVs today (no location-level timezone field exists in the data model) — both features use this same boundary.
- Q: For a caregiver assigned to multiple locations, does same-day edit eligibility depend on the event's own location or the caregiver's current location? → A: The event's own recorded location. **Superseded during implementation** — device-token-authenticated tablet actions have no individual caregiver identity to check eligibility against (feature 008a's security model); the actual, implementable rule is that the requesting *device* must be paired to the event's own location, not a per-staff-member eligibility lookup. See FR-006 and research.md R4 for the corrected design.
- Q: Does the daily summary's staff-internal exclusion apply to latest-value fields (mood, temperature, medication) as well as counts? → A: Yes, uniformly — a staff-internal event must never surface anywhere in the summary, including as a "latest" value.
- Q: Does FR-018's exclusion also cover soft-deleted events, not just staff-internal ones? → A: Yes — both are exclusion criteria for the daily summary and any parent-facing view.
- Q: Does `medicationAdministered` require a confirmed `administeredBy` attribution, or just that a medication event was recorded? → A: Just that a visible, non-deleted medication event was recorded that day — attribution is a separate, optional detail, not a precondition for this flag.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver records a routine event in under 5 seconds (Priority: P1)

A caregiver standing in the room, often with a child in one arm, taps a child's card from the group view, picks an event type (diaper, bottle, mood, nap, activity, note) from a quick-action sheet, makes a minimal number of taps/selections (no free typing required for the common cases), and the event is saved and visible on that child's timeline immediately — whether or not the tablet currently has network connectivity.

**Why this priority**: This is the core operational loop the whole feature exists for. Every other user story (temperature alerts, parent summaries, director edits) depends on events actually getting recorded reliably during a busy day.

**Independent Test**: Can be fully tested by logging in as a caregiver, opening the group view, recording a diaper change and a bottle-feeding for a child, and confirming both appear on that child's event timeline with correct timestamps — with no dependency on any other user story.

**Acceptance Scenarios**:

1. **Given** a caregiver is viewing their assigned group, **When** they tap a child's card and select "diaper" from the quick-action sheet and choose a type (wet/dirty/both), **Then** the event is saved with `occurred_at` = now and appears at the top of that child's timeline within the same screen, with no full-screen modal or page navigation required.
2. **Given** a caregiver records a bottle-feeding event, **When** they enter the amount in ml and confirm, **Then** the event is saved and shows on the timeline with the amount.
3. **Given** the tablet has no network connectivity, **When** a caregiver records any routine event, **Then** the event appears immediately in the local timeline marked as "pending sync" and is queued for delivery once connectivity returns.
4. **Given** a caregiver starts a sleep event with no end time, **When** they view the child's timeline, **Then** the event is shown as "in progress" until it is ended.
5. **Given** a caregiver ends an in-progress sleep event, **When** they confirm the end, **Then** the timeline shows the completed nap with a duration.

---

### User Story 2 - Caregiver records a temperature reading and the system alerts guardians when it's high (Priority: P1)

A caregiver takes a child's temperature and enters the reading. If the reading exceeds a fever threshold, every contact authorised to pick up that child who has a registered device receives a push notification, without the caregiver needing to do anything beyond entering the reading.

**Why this priority**: Health and safety events are the highest-stakes data this feature records — a missed fever alert is a real safety gap, and Belgian KDVs are legally accountable for accurate health tracking (weight events specifically are a legal requirement).

**Independent Test**: Can be fully tested by recording a temperature reading above the threshold for a child with a can_pickup contact who has a registered push token, and confirming a push notification is sent — independent of any other event type.

**Acceptance Scenarios**:

1. **Given** a caregiver records a temperature of 38.5°C for a child, **When** the event is saved (online), **Then** the system sends a push notification to every contact of that child with `can_pickup = true` and a registered push token.
2. **Given** a caregiver records a temperature of 37.0°C, **When** the event is saved, **Then** no push notification is sent.
3. **Given** none of a child's authorised contacts has a registered push token, **When** a high temperature event is recorded, **Then** the event is still saved successfully and the system logs the notification attempt without crashing or blocking the save.
4. **Given** a caregiver records a high temperature while offline, **When** the tablet reconnects and the event syncs, **Then** the push notification fires at that point, not before, and not from the client.
5. **Given** a caregiver logs a medication administration, **When** they confirm which caregiver administered it (or skip), **Then** the event's administrator attribution is recorded consistently with the room's shift-confirmation pattern already used for check-in/out.

---

### User Story 3 - Director or caregiver corrects a mistaken or wrong-child event (Priority: P2)

A caregiver realizes they logged an event against the wrong child or with wrong details. They can edit or delete it themselves the same day it was recorded. A director can edit or delete any event regardless of age, to correct mistakes discovered later.

**Why this priority**: Mistakes happen constantly in a busy room (wrong child tapped, wrong amount entered) — without a correction path, caregivers either can't fix it or a director has to intervene for even trivial slip-ups, which slows daily operations.

**Independent Test**: Can be fully tested by recording an event as a caregiver, editing it same-day, then confirming a caregiver cannot edit a prior day's event while a director can — independent of other stories.

**Acceptance Scenarios**:

1. **Given** a caregiver recorded an event earlier today, **When** they edit or delete it, **Then** the change succeeds and the timeline reflects it.
2. **Given** a caregiver attempts to edit or delete an event from a previous day, **When** they submit the change, **Then** the system rejects it.
3. **Given** a director attempts to edit or delete any event regardless of its date, **When** they submit the change, **Then** the change succeeds.
4. **Given** an event is deleted, **When** any user later looks for it, **Then** it no longer appears on the timeline, but the record is retained (soft-deleted) rather than physically removed.

---

### User Story 4 - Parent-facing daily summary is available for consumption (Priority: P3)

For a given child and date, the system can produce a summary of the day: how many naps, bottles, and diaper changes occurred, the latest mood assessment, the latest temperature reading, and whether medication was administered — excluding any event marked staff-internal.

**Why this priority**: The parent mobile app itself has not been built yet (no feature has scaffolded it), so there is no UI consumer for this today. This capability is still valuable to build now as an API so the eventual parent-app feature does not have to invent event aggregation from scratch, but it is lower priority than the caregiver-facing recording flows that this feature's actual users depend on immediately.

**Independent Test**: Can be fully tested by recording a mix of events (some visible to parents, some staff-only) for a child on a given day, then requesting that day's summary and confirming counts/last-values are correct and staff-only events are excluded — independent of a parent-facing UI existing.

**Acceptance Scenarios**:

1. **Given** a child has 2 diaper changes, 1 bottle, and 1 completed nap recorded today, **When** the daily summary is requested for that child and date, **Then** it reports those counts accurately.
2. **Given** one of today's events is marked staff-internal (`visible_to_parent = false`), **When** the daily summary is requested, **Then** that event's data is excluded from the summary and from any timeline view intended for parent consumption.
3. **Given** a child has no events recorded yet today, **When** the daily summary is requested, **Then** it returns an empty/zeroed summary rather than an error.

---

### Edge Cases

- A sleep event is recorded offline with no end time (nap in progress). The caregiver ends it later, still offline. Both the create and the end-update must land in the eventual sync as a single completed event, not two conflicting records.
- A caregiver goes offline for an extended period with many events queued. On reconnect, all queued events must sync in the order they were originally recorded, and all must land correctly (no drops, no duplicates).
- Two tablets are both recording events for children in the same room while offline, then both reconnect — their events must not conflict since events are independent records, except the specific case of two tablets both ending the *same* sleep event, where the system must deterministically pick one outcome.
- A caregiver records an event against the wrong child entirely (not just wrong details) — same-day edit/delete must allow full correction, not just field-level updates.
- A temperature or medication event is recorded while offline — the confirmation-of-administrator step is skipped when connectivity isn't available at entry time, and a director can fill it in later rather than blocking the caregiver from saving the reading.
- A child has zero authorised contacts with `can_pickup = true`, or none have a push token — a high-temperature reading must still succeed as a data-entry action.
- A very large volume of historical events exists for a long-enrolled child — browsing or requesting their timeline must not require loading the full history at once.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a caregiver to record an event for a specific child, selecting one of the defined event types (sleep, temperature, medication, feeding_bottle, feeding_solid, diaper, mood, activity, note, weight, measurement).
- **FR-002**: Each event type MUST accept only the specific data fields defined for that type (e.g., a diaper event accepts type wet/dirty/both plus optional notes; a temperature event accepts a Celsius reading) — the system must reject a payload with fields that don't belong to the selected type or missing required fields for that type. A `measurement` payload with none of its three optional fields present is treated as missing its required content and rejected the same way.
- **FR-002a**: Numeric readings MUST be rejected if outside a plausible physiological range for an infant/toddler: `celsius` 30.0–42.0, `kg` (weight, both `weight` and `measurement.weightKg`) 0–30, `heightCm` 30–120, `headCm` 25–60. Out-of-range values are rejected with the same validation error as a malformed payload (FR-002), not silently accepted or clamped.
- **FR-003**: System MUST record, for every event, when it occurred, who recorded it, and when it was created/last updated.
- **FR-004**: A sleep event MUST support being recorded as "in progress" (no end time yet) and later completed with an end time; the recorded duration must reflect the actual elapsed time once completed.
- **FR-005**: System MUST allow marking any event as staff-internal (not visible to parents) at the time it is recorded.
- **FR-006**: System MUST allow a same-day event to be edited or deleted from any tablet paired to the same location the event belongs to (checked against the requesting device's own location, since routine tablet actions are device-authenticated, not individually authenticated per caregiver — feature 008a's security model) — regardless of which caregiver physically recorded it or performs the correction. A caregiver's own multi-location assignment is not separately checked on this path, since no individual caregiver identity is available in a device-token-authenticated request; the location scoping already comes from which room's tablet is used.
- **FR-007**: System MUST allow a director to edit or delete any event regardless of when it was recorded.
- **FR-008**: Deleting an event MUST NOT permanently remove its data — it must be retained but excluded from all subsequent timeline/summary views (soft-delete).
- **FR-010**: When a temperature event is recorded with a reading above 38.0°C, the system MUST notify every contact of that child who is authorised for pickup and has a registered device, without requiring any caregiver action beyond recording the reading.
- **FR-011**: If a child has no eligible contacts or none have a registered device, the system MUST still save the temperature event successfully and MUST record that the notification could not be delivered, without failing the save.
- **FR-011a**: If dispatching the notification itself fails (e.g., a transport-level error reaching the push service, as distinct from FR-011's zero-eligible-recipients case), the system MUST still treat the temperature event as saved successfully and MUST log the dispatch failure, without failing the save or retrying indefinitely.
- **FR-011b**: Every temperature event that individually qualifies (FR-010) MUST trigger its own notification attempt independently — there is no cooldown or de-duplication across multiple qualifying readings for the same child.
- **FR-012**: When an event is recorded while the tablet has no connectivity, the system MUST make it visible immediately in the local timeline, clearly indicated as not yet confirmed by the server, and MUST deliver it once connectivity returns.
- **FR-013**: When a sleep event's end is recorded before its original create has reached the server, the system MUST result in one completed sleep event once both reach the server — never two separate/conflicting records. If the create has, in the interim, already reached the server before the merge is applied locally, the system MUST fall back to sending the end as a normal separate update against the now-existing record rather than losing the update.
- **FR-013a**: A create request MUST be idempotent by its client-generated event id: if a create is submitted again for an id that already exists (e.g., a retried request after a timeout whose original request had actually succeeded), the system MUST return the existing record rather than creating a duplicate or returning an error.
- **FR-014**: When multiple events recorded offline on the same tablet are later delivered together, the system MUST process that tablet's queue in the order the events originally occurred, and all must be retained. This ordering guarantee applies per-tablet only — events from different tablets are never treated as conflicting with each other and may be delivered in any relative order to one another.
- **FR-014a**: If an event fails validation when it is finally delivered from the offline queue (a genuine rejection, not a transient network error), the system MUST surface it to the caregiver as a distinct "needs review" state on-device (not silently dropped, and not shown as an ordinary transient pending-sync state), correctable later by a director.
- **FR-015**: In the specific case where two different tablets both record the end of the *same* sleep event while both were offline, the system MUST deterministically resolve to one outcome: the end time from whichever update the server receives and processes last (by server arrival order), not the earlier-arriving one — rather than creating duplicate or contradictory records.
- **FR-016**: For a medication or temperature event, the system MUST support recording which caregiver administered/took the reading as a distinct, separately-confirmed attribution from who simply logged the entry — and MUST allow this attribution to be filled in later by a director if it was skipped (e.g., recorded while offline).
- **FR-017**: System MUST be able to produce a daily summary for a given child and date, reporting counts of key event types (naps, bottles, diaper changes) and the latest value of others (mood, temperature, whether medication was given). `medicationAdministered` reflects only whether a qualifying medication event was recorded that day — it does not require that event to have a confirmed administrator attribution.
- **FR-018**: The daily summary and any parent-facing timeline view MUST exclude events that are marked staff-internal or soft-deleted — this exclusion applies uniformly to every field, including latest-value fields (e.g., a staff-internal or deleted event must never surface as the "latest" mood or temperature), not just to counts.
- **FR-018a**: The calendar-day boundary used by the daily summary MUST be the same fixed boundary used for the same-day edit window (FR-006) — both use a single `Europe/Brussels` reference day, since the product currently serves only Belgian KDVs and no per-location timezone exists in the data model.
- **FR-019**: System MUST support retrieving a child's event history with pagination rather than requiring the full history to be loaded at once.
- **FR-020**: All caregiver-tablet-facing and any other user-facing text for this feature MUST be presented via the existing i18n mechanism (NL/FR/EN), with no hardcoded user-facing strings.
- **FR-021**: The caregiver-tablet quick-entry flow for routine event types (diaper, bottle, mood) MUST require no more than 2 taps/selections after the quick-action sheet is opened, with no free-text typing for the common case; `activity`/`note` (free-text by nature) are exempt from the tap-count limit but still require no more taps than opening the sheet, selecting the type, and entering text.

### Key Entities

- **Child Event**: A single recorded occurrence for one child — has a type (one of the defined set), when it occurred, an optional end time (sleep only), type-specific data, whether it's visible to parents, who recorded it, and whether it has been withdrawn (soft-deleted). Append-only in spirit: corrections happen through edit/delete rather than ever recomputing history.
- **Daily Summary**: A computed (not stored) aggregation of a child's events for one calendar day — counts and latest values per relevant event type, excluding staff-internal events.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can record a routine event (diaper, bottle, mood, activity, note) in 5 seconds or less from tapping a child's card to the event appearing on the timeline.
- **SC-002**: 100% of events recorded while offline are successfully delivered and correctly reflected once connectivity returns, with zero data loss across a session of 30+ queued events.
- **SC-003**: 100% of temperature readings above the fever threshold result in a notification attempt to every eligible contact with a registered device, with zero missed alerts in testing.
- **SC-004**: A director can locate and correct any historical event (regardless of age) without needing developer or database intervention.
- **SC-005**: A daily summary for any child/date reflects accurate counts and latest values, verified against the underlying recorded events, and never exposes a staff-internal event.
- **SC-006**: Browsing a child's event history remains responsive (no full-history load) even for a child with months of continuous daily records.

## Assumptions

- No `daily_logs` or equivalent existing table was found in the current codebase, despite the originating backlog prompt describing this feature as "replacing" one — this spec treats Child Event as a new capability with nothing to migrate.
- The parent mobile app does not exist yet as a product (no feature has scaffolded it) — User Story 4's daily summary is built as a backend capability now so the eventual parent-app feature can consume it directly, but no parent-facing UI is delivered by this feature.
- "Registered device" for push notifications means a contact has previously registered a push token through some client (caregiver app registers its own token at login for its own purposes; a parent-facing token registration path does not exist yet since there is no parent app — in practice this means temperature alerts may currently have zero deliverable recipients until a parent-facing client exists, which is an accepted limitation of building this ahead of the parent app).
- "Location" scoping for who may edit same-day events (FR-006) follows the existing tenant/location assignment model already established by prior features (staff are scoped to the locations they're assigned to).
- The fever threshold (38.0°C, exclusive) is fixed as specified in the originating request; no configuration UI for adjusting it is in scope for this feature.
- Same-day edit window and the daily summary's calendar-day boundary (FR-006, FR-018a) both use a single fixed `Europe/Brussels` reference day rather than a per-location timezone, since `Location` has no timezone field today and the product serves only Belgian KDVs — consistent with how "today" is already understood elsewhere in the caregiver app (e.g., kiosk shift check-in/out).
- The same-day edit window is evaluated using the server's clock at the moment it receives the edit/delete request, not the client's submission timestamp — an edit submitted in the last moments of "today" that the server doesn't process until after midnight is evaluated against the day it was received, avoiding any ambiguity from client/server clock skew.
- Validation error responses for a rejected payload (FR-002/FR-002a) follow the same FluentValidation pipeline behavior already used by every other command in this codebase (constitution Principle III) — a standard per-field validation problem response, not a bespoke shape invented for this feature.
- A hard validation failure discovered only when an offline-queued event finally reaches the server (FR-014a) is expected to be rare in practice (the same validation rules already ran client-side before the event was queued) but is still handled explicitly rather than assumed impossible.
- Photo attachment (event photos / child+date photos) is explicitly out of scope for this feature per the 2026-07-08 clarification session — it is deferred to a future feature rather than built here without a dedicated user story.
- `next_dose_not_before` on a medication event is stored and displayed for informational purposes only; this feature does not add any warning or blocking logic when a new dose is logged before that time.
- FR-016's "a director can fill this in later" (for a skipped `AdministeredBy`) is reachable only via the raw `PATCH /api/child-events/{id}` API in this feature — no web-admin UI is built for it, since plan.md scopes all web-admin/parent-facing UI out of this feature. This mirrors the accepted push-token-registration gap above: the capability exists at the API layer now so a future web-admin feature only needs to add a screen, not new backend logic.
