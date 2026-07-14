# Feature Specification: Monthly Menu

**Feature Branch**: `013e-monthly-menu`

**Created**: 2026-07-14

**Status**: Draft

**Input**: User description: "Let the director publish a monthly meal menu visible to parents in
the parent app. Parents can see what their child will eat and request changes to their child's
meal preferences. Each child's preferences are personalised (texture, dietary type, allergies
from 013c)."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web and parent
mobile).

### Primary Consumer

Director (primary for menu authoring — creates/edits/publishes/unpublishes the monthly menu,
reviews preference-change requests). Parent (primary for menu viewing and preference-change
requests). Caregiver is not a consumer of this feature.

### Workflow Boundary

This feature spans two existing workflows; no new workflow is added.

- **Daily Child Care** (`Workflows/dailycare.md` — Meals): the monthly-menu authoring/publishing
  capability extends this workflow's existing meal-tracking ground (013d's `child_meal_preferences`
  and meal-list).
- **Parent Communication** (`Workflows/communication.md`): the parent-facing Menu tab and the
  preference-change decision notification are a form of parent-facing update, the same category
  as 013a's day-reservation decision notifications.

Actors: Director (creates/edits/publishes/unpublishes a location's monthly menu; reviews and
approves/rejects preference-change requests). Parent (views the published monthly menu for their
child's location(s); submits a meal-preference change request).

Actions: create/edit monthly menu day entries; publish/unpublish a monthly menu; view the current
month's published menu; view a child's current meal preference; submit a preference-change
request; approve or reject a pending preference-change request.

Data Flow: Director writes `monthly_menus`/`monthly_menu_days` (tenant schema) in web admin →
published menus become readable by the parent app for that location. Parent submits a
`meal_preference_change_requests` record → director reviews it in web admin, alongside the
child's active health records (013c) for context → approving updates the child's existing
`child_meal_preferences` record (013d, reusing its existing upsert write path) and marks the
request decided → an in-app notification (and push, if a token is registered) is sent back to the
requesting parent either way.

Outputs: published monthly menu view (parent app), preference-change request queue (director
web), decision notification (parent app, in-app + push).

Cross-platform Impact: parent mobile (new Menu tab, preference-change request form) and director
web (new Menu management section under a location, new preference-request review queue) are
affected. Caregiver tablet and backend-only surfaces are not directly affected beyond the new
schema/API additions.

### User Impact

This enables a director to publish a personalised monthly meal menu and parents to view it and
request meal-preference changes, resulting in greater parent trust and transparency around what
their child eats.

### UX Requirements

Persona: Parent (mobile, an emotional user seeking reassurance per `platform-rules.md` — the Menu
tab reads warmly, not like a data table; texture/dietary indicators use plain language, not
database phrasing) and Director (web, high-density, per-location authoring and a review queue).

Platform: Parent mobile (Expo/React Native) for the Menu tab and preference-change request;
director web (Next.js) for menu authoring and preference-request review.

User job (parent): "What is my child eating this month, and does it match their needs?"

User job (director): "Publish this month's menu quickly, and clear the preference-change queue."

Success criteria: A parent sees the current month's menu and their child's active preference
within one tap of opening the app. A director can create and publish a full month's menu in one
sitting without leaving the Menu section.

Main flow (parent): open app → Menu tab → see current month grid (day: soup/main/dessert) with
closure days greyed out → see own child's preference indicator → optionally tap "Voorkeur
aanpassen" → submit request → receive an in-app + push notification on the director's decision.

Main flow (director): web admin → Location → Menu → select month → fill in days → Save draft →
Publish (or correct via Un-publish → edit → re-publish) → separately review pending
preference-change requests → approve (updates `child_meal_preferences`) or reject (optional
reason, triggers parent notification).

Loading/empty/error states: unpublished-month placeholder ("Menu nog niet beschikbaar"); a day
with no entries renders as "—", not a blank confusing gap; a failed preference-change submission
shows a clear inline error, not a silent failure.

Accessibility: standard WCAG AA contrast per `design-system.md`; preference-request status badges
pair color with an icon, never color alone; a closure day is distinguished by a label/icon in
addition to reduced opacity, not by greying alone.

Offline behavior: the parent-app Menu tab is read-mostly and falls back to the last cached
published menu when offline, consistent with 013c's caregiver-summary cache-fallback precedent.
Submitting a preference-change request requires connectivity — unlike check-in/out, it needs
immediate server-side validation against the child's current preference state, so it is
deliberately not queued through the offline-write infrastructure.

### Technical Requirements

API impact: director endpoints for monthly-menu CRUD/publish/unpublish and preference-request
list/approve/reject; parent endpoints for reading the published monthly menu (per child's
active-contract location(s)) and submitting a preference-change request. A new parent-facing
closure-day read is also required — no such endpoint exists today (the only closure-calendar read
endpoint, `GET /api/closures`, is `DirectorOnly` and also returns director-facing notification-
delivery status alongside each closure date); this feature adds the first parent-scoped read of
closure days, reusing the existing closure-calendar data rather than duplicating it, but exposing
only the date itself (plus a display label) — the parent-facing read MUST NOT expose delivery
status or any other director-only field carried by the existing closure read.

Data-model impact: new tenant-schema tables for the monthly menu (one row per location/year/month,
plus one child row per calendar day with soup/main/dessert/notes), and for preference-change
requests (one row per request, referencing the child and decision metadata) — schemas finalized in
`plan.md`/`data-model.md`. Approving a request writes through the existing `MealPreference`
(013d) entity/upsert path rather than introducing a second write mechanism for the same data. A
new EF Core migration is required — per this repo's convention, EF never auto-migrates in
production; a SQL script is generated and run manually.

Security considerations: parent reads/writes are scoped to children linked to the requesting
parent's account via the existing `ChildContacts` join and `ICurrentParentContactResolver`
pattern (the same authorization primitive used by feature 013a's day-reservation requests) — no
new authorization mechanism is introduced. Director actions are scoped to their own tenant and
(for authoring) the selected location, per existing conventions.

Performance considerations: a month's menu is a small, bounded dataset (max 31 day-rows) — no
pagination needed.

Testing requirements: happy path plus key negative flows per this repo's convention (xUnit/Moq
backend) — an unpublished menu is never visible to parents, a preference request is rejected for
a child the requester is not linked to, a second pending request for the same child is rejected,
and an approved request correctly upserts `child_meal_preferences`.

## Clarifications

### Session 2026-07-14

- Q: The prompt says a rejected preference-change request notifies the parent "via push with
  optional reason" — should this reuse an existing notification mechanism or introduce a new one?
  → A: Reuse feature 013a's exact pattern (`IExpoPushSender` + an in-app `Notification` row, a
  distinct i18n `BodyKey` when a reason is present vs. absent rather than interpolating a possibly
  -null value) — precedent already exists in `DayReservationNotificationService`, and a
  `NotificationType` enum value is added alongside `DayReservationDecided` for this feature.
