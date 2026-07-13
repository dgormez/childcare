# Feature Specification: Meal List (Maaltijdenlijst)

**Feature Branch**: `013d-meal-list`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Generate a daily meal list for the kitchen — who eats what today,
with allergen flags and per-child meal texture visible at a glance. Build a child_meal_preferences
table, a GET /locations/{id}/meal-list?date= endpoint aggregating today's present children with
their meal preferences and allergen flags, a director-web 'Maaltijdenlijst' print page, and a
caregiver-tablet read-only view scoped to the caregiver's own group."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web and caregiver
tablet).

### Primary Consumer

Director (primary — manages meal preferences, views/prints the full-location list). Caregiver
(secondary — read-only, own-group view).

### Workflow Boundary

This feature belongs to the **Daily Child Care** workflow (`Workflows/dailycare.md` — Meals). It
is a read-model aggregation over three existing workflows' data (Attendance & Presence, Health &
Safety, Child Lifecycle's group assignment) rather than new business meaning, so no new workflow
entry is added.

Actors: Director (creates/edits a child's meal preferences; views and prints the full-location
meal list); Caregiver (views own-group meal list, read-only); System (aggregates attendance,
group assignment, allergy/medication data, and meal preferences into one per-location,
per-date view).

Actions: Set or edit a child's meal preference; fetch the day's meal list for a location
(director) or a group (caregiver); print the meal list.

