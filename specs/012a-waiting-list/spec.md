# Feature Specification: Waiting List Management

**Feature Branch**: `012a-waiting-list`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Build the KDV waiting list — the day-to-day tool directors use to track families who want a place but don't have one yet. Entries (name, DOB, contact, requested location/start date), priority ordering, status lifecycle (waiting → offered → enrolled/withdrawn), an occupancy view honouring closure days, and a manual link to a child record on enrollment."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web).

### Primary Consumer

Director. No caregiver or parent surface in this feature (parent self-registration is feature 023).

### Workflow Boundary

This feature belongs to the **Child Lifecycle** workflow (workflow map's "Includes: Enrollment"), which has no detail file yet — `Workflows/child-lifecycle.md` is created as part of this feature, since 012a is the first feature to actually need it.

Actors: Director (registers entries, reorders priority, transitions status, links to a child record on enrollment); System (computes the occupancy view); future Parent/System once feature 023 ships self-registration (not built here).

Actions: Register a waiting-list entry; reorder priority within a location's queue; transition status (waiting → offered → enrolled/withdrawn); view projected occupancy for a location from a given date forward; link an enrolled entry to an existing child record, or create one if none exists.

Data Flow: Director web writes `waiting_list_entries` (tenant schema). The occupancy view reads `locations.MaxCapacity` (004) and active `contracts` (007, joined by `ContractedDays`/weekday + `StartDate`/`EndDate`) to project future free capacity per weekday, and reads `KdvClosureDay` (011) to exclude closed dates. On enrollment, an entry optionally gets a nullable FK to an existing `children.id` row (006) — set manually by the director, never auto-matched.

Outputs: A prioritized, filterable waiting list; a forward-looking occupancy view per location; an email to the contact when status becomes `offered`.

Cross-platform Impact: Director web only. No caregiver tablet or parent mobile surface ships in this feature.

### User Impact

This enables a Director to track and prioritize families waiting for a place, and see forward-looking occupancy against that queue, resulting in faster, better-informed enrollment decisions and less reliance on informal spreadsheets or paper lists.

### UX Requirements

Persona: A director managing one or more locations' waiting lists.

Platform: Director web, desktop-first at 1280px and above, high-density per `platform-rules.md`'s Director Web section.

User job: "Who is waiting, in what order, for which location, and do I have room for them?"

Success criteria: The director can see the full waiting list sorted by priority, filter by status, reorder priority, move an entry through its status lifecycle, and cross-check occupancy — all without leaving the screen.

Main flow: Waiting list table (sortable by priority, filterable by status and location) with an adjacent occupancy panel; row actions for status transition and priority reorder (up/down); a conversion flow prompting child-record creation on enrollment when no existing child is linked.

Loading/empty/error states: Per `design-system.md` — an empty list shows an icon + one sentence ("No families on the waiting list yet"); the occupancy panel shows a closed day as "Closed" (not as available or full); errors surface the i18n error key, never a raw error.

Accessibility: Priority reorder is keyboard-operable via up/down actions (not drag-and-drop only), per `platform-rules.md`'s keyboard-navigation requirement; visible focus rings; status badges pair color with an icon per `design-system.md`'s semantic-color rule.

Offline behavior: Not required — director web assumes network access for all writes, consistent with every other director-web feature to date (007a, 011, 012).

### Technical Requirements

API impact: `DirectorOnly` endpoints for CRUD on `waiting_list_entries`, priority reorder (scoped per location), status transition, the occupancy query, and enrollment-linking (to an existing child or none yet).

Data-model impact: New `waiting_list_entries` table (tenant schema, schema-per-tenant, no explicit tenant column — same pattern as every entity since 004). Nullable FK to `children.id`, set only on enrollment.

Security considerations: All endpoints restricted to `DirectorOnly` — no caregiver or parent access to waiting-list data.

Performance considerations: Expected volume is tens to low hundreds of rows per tenant — no pagination required; index `(location_id, status, priority)` for the common list/sort/filter query.

Testing requirements: Happy path (create, list, reorder, full status lifecycle, occupancy honoring closures) plus key negative flows (invalid status transition rejected, duplicate name+DOB flagged but not blocked, enrollment without a linked child prompts child creation).

## Clarifications

### Session 2026-07-10

- Q: BACKLOG.md's prompt says the occupancy view "reads from attendance (010) + contracts (007)" — is attendance actually the right source for a *forward-looking* (requested-start-date-onward) occupancy projection? → A: No, and this is corrected here rather than implemented as written. Feature 010's attendance records are a same-day/historical check-in log — they don't exist yet for future dates, which is exactly when a waiting-list occupancy check matters (a family typically wants a start date weeks or months out). The occupancy view is instead computed from active `Contract`s: for each weekday, count active contracts whose `ContractedDays` include that weekday and whose date range covers the target date, subtract from `Location.MaxCapacity`, then mark any date with a `KdvClosureDay` row for that location as `Closed` rather than showing a numeric count. This mirrors feature 012's own precedent (correcting a BACKLOG premise against what an earlier feature actually shipped, documented rather than silently followed).
- Q: Is `location_id` on a waiting-list entry required? → A: Yes. The DDL's comment ("which KDV they want") and the occupancy view's per-location projection both only make sense against a specific location; an entry MUST specify one at creation.
- Q: Is priority ordering global across all of a tenant's locations, or scoped per location? → A: Per location. Occupancy is projected per location, so ranking families "waiting for location A" against "waiting for location B" has no shared meaning. Reordering and the default sort both operate within a single selected location's queue.
- Q: Does the waiting list default to showing every status, or only active (`waiting`) entries? → A: Defaults to `waiting` only, with a status filter available to see `offered`/`enrolled`/`withdrawn` — matches the "day-to-day tool" framing (BACKLOG.md) and mirrors feature 007a's staff-list pattern (active-first, filterable).
- Q: Can `offered`, `enrolled`, or `withdrawn` entries be reordered, or is reordering restricted to `waiting` entries? → A: Restricted to `waiting` entries only. Priority encodes "who's next in line" — that's meaningless once an entry has moved past `waiting`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director registers and reviews the waiting list (Priority: P1)

A director adds a family to the waiting list with the child's name, date of birth, contact details, desired location, and requested start date, then reviews the list sorted by priority and filterable by status. This is the foundational capability — nothing else in this feature has data to operate on without it.

**Why this priority**: This is the entire point of the feature. No waiting list exists without it, and every other story (reorder, status transitions, occupancy, enrollment linking) depends on entries existing.

**Independent Test**: Can be fully tested by a director creating several entries for a location and verifying each persists with the correct fields and appears in the list, independent of reordering or status transitions.

**Acceptance Scenarios**:

1. **Given** a director on the waiting list screen, **When** they register a new entry with child name, date of birth, contact name, location, and requested start date, **Then** the entry is created with `status = waiting` and appears in that location's list.
2. **Given** a location's waiting list with several entries, **When** the director views the list, **Then** entries are shown sorted by priority (lowest number first) by default, with child name, DOB, contact, requested start date, and status visible per row.
3. **Given** a waiting list with entries in multiple statuses, **When** the director filters by a specific status, **Then** only entries in that status are shown.
4. **Given** two entries share the same child first name, last name, and date of birth, **When** the director views the list, **Then** both rows are visually flagged as a likely duplicate — neither is blocked from being created nor auto-merged.
5. **Given** a director registering a new entry, **When** they omit the optional contact email/phone or requested start date, **Then** the entry is still created successfully — only child name, date of birth, contact name, and location are required.

---

### User Story 2 - Director reorders the priority queue (Priority: P2)

A director adjusts the priority order of families waiting for a specific location, moving a family up or down the queue as circumstances change (e.g., a sibling already enrolled, a family's urgency changes).

**Why this priority**: A meaningful operational tool, but the list is still usable in registration order without it — so it ranks below the core capture-and-review capability.

**Independent Test**: Can be fully tested by reordering entries within one location's queue and verifying the new order persists and is reflected on reload, independent of status transitions or occupancy.

**Acceptance Scenarios**:

1. **Given** a location's waiting list with multiple `waiting` entries, **When** the director moves an entry up or down, **Then** the entry's priority value updates and the list re-sorts immediately.
2. **Given** the reorder action, **When** performed via keyboard (no mouse), **Then** the same up/down reordering is achievable without drag-and-drop.
3. **Given** entries across two different locations, **When** the director reorders priority for location A, **Then** location B's queue order is unaffected.

---

### User Story 3 - Director moves an entry through its status lifecycle (Priority: P2)

A director transitions a waiting-list entry from `waiting` to `offered` (contacting the family, which triggers an email notification), and then to either `enrolled` or `withdrawn` depending on the family's response.

**Why this priority**: This is what makes the waiting list an active workflow tool rather than a static list — core to the feature's purpose, but it operates on entries already captured in US1.

**Independent Test**: Can be fully tested by moving a single entry through waiting → offered → enrolled (and separately, waiting → offered → withdrawn), verifying each transition updates status correctly and that an email is sent on the transition to `offered`, independent of priority reordering.

**Acceptance Scenarios**:

1. **Given** an entry with `status = waiting`, **When** the director marks it `offered`, **Then** the status updates and an email notification is sent to the entry's contact (if a contact email is present).
2. **Given** an entry with `status = waiting` and no contact email, **When** the director marks it `offered`, **Then** the status updates successfully and no email is sent (logged, not failed).
3. **Given** an entry with `status = offered`, **When** the director marks it `enrolled` or `withdrawn`, **Then** the status updates accordingly.
4. **Given** an entry with `status = offered`, **When** the director reverts it back to `waiting` (e.g., the offer call didn't go as planned), **Then** the status reverts and no email is sent for this reverse transition.
5. **Given** an entry with `status = enrolled` or `status = withdrawn`, **When** the director attempts any further status transition, **Then** the system rejects it — both are terminal states for this feature (a withdrawn family that reapplies gets a new entry, per Edge Cases).
6. **Given** an entry with `status = waiting`, **When** the director marks it `withdrawn` directly (family cancels before ever being offered), **Then** the status updates to `withdrawn` without requiring the `offered` step.

---

### User Story 4 - Director views projected occupancy for a location (Priority: P3)

Alongside the waiting list, a director views which upcoming dates have free capacity at a given location, so they know whether — and when — they can realistically offer a place, honoring the location's closure calendar.

**Why this priority**: A valuable decision-support view, but the waiting list is functional without it (a director can still track and prioritize families) — so it ranks below the core queue mechanics.

**Independent Test**: Can be fully tested by querying occupancy for a location across a date range with known active contracts and known closure days, and verifying the free-capacity count and closed-day flags are both correct, independent of any specific waiting-list entry.

**Acceptance Scenarios**:

1. **Given** a location with `MaxCapacity = N` and active contracts covering some weekdays, **When** the director views the occupancy panel for a future date, **Then** the shown free capacity equals `N` minus the count of active contracts whose contracted days include that date's weekday and whose date range covers that date.
2. **Given** a date that has a published closure-calendar entry (011) for that location, **When** the director views the occupancy panel for that date, **Then** the date is shown as `Closed`, never as a numeric free-capacity count.
3. **Given** a waiting-list entry with a requested start date, **When** the director opens the occupancy panel from that entry, **Then** the panel defaults to that location, starting from that requested date.

---

### User Story 5 - Director enrolls a family and links the child record (Priority: P3)

When a family accepts an offered place and a contract is created (feature 007, outside this feature), the director marks the waiting-list entry `enrolled` and links it to the corresponding child record — creating a new child record first if one doesn't exist yet.

**Why this priority**: Closes the loop between the waiting list and the rest of the system, but is only reachable after US3's status lifecycle and is the least frequent action (happens once per successful enrollment, not daily).

**Independent Test**: Can be fully tested by transitioning an entry to `enrolled` and linking it to an existing child record, and separately by triggering the "create child record now?" prompt when no match exists — independent of occupancy or reordering.

**Acceptance Scenarios**:

1. **Given** an `offered` entry and a matching existing child record, **When** the director marks it `enrolled` and selects that child record, **Then** the entry is linked to the existing child (`waiting_list_entries.child_id` set).
2. **Given** an `offered` entry with no matching child record, **When** the director marks it `enrolled`, **Then** the system prompts "Create child record now?" pre-filled with the entry's first/last name and date of birth, and confirming creates a new child record and links it.
3. **Given** an entry marked `enrolled` without ever being linked to a child (director skips linking), **When** the director revisits the entry later, **Then** they can still link it to a child record at that point.

---

### Edge Cases

- A family withdraws and later reapplies — the director creates a brand-new entry; there is no merge with the old (now-terminal, `withdrawn`) entry (US3, scenario 5).
- Two entries share the same child name and date of birth — flagged visually on the list, never auto-merged or blocked (US1, scenario 4).
- An entry is enrolled but the matching child record doesn't exist yet — the conversion flow prompts child-record creation, pre-filled (US5, scenario 2).
- A requested date range includes a closure day — the occupancy view marks it `Closed`, never as available or "full" (US4, scenario 2).
- An entry's target location is deactivated (004) after the entry was created — the entry remains visible on the list (for the director to see and resolve), but the occupancy panel cannot compute projected capacity for a deactivated location.
- A director attempts an invalid status transition (e.g., `waiting` directly to `enrolled`, or any transition out of `enrolled`/`withdrawn`) — rejected with a clear error (US3, scenario 5).
- Concurrent priority reorders by two directors on the same location's queue — last write wins; this is a low-frequency administrative action with no legal/compliance stakes (unlike feature 007's contract day-overlap validator), so no advisory-lock mechanism is needed here.
- A location with zero active contracts — the occupancy view's free-capacity formula (FR-014) naturally returns the full `MaxCapacity` for every non-closed date, with no special-case handling required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to create a waiting-list entry specifying child first name, last name, date of birth, contact name, and location; contact email, contact phone, requested start date, and notes are optional.
- **FR-002**: The system MUST default a newly created entry's status to `waiting` and priority to a value ordering it after all existing entries for that location (appended to the end of the queue).
- **FR-003**: Directors MUST be able to view a location's waiting list sorted by priority (ascending — lower value is higher priority) and filterable by status; the list MUST default to showing only `waiting` entries, with a filter available to show `offered`, `enrolled`, or `withdrawn` entries.
- **FR-004**: The system MUST visually flag two or more entries that share the same child first name, last name, and date of birth as likely duplicates, without blocking creation or auto-merging them. Duplicate detection MUST compare against all entries for that location regardless of the currently applied status filter (e.g., a `waiting` entry and a `withdrawn` entry for the same child/DOB are still flagged as duplicates even when the list is filtered to show only `waiting` entries) — a director must never miss a duplicate simply because one of the pair is hidden behind the default status filter.
- **FR-005**: Directors MUST be able to reorder a `waiting`-status entry's priority (up/down) within its location's queue, via both a pointer-driven action and a keyboard-operable equivalent. `offered`, `enrolled`, and `withdrawn` entries are not reorderable — priority only encodes queue position for entries still awaiting an offer.
- **FR-006**: Reordering priority for one location MUST NOT affect the priority ordering of any other location's queue.
- **FR-007**: Directors MUST be able to transition an entry's status following these MUST-supported paths only: `waiting → offered`, `waiting → withdrawn`, `offered → enrolled`, `offered → withdrawn`, `offered → waiting`. Any other transition (including any transition originating from `enrolled` or `withdrawn`) MUST be rejected.
- **FR-008**: When an entry transitions to `offered` and has a non-empty contact email, the system MUST send an email notification to that contact. When no contact email is present, the system MUST complete the transition without error and log that no email was sent.
- **FR-009**: The system MUST NOT send an email notification for the `offered → waiting` reverse transition, or for any transition other than the one specified in FR-008.
- **FR-010**: Directors MUST be able to link an `enrolled` entry to an existing child record (006) by manual selection — never an automatic match.
- **FR-011**: When a director marks an entry `enrolled` and no matching child record is linked, the system MUST offer to create a new child record pre-filled with the entry's first name, last name, and date of birth.
- **FR-012**: An entry left `enrolled` without a linked child record MUST remain linkable at any later time.
- **FR-013**: The system MUST expose an occupancy view for a given location and date (or date range) that, for each date, returns either a free-capacity count or a `Closed` indicator.
- **FR-014**: The occupancy view's free-capacity count for a given location and date MUST be computed as that location's `MaxCapacity` minus the number of active contracts (007) whose contracted weekdays include that date's weekday and whose active date range covers that date.
- **FR-015**: The occupancy view MUST mark any date with a published closure-calendar entry (011) for that location as `Closed`, and MUST NOT return a numeric free-capacity count for that date.
- **FR-016**: All write operations (create, update, reorder, status transition, child-link) on waiting-list entries MUST be restricted to directors (`DirectorOnly` authorization policy).
- **FR-017**: All read operations (list, occupancy view) MUST be restricted to directors (`DirectorOnly` authorization policy).
- **FR-018**: All user-facing strings MUST use i18n keys (NL/FR/EN) — no hardcoded text.
- **FR-019**: A withdrawn entry MUST be retained (soft-delete semantics — no hard delete), preserving it for historical reference.
- **FR-020**: A waiting-list entry MUST require a `location_id` at creation — it cannot be created without specifying which location the family wants.

### Key Entities

- **WaitingListEntry**: A single family's request for a place at a specific location — child first/last name, date of birth, contact name/email/phone, requested start date, priority (per-location ordering), status (`waiting`/`offered`/`enrolled`/`withdrawn`), optional notes, optional link to an existing `Child` (006) once enrolled. Belongs to the tenant schema (schema-per-tenant, no explicit tenant column, per the established pattern from features 004/005/012). Terminal once `enrolled` or `withdrawn`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can register a new waiting-list entry in under 1 minute.
- **SC-002**: 100% of status transitions outside the FR-007 allow-list are rejected before being saved.
- **SC-003**: 100% of dates with a published closure-calendar entry for a location are shown as `Closed` in the occupancy view, never as a numeric free-capacity count.
- **SC-004**: 100% of entries sharing the same child first name, last name, and date of birth are visually flagged as likely duplicates on the list view.
- **SC-005**: A director can reorder a location's priority queue and see the new order reflected immediately, with zero effect on any other location's queue.

## Assumptions

- **Occupancy is computed from contracted capacity, not attendance records** — corrected from BACKLOG's original premise during specification (see Clarifications): feature 010's attendance data is same-day/historical and doesn't exist for the future dates a waiting-list occupancy check actually needs. This mirrors feature 012's precedent of correcting a BACKLOG assumption against what an earlier feature actually shipped, documented rather than silently followed.
- **Priority ordering is scoped per location**, not global across a tenant, since occupancy (and thus "can this family get in") is inherently per-location.
- **`location_id` is required** on every entry — the occupancy view and priority queue both only make sense against a specific location.
- **Email notification on `offered` reuses the existing `IEmailSender`/`EmailService` (MailKit-based, feature 001/003/005 precedent)** rather than building a new mechanism — feature 020's `EmailService` is not yet shipped, consistent with the original prompt's fallback instruction.
- **No advisory-lock/concurrency-safety mechanism is added for priority reordering** — unlike feature 007's contract day-overlap validator (a legal/compliance-significant check), reordering is a low-stakes administrative action where last-write-wins is an acceptable simplification.
- Duplicate detection (FR-004) is exact-match on first name + last name + date of birth — fuzzy/near-duplicate matching is out of scope for this feature.
- A deactivated location's existing waiting-list entries remain visible for the director to resolve manually; this feature does not add a deactivation guard blocking location deactivation while waiting-list entries reference it (no such guard is requested in scope, and waiting-list entries are pre-enrollment, not a hard dependency the way active contracts/staff are for features 004's `ILocationDeactivationGuard`).