- Q: A parent's child can hold active contracts at more than one location (an existing, already-
  handled case in feature 013a) — should the Menu tab show one location's menu or every location
  the child is actively contracted at? → A: Every location, each clearly labeled — mirrors 013a's
  existing resolution of "this child's location(s)" from every active contract rather than
  assuming exactly one, avoiding a silently wrong or missing menu for a multi-location child.
- Q: Can a parent submit a new preference-change request while a prior request for the same child
  is still pending? → A: No — the system rejects a second pending request for the same child with
  a clear message, keeping the director's review queue free of duplicate/conflicting decisions for
  the same child. The parent can submit again once the pending request is decided.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director creates and publishes a monthly menu (Priority: P1)

A director opens the Menu section for a location, fills in soup/main course/dessert for the
month's days, saves as a draft, and publishes it so parents can see it.

**Why this priority**: This is the feature's entire supply side — nothing else in this feature
has value until a menu exists and is published.

**Independent Test**: Can be fully tested by creating a monthly menu with entries for several
days, saving as draft (confirming it is not yet parent-visible), then publishing (confirming it
becomes visible in the parent-read path).

**Acceptance Scenarios**:

1. **Given** no monthly menu exists yet for a location/year/month, **When** the director fills in
   several days and saves, **Then** a draft menu is created (`published_at` unset) and is not
   visible to parents.
2. **Given** a saved draft menu, **When** the director clicks Publish, **Then** the menu becomes
   visible to parents immediately and `published_at` is set.
3. **Given** a director attempts to create a second menu for a location/year/month that already
   has one, **When** they save, **Then** the existing menu is updated rather than a duplicate
   being created.