Data Flow: `child_meal_preferences` (new table) + `AttendanceRecord` (010, today's presence) +
`Contract`/`ContractedDay` (007, today's expected-but-not-yet-present children) +
`ChildGroupAssignment` (006, group membership) + `Child.AllergySeverity` (006) +
`HealthRecord` with `RecordType = MedicationStanding` (013c) are aggregated server-side into one
read model per location per date.

Outputs: `GET /locations/{id}/meal-list?date=` response, consumed by a director-web print-ready
page and a caregiver-tablet read-only screen.

Cross-platform Impact: Director web (new screen + print stylesheet) and caregiver tablet (new
screen off the group home screen). No parent-app impact — explicitly out of scope, this is an
internal operational document.

### User Impact

This enables kitchen staff and caregivers to see, at a glance, exactly what each present child
should eat today — texture, dietary restrictions, and allergen severity — reducing the risk of a
caregiver serving the wrong meal to a child with a severe allergy.

### UX Requirements

Persona: Director (web, high-density, print-oriented) and Caregiver (tablet, glanceable,
read-only, one tap from the group home screen).

Platform: Director web (desktop-first, ≥1280px, per `platform-rules.md`) and caregiver tablet
(landscape, 48pt touch targets).

User job: "What does each present child eat today, and who has a severe allergy I must not
miss?"

Success criteria: A caregiver or director can identify a child's texture, dietary type, and
allergy severity within a half-second glance. Allergy severity is never conveyed by color alone
(icon + color, per `design-system.md`'s WCAG 1.4.1 rule).

Main flow: Open the meal list (web: sidebar nav entry with a Print button; tablet: from the group
home screen) → see today's present children for the location/group, grouped by group/section →
optionally toggle "Inclusief verwacht" to also show expected-but-not-yet-checked-in children in a
separate "Verwacht" section.

Loading/empty/error states: Loading skeleton consistent with other list screens. Empty state (no
children present yet) uses an icon + one short sentence per `design-system.md`. Error state
surfaces a clean i18n'd message, never a raw stack trace.

Accessibility: Allergen severity always paired with an icon, never color alone. The print
stylesheet must stay legible in black & white (icon shape, not just color, distinguishes
severe/moderate-or-mild/none).

Offline behavior: The caregiver-tablet screen reads from the existing offline `read_cache`
(feature 008 infrastructure) when offline, consistent with other caregiver-tablet read screens
(e.g. the group view). No offline write path — editing preferences is director-web only, always
online.

### Technical Requirements

API impact: New `GET /locations/{id}/meal-list?date=` endpoint (Director and device-token/
Caregiver readable, scoped as described in Security below). New `PUT
/children/{id}/meal-preferences` endpoint (`DirectorOnly`) to create/update a child's meal
preference record.

Data-model impact: New `child_meal_preferences` table (tenant schema), one row per child
(`UNIQUE ChildId`), FK to `children(id)`. `texture`, `dietary_type`, `portion_size`,
`additional_notes` as described in the feature prompt. Requires a migration — generate the SQL
script and run it manually; no auto-migrate in production (per this repo's conventions).

Security considerations: Write restricted to `DirectorOnly`. Read available to Director (any
location in their tenant) and Staff/Caregiver via device token, scoped to that device's own
`LocationId`/`GroupId` (same scoping pattern as existing caregiver-tablet endpoints, e.g.
`RoomShiftEndpoints`). This is a legitimate PHI-adjacent view (allergens, dietary/religious data)
— never exposed to the parent app, per explicit out-of-scope.

Performance considerations: The aggregation query spans attendance + contracts + group
assignment + child allergy/medication data + the new preferences table for a single location/
date — implemented as a single efficient read query, not N+1 per child, given classes of
10–40 children.

Testing requirements: Backend tests for the happy path (present children shown with correct
texture/allergen/dietary/medication indicators) plus key negative flows (absent child never
shown; a child with no preferences row shows "Geen voorkeur" rather than being hidden; a Severe
allergy is never rendered by color alone in the response's paired icon/severity fields). Web
component tests for the meal-list screen and print stylesheet. Mobile test for the caregiver
read path, including its offline-cache-fallback branch.

## Clarifications

### Session 2026-07-13

- Q: The BACKLOG prompt says allergen flags come "from 013c health_records," but `HealthRecord`
  (013c) has no severity field — severity (`Mild`/`Moderate`/`Severe`) actually lives on
  `Child.AllergySeverity` (feature 006), and `HealthRecord.RecordType = Allergy` is a separate,
  severity-less structured detail row. Which is the source of truth for the meal list's RED/
  AMBER/GREY flag? → A: `Child.AllergySeverity` maps directly to RED (Severe) / AMBER (Mild or
  Moderate) / GREY (none set). `HealthRecord` allergy detail rows are not surfaced on the meal
  list — this feature only needs the severity flag, not the full allergy narrative, and
  `Child.AllergySeverity` is the field every other feature (006, 013c's dashboards) already
  treats as authoritative for severity.
- Q: No "expected but not yet checked in" aggregation exists anywhere in the codebase today —
  should this feature build it, or should the "Inclusief verwacht" toggle be deferred? → A:
  Build it. "Expected" = a child with an active `Contract` at this location whose
  `ContractedDays` include today's weekday, cross-referenced against today's `AttendanceRecord`:
  present if checked in (`Status = Present`, `CheckInAt` set); expected if contracted for today
  but no attendance record yet; excluded entirely if `Status = Absent` or `Status = Closure`.
- Q: `Child.DietaryRestrictions` (free-text, feature 006) already exists — does the new
  structured `child_meal_preferences.dietary_type` array replace it? → A: No, they coexist.
  `Child.DietaryRestrictions` remains the child profile's general free-text medical/dietary
  note; `child_meal_preferences.dietary_type` is a new, meal-list-specific structured tag set
  (halal/kosher/vegetarian/vegan/gluten_free) purpose-built for at-a-glance kitchen filtering,
  with `additional_notes` on the same table covering anything the structured tags don't.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver views today's meal list for their group (Priority: P1)

A caregiver on the room tablet opens the meal list from the group home screen and sees, for each
child currently present in their group, the texture, dietary tags, allergy severity, and a pill
icon if the child has standing medication.

**Why this priority**: This is the core daily-operational value — a caregiver preparing or
serving a meal needs this information in the moment, and it's the single highest-frequency use
of this feature (every meal, every day, every room).

**Independent Test**: Can be fully tested by checking in several children with different meal
preferences and allergy severities to a group, opening the caregiver-tablet meal list, and
confirming each present child's texture/dietary/allergy/medication indicators render correctly
and no absent child appears.

**Acceptance Scenarios**:

1. **Given** three children checked in to a group, one with `Severe` allergy severity and a
   `pureed` texture preference, **When** the caregiver opens the group's meal list, **Then** that
   child's row shows a RED allergy indicator (icon + color) and the `pureed` texture, and the
   other two children show their own correct texture/severity.
2. **Given** a child checked in with no `child_meal_preferences` row, **When** the caregiver views
   the meal list, **Then** that child still appears with a "Geen voorkeur" indicator rather than
   being hidden.
3. **Given** a child not checked in today (no attendance record, or `Status = Absent`), **When**
   the caregiver views the meal list, **Then** that child never appears in the default view.

---

### User Story 2 - Director views and prints the full-location meal list (Priority: P1)

A director opens the "Maaltijdenlijst" page in the web admin, sees every present child across all
groups at a location grouped by group/section, and prints it for the kitchen.

**Why this priority**: Equal in operational necessity to User Story 1 — Belgian KDV kitchens
prepare meals centrally for the whole location, not per room, so a director/kitchen-facing
full-location view with a physical printout is the other half of this feature's daily use.

**Independent Test**: Can be fully tested by opening the Maaltijdenlijst page for a location with
children present across two different groups, confirming both groups' present children appear
grouped correctly, and confirming the Print button produces a legible black-and-white-compatible
layout (icons, not just color, for severity).

**Acceptance Scenarios**:

1. **Given** children present across two groups at a location, **When** the director opens the
   Maaltijdenlijst page, **Then** children are shown grouped by group/section, each with texture,
   dietary tags, allergy severity, and medication indicator.
2. **Given** the director clicks Print, **When** the page renders for print, **Then** RED/AMBER/
   GREY allergy severity remains distinguishable using icon shape alone (color removed, as in
   grayscale printing).

---

### User Story 3 - Director edits a child's meal preferences (Priority: P2)

A director opens a child's profile and sets or updates that child's texture, dietary tags,
portion size, and additional notes.

**Why this priority**: Necessary for the data behind User Stories 1 and 2 to exist and stay
current, but lower priority than the read/print flows since a child with no preference set still
renders safely ("Geen voorkeur") — the feature is usable, if less complete, without this story
being built first.

**Independent Test**: Can be fully tested by setting a child's texture to `mixed` and dietary
tags to `["halal"]` from the child profile, then confirming the meal list reflects the change
immediately.

**Acceptance Scenarios**:

1. **Given** a child with no meal preferences, **When** the director sets texture, dietary tags,
   portion size, and a note, **Then** the meal list shows all four values for that child on next
   load.
2. **Given** a child with existing meal preferences, **When** the director updates the texture,
   **Then** the meal list reflects the new value and the record's `updated_at`/`updated_by`
   fields update accordingly.

---

### User Story 4 - "Inclusief verwacht" toggle shows expected children (Priority: P3)

A caregiver or director toggles "Inclusief verwacht" to also see children who are contracted to
attend today but have not yet been checked in, in a separate "Verwacht" section.

**Why this priority**: A genuine convenience (kitchen can start prepping ahead of full
check-in) but not required for the feature's core safety/operational purpose — the default view
(present children only) already satisfies the primary user job.

**Independent Test**: Can be fully tested by having a child with an active contract for today's
weekday but no attendance record yet, confirming they are absent from the default view, and
confirming they appear in a separate "Verwacht" section only when the toggle is enabled.

**Acceptance Scenarios**:

1. **Given** a child contracted for today but not yet checked in, **When** the toggle is off,
   **Then** the child does not appear anywhere in the meal list.
2. **Given** the same child, **When** the toggle is switched on, **Then** the child appears in a
   separate "Verwacht" section, visually distinct from the present-children groups.

---

### Edge Cases

- A child is checked in as an "extra day" with no active contract covering today (feature 010's
  existing precedent) — they still appear in the present-children view since presence is driven
  by the actual `AttendanceRecord`, not by contract state.
- A child's `AttendanceRecord.Status` transitions to `Closure` (bulk closure-day marking, feature
  011) — excluded from both the present and "Verwacht" views, same as `Absent`.
- A child has standing medication (`HealthRecord.RecordType = MedicationStanding`) whose
  `ValidFrom`/`ValidUntil` window does not cover today — the pill icon is not shown; only
  medication currently valid for today's date is surfaced.
- Two directors edit the same child's meal preferences concurrently — last write wins (same
  optimistic-write precedent as the rest of this codebase's single-record update endpoints; no
  new concurrency mechanism introduced).
- A location has zero children present and the toggle is off — the empty state (icon + one short
  sentence) is shown rather than an empty table.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `child_meal_preferences` record per child with `texture`
  (`pureed`/`mixed`/`pieces`/`normal`, default `normal`), `dietary_type` (a set of zero or more
  of `halal`/`kosher`/`vegetarian`/`vegan`/`gluten_free`), `portion_size`
  (`small`/`normal`/`large`, default `normal`), and free-text `additional_notes`.
- **FR-002**: Directors MUST be able to create or update a child's meal preferences from the
  child profile.
- **FR-003**: The system MUST expose a per-location, per-date meal list that includes every
  child present at that location on that date — defined precisely as: an attendance record
  exists for that child/location/date with `Status = Present` AND `CheckOutAt` is still null (a
  child who has already checked out is no longer physically present, even though their
  attendance `Status` value itself never changes away from `Present` on check-out) — grouped by
  the child's current group/section.
- **FR-004**: The meal list MUST NEVER include a child whose attendance status for that date is
  `Absent` or `Closure`, in the default view.
- **FR-005**: A child with no `child_meal_preferences` row MUST still appear on the meal list,
  showing "Geen voorkeur" rather than being hidden or omitted.
- **FR-006**: Each child's row MUST show that child's allergy severity, derived from
  `Child.AllergySeverity` (`Severe` → RED, `Mild`/`Moderate` → AMBER, none set → GREY).
- **FR-007**: Allergy severity MUST be conveyed by an icon paired with color, never by color
  alone, in both the on-screen view and the printed output.
- **FR-008**: Each child's row MUST show a distinct indicator (pill icon) when that child has at
  least one currently-valid standing medication record (`HealthRecord.RecordType =
  MedicationStanding` with today's date falling within an inclusive `ValidFrom`/`ValidUntil`
  window — a boundary date equal to either bound counts as valid — treating a null bound as
  open-ended on that side). The indicator is a single boolean presence flag: a child with
  multiple currently-valid standing medication records still shows exactly one pill icon, never
  a count.
- **FR-009**: The system MUST support an "Inclusief verwacht" toggle that, when enabled, adds a
  separate "Verwacht" section listing children with an active contract covering today's weekday
  who have not yet been checked in today.
- **FR-010**: The director-web meal list MUST support printing via a CSS print stylesheet (no
  PDF generation).
- **FR-011**: The caregiver-tablet meal list MUST be scoped to the caregiver's own group (derived
  from the device's paired `GroupId`), while the director-web meal list MUST show every group at
  the selected location.
- **FR-012**: The meal list and meal-preference read/write endpoints MUST NEVER be reachable by
  the parent app or exposed to parent-facing surfaces.
- **FR-013**: All user-facing strings on both the web and caregiver-tablet meal list screens MUST
  use i18n keys (NL/FR/EN) — no hardcoded labels.
- **FR-014**: The caregiver-tablet meal list MUST be readable from the existing offline
  `read_cache` when the device is offline, consistent with other caregiver-tablet read screens.
- **FR-015**: The per-child data fields shown (texture, dietary tags, portion size, allergy
  severity, standing-medication indicator, "Geen voorkeur" fallback) MUST be identical between
  the director-web and caregiver-tablet views of the same child — platform layout/density differs
  per `platform-rules.md`, but no field is shown on one platform and withheld on the other.

### Key Entities

- **MealPreference** (`child_meal_preferences`): One per child. Holds texture, dietary tags,
  portion size, and free-text notes used to render the meal list. Independent of, and
  complementary to, the child profile's general free-text `DietaryRestrictions` field.
- **MealListEntry** (read model, not a persisted table): A per-child, per-date, per-location
  aggregation of attendance status, group membership, meal preference, allergy severity, and
  standing-medication presence — computed on read, not stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver or director can identify any present child's texture, dietary tags, and
  allergy severity within a half-second glance at the meal list screen.
- **SC-002**: 100% of present children appear on the meal list every time it is opened — zero
  children are ever silently omitted, including those with no preferences set.
- **SC-003**: Allergy severity is distinguishable in printed black-and-white output without color,
  verified by icon shape alone remaining legible.
- **SC-004**: A director can update a child's meal preference and see it reflected on the next
  meal-list load, with no manual cache-clear or page-specific workaround needed.
- **SC-005**: The meal list for a 40-child location loads as a single request with no visible
  per-child loading delay (no N+1 pattern perceptible to the user).

## Assumptions

- `Child.AllergySeverity` (feature 006) is the authoritative source for the RED/AMBER/GREY
  allergen flag — `HealthRecord.RecordType = Allergy` (013c) detail rows are not additionally
  surfaced on this screen (see Clarifications).
- "Expected" children are computed from `Contract`/`ContractedDay` (007) cross-referenced with
  today's `AttendanceRecord` (010) — no new table is needed to track this; it is derived entirely
  from existing data (see Clarifications).
- `Child.DietaryRestrictions` (free-text, 006) and the new structured `dietary_type` field remain
  two distinct fields serving different purposes; this feature does not migrate or deprecate the
  former (see Clarifications).
- Caregiver-tablet access is scoped by the device's paired `GroupId`, the same mechanism already
  used by other caregiver-tablet endpoints (e.g. `RoomShiftEndpoints`, feature 008a) — no new
  scoping mechanism is introduced.
- Printing uses a CSS print stylesheet only, per the BACKLOG prompt's explicit "no PDF needed" —
  this diverges from feature 007's `IContractPdfGenerator` (QuestPDF) precedent by design, since
  this is an ephemeral daily operational document, not a retained legal record.
- Directors editing meal preferences always have network access (consistent with every other
  director-web write in this codebase to date) — no offline write path for this feature.
- The meal-list aggregation is implemented as a single database query per request (data-model.md)
  — there is no multi-step/multi-service load sequence, so a "partial load" state (some fields
  present, others missing due to a failed sub-fetch) does not apply; the request either returns a
  complete result or fails as a whole, handled by this codebase's standard error-response
  convention.
- The caregiver-tablet offline cache may show slightly stale data relative to a simultaneously
  open director-web view (e.g. a preference edited moments ago has not yet reached a tablet that
  has been offline) — no strict cross-platform staleness bound is required, consistent with every
  other existing caregiver-tablet cache-fallback screen (e.g. 013c's health summary), none of
  which impose one either.