---

### User Story 2 - Parent views the current month's published menu (Priority: P1)

A parent opens the Menu tab and sees the current month's published menu for their child's
location, with closure days greyed out and their child's meal preference shown alongside.

**Why this priority**: This is the feature's core parent-facing value and the reason the feature
exists — equal in necessity to User Story 1, since a menu with no viewer has no value either.

**Independent Test**: Can be fully tested by publishing a menu for the current month, opening the
parent app's Menu tab, and confirming the correct days/courses render, closure days are visibly
distinguished, and the child's own texture/dietary preference is shown.

**Acceptance Scenarios**:

1. **Given** a published menu for the current month, **When** the parent opens the Menu tab,
   **Then** each day shows soup/main course/dessert, days with no entries show "—", and closure
   days are visually de-emphasized and labeled distinctly (not by color alone).
2. **Given** no menu has been published for the current month, **When** the parent opens the Menu
   tab, **Then** a "Menu nog niet beschikbaar" placeholder is shown instead of an empty grid.
3. **Given** the parent's child has active contracts at two different locations, **When** the
   parent opens the Menu tab, **Then** both locations' menus are shown, each clearly labeled by
   location name.

---

### User Story 3 - Parent requests a meal-preference change (Priority: P2)

A parent taps "Voorkeur aanpassen" next to their child's current preference, selects a new
texture and/or dietary tags, adds an optional note, and submits a change request.

**Why this priority**: A meaningful but secondary capability — the menu itself (User Story 2)
delivers value without this, but this is what makes the preference indicator actionable rather
than purely informational.

**Independent Test**: Can be fully tested by submitting a preference-change request from the
parent app and confirming it appears in the director's review queue with status Pending.

**Acceptance Scenarios**:

1. **Given** a parent viewing their own child's preference indicator, **When** they submit a new
   texture, dietary tags, and a note, **Then** a `Pending` preference-change request is created,
   visible to the director, and `child_meal_preferences` is unchanged until a decision is made.
2. **Given** a child already has a pending preference-change request, **When** the parent attempts
   to submit another one for the same child, **Then** the submission is rejected with a clear
   message rather than creating a second pending request.
3. **Given** a parent who is not linked to a given child, **When** they attempt to submit a
   preference-change request for that child, **Then** the request is rejected.

---

### User Story 4 - Director reviews and decides a preference-change request (Priority: P2)

A director opens the preference-change request queue, sees a pending request alongside the
child's active health records for context, and approves or rejects it.

**Why this priority**: Closes the loop opened by User Story 3 — without this, submitted requests
would accumulate with no resolution path, but the feature's read-only value (User Stories 1-2)
does not depend on it.

**Independent Test**: Can be fully tested by submitting a request, opening the director's review
queue, approving it, and confirming `child_meal_preferences` reflects the change and the parent
receives a decision notification; separately, rejecting a request with a reason and confirming
the parent's notification includes that reason.

**Acceptance Scenarios**:

1. **Given** a pending preference-change request, **When** the director views it in the queue,
   **Then** the child's currently active health records (013c) are shown alongside it for
   context.
2. **Given** a pending request, **When** the director approves it, **Then**
   `child_meal_preferences` is created or updated with the requested texture/dietary tags, the
   request is marked `Approved` with `decided_by`/`decided_at` set, and the parent receives a
   decision notification.
3. **Given** a pending request, **When** the director rejects it with a reason, **Then** the
   request is marked `Rejected`, `child_meal_preferences` is left unchanged, and the parent's
   notification includes the stated reason.
4. **Given** a pending request, **When** the director rejects it without a reason, **Then** the
   parent's notification renders a clean generic decision message — never a blank or literal-null
   reason.

---

### User Story 5 - Director corrects a published menu mid-month (Priority: P3)

A director notices a typo in an already-published menu, un-publishes it, corrects the entry, and
re-publishes.

**Why this priority**: A necessary operational safety valve, but strictly lower-value than
authoring and publishing themselves — a menu that can be published but never corrected is still a
usable, if imperfect, MVP.

**Independent Test**: Can be fully tested by publishing a menu, un-publishing it (confirming it
becomes parent-invisible again), correcting a day's entry, and re-publishing (confirming parents
see the corrected value).

**Acceptance Scenarios**:

1. **Given** a published menu, **When** the director un-publishes it, **Then** it is no longer
   visible to parents and `published_at` is cleared.
2. **Given** an un-published (draft) menu with a corrected entry, **When** the director
   re-publishes it, **Then** parents see the corrected value on next app open.

---

### Edge Cases

- A day within the displayed month has no menu entries at all (KDV closed, or parents bring their
  own lunch) — all fields render as "—", not a missing/blank row.
- A closure day (feature 011) falls within the displayed month — shown, greyed out and labeled,
  never hidden.
- A parent's preference-change request conflicts with an existing structured health record (e.g.
  requesting `normal` texture for a child with a feeding-related note) — the system does not block
  the request; the director sees the child's active health records alongside the request and
  decides.
- A director publishes a menu with a typo mid-month — corrected via un-publish → edit → republish
  (User Story 5); parents see the correction on next app open, not instantly pushed.
- A child has active contracts at more than one location — each location's menu is shown,
  distinctly labeled (see Clarifications).
- Two directors edit the same monthly menu concurrently — last write wins, consistent with this
  codebase's existing single-record update convention (no new concurrency mechanism introduced).
- A parent submits a preference-change request for a child they are not linked to — rejected, same
  authorization pattern as feature 013a's day-reservation requests.
- A child is deactivated while a preference-change request for them is still `Pending` — the
  request remains visible in the review queue, but approving it MUST fail cleanly with a clear
  error (the existing `child_meal_preferences` write-through already refuses a deactivated child,
  per feature 013d) rather than silently succeeding or corrupting the child's preference record;
  the director can still reject the request to clear it from the queue.
- Two directors attempt to decide (approve or reject) the same pending request at the same
  moment — the second decision attempt MUST fail cleanly (the request is no longer `Pending` by
  the time it runs) rather than silently overwriting the first decision or applying two
  conflicting outcomes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to create or edit a monthly menu for a location/year/month,
  with per-day soup, main course, dessert, and free-text notes fields, saved as an editable draft.
- **FR-002**: A monthly menu in draft state (not yet published) MUST NOT be visible to parents.
- **FR-003**: Directors MUST be able to publish a draft menu, making it immediately visible to
  parents linked to that location.
- **FR-004**: Directors MUST be able to un-publish a published menu to make corrections, after
  which it reverts to a director-only draft state until re-published.
- **FR-005**: At most one monthly menu MAY exist per location per year/month; editing an existing
  month updates that menu rather than creating a duplicate.
- **FR-006**: The parent app MUST show, for the current month, every location the parent's child
  holds an active contract at, each with its own published menu (or the placeholder from FR-008),
  clearly labeled by location name when more than one applies. The system MUST NOT show a menu,
  or any closure-day data (FR-009), for a location none of the requesting parent's linked
  children currently holds an active contract at — this location set MUST be derived server-side
  from the requesting parent's own active contracts, never accepted as a client-supplied
  `locationId` parameter.
- **FR-007**: A day with no menu entries MUST render as "—" per field, not as a blank or missing
  row.
- **FR-008**: If no menu has been published for a location's current month, the parent app MUST
  show a "Menu nog niet beschikbaar" placeholder instead of an empty grid.
- **FR-009**: Closure days (feature 011) falling within the displayed month MUST be shown but
  visually de-emphasized (greyed out) and labeled distinctly — never conveyed by color/opacity
  alone.
- **FR-010**: The parent app MUST show, alongside the menu, the parent's child's current meal
  preference (texture, dietary tags) sourced from `child_meal_preferences` (013d), in plain
  language rather than raw enum values.
- **FR-011**: A parent MUST be able to submit a meal-preference change request (new texture, new
  dietary tags, optional free-text note) for a child they are linked to via `ChildContacts`, using
  the same authorization pattern as feature 013a's day-reservation requests. At least one of the
  new texture or new dietary tags MUST be present — a request that changes neither is meaningless
  and MUST be rejected.
- **FR-012**: The system MUST reject a new preference-change request for a child that already has
  a pending (undecided) request, with a clear error rather than creating a second pending
  request. Concurrent submissions for the same child racing this check is an accepted, low-impact
  edge case (at most one extra pending row briefly exists per child) rather than a scenario the
  system must prevent with a stronger concurrency mechanism — a director deciding either resulting
  request still leaves the child in a correct final state.
- **FR-013**: Director web MUST expose a queue of pending preference-change requests, each
  showing the requesting parent, the requested changes, the free-text note, and the child's
  currently active health records (013c) for context.
- **FR-014**: Approving a preference-change request MUST create or update the child's
  `child_meal_preferences` record with only the fields the request actually specified (reusing
  the existing 013d partial-upsert write path) — a request that specifies only a new texture
  MUST leave the child's existing dietary tags, portion size, and notes unchanged, and vice
  versa; approving MUST NEVER clear a field the request did not ask to change. The system MUST
  mark the request `Approved` with `decided_by`/`decided_at` set.
- **FR-015**: Rejecting a preference-change request MUST leave `child_meal_preferences` unchanged,
  record an optional reason, and mark the request `Rejected` with `decided_by`/`decided_at` set.
- **FR-016**: On approval or rejection, the requesting parent MUST receive an in-app notification
  and, if a push token is registered, a push notification — reusing the existing
  `IExpoPushSender`/`Notification` pattern. A rejection with a stated reason MUST render distinctly
  from a rejection without one; a null/absent reason MUST NEVER be interpolated into client-facing
  text.
- **FR-017**: Director-web menu authoring and preference-request review MUST be scoped to the
  director's own tenant and, for authoring, the selected location. Parent reads and writes MUST be
  scoped to children linked to the requesting parent's account.
- **FR-018**: All user-facing strings on both the parent-app Menu tab and the director-web Menu/
  preference-request screens MUST use i18n keys (NL/FR/EN) — no hardcoded labels.
- **FR-019**: Neither the monthly menu nor the preference-change request endpoints MUST be
  reachable by the caregiver tablet — this feature is director-web and parent-app only.

### Key Entities

- **MonthlyMenu**: One per location per year/month. Tracks draft vs. published state
  (`published_at`) and who created it.
- **MonthlyMenuDay**: One per calendar date within a `MonthlyMenu`, holding soup/main course/
  dessert/notes. A date with no entries is not a data-integrity error — it renders as "—".
- **MealPreferenceChangeRequest**: One per parent-submitted request. References the target child,
  the requested texture/dietary tags, an optional note, and decision metadata
  (`status`/`decided_by`/`decided_at`). Approving one writes through to the existing
  `MealPreference` (013d) entity rather than duplicating its data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create and publish a full month's menu (up to 31 days) in one sitting
  without leaving the Menu section.
- **SC-002**: A parent can see the current month's menu and their child's active meal preference
  within one tap of opening the parent app.
- **SC-003**: 100% of unpublished months show the "Menu nog niet beschikbaar" placeholder rather
  than an empty or broken view.
- **SC-004**: Every decided preference-change request (approved or rejected) reaches the
  requesting parent with a clear notification — zero silent decisions.
- **SC-005**: A director reviewing a pending preference-change request can see the child's
  relevant active health records without leaving the review screen.

## Assumptions

- A parent's child may hold active contracts at more than one location; the Menu tab shows every
  such location's menu, mirroring how feature 013a already resolves "this child's location(s)"
  from every active contract rather than assuming exactly one (see Clarifications).
- Reviewing a preference-change request against a health record is advisory only — the director
  sees the child's active health records alongside the request and decides; no automated
  texture-vs-health-record blocking rule is introduced, matching the BACKLOG prompt's own framing
  ("director sees the health record alongside the request when deciding").
- Only one pending preference-change request may exist per child at a time, to keep the director's
  review queue free of duplicate/conflicting in-flight decisions for the same child (see
  Clarifications).
- No parent-facing closure-day read endpoint exists today (`GET /api/closures` is `DirectorOnly`);
  this feature adds the first one, reusing existing closure-calendar data rather than duplicating
  it — the same "additive gap found while wiring UI" pattern noted in prior features' shipped
  notes (007a, 009, 013d).
- Decision notifications reuse feature 013a's exact `IExpoPushSender` + in-app `Notification`
  pattern, adding one new `NotificationType` value alongside `DayReservationDecided` — no new
  notification mechanism is introduced (see Clarifications).
- Approving a request writes through the existing `MealPreference` (013d) entity/upsert path; this
  feature does not introduce a second persistence mechanism for the same preference data.
- Parents always have network access when submitting a preference-change request (consistent with
  every other parent-app write in this codebase to date) — no offline queue for this write.
