# Feature Specification: Developmental Milestones

**Feature Branch**: `016-developmental-milestones`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "Build developmental milestone tracking — monitoring each child's
progress across developmental domains (motor_gross, motor_fine, language, cognitive, social,
emotional, self_care). Caregivers record observations (emerging/achieved/not_yet) from the
caregiver app; observations are immutable (append-only history). Director views/manages from web
admin. Milestone portfolio is shared with parents, either as an in-app view or a PDF export.
Age-appropriate milestones are highlighted based on the child's current age. Seed data uses the
standard Belgian developmental framework."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability, with UI across all three surfaces: caregiver
tablet records, director web manages, parent mobile views).

### Primary Consumer

Caregiver (records observations). Director (views/manages a child's portfolio). Parent (views
their child's shared portfolio as the ultimate beneficiary — reassurance, not action).

### Workflow Boundary

**Daily Child Care** (`workflows.md`) — explicitly listed there: "Learning observations." No new
workflow needed.

Actors: Caregiver (records an observation during daily care). Director (views/manages a child's
full portfolio). Parent (views their child's portfolio). System (resolves current age-appropriate
milestone band, renders the optional PDF export).

Actions: Caregiver selects a child → picks a domain and milestone → records a status
(`emerging`/`achieved`/`not_yet`) with an optional note → observation is saved immutably. Director
opens a child's profile → views the full milestone portfolio grouped by domain, with the
age-appropriate band highlighted and full observation history per milestone. Parent opens their
child's portfolio in the parent app (or downloads a PDF) → sees the same grouped, age-highlighted
view, framed warmly rather than clinically.

Data Flow: Caregiver app → API → tenant DB (`child_milestone_observations`, append-only) →
Director web (management view, same tenant data) + Parent app (read-only, same data scoped to
their own child) / on-demand PDF render. Reference data (`developmental_domains`,
`developmental_milestones`) is shared, admin-maintained data queried by all three surfaces, not
duplicated per child.

Outputs: milestone portfolio (grouped by domain, latest status per milestone, full history
available), age-appropriate current-focus highlighting, optional on-demand PDF export.

Cross-Platform Impact: Caregiver tablet (record — primary). Director web (view/manage —
primary). Parent mobile (view — primary). No location/attendance/billing impact.

### User Impact

This enables a caregiver to record a child's developmental progress across standard domains
during daily care, resulting in parents and directors having an always-current, age-appropriate
view of that child's development without any manual compilation.

### UX Requirements

**Persona**: Caregiver (tablet, landscape, per `platform-rules.md`'s Caregiver Tablet section)
recording a quick observation during an otherwise busy day. Director (desktop web) reviewing and
managing a child's full developmental history. Parent (mobile, portrait) checking in on their
child's progress the same way they already check daily reports.

**Platform**: Caregiver tablet (record). Director web (view/manage). Parent mobile (view,
optional PDF download).

**User job (caregiver)**: "While I'm with a child, quickly log what I'm observing about their
development, without interrupting the rest of my day."

**User job (director)**: "See a child's full developmental picture at a glance — what's on track,
what's emerging, what needs attention — without digging through raw event logs."

**User job (parent)**: "See how my child is developing, in plain language, the same place I
already check daily updates."

**Success criteria**:

- A caregiver can record an observation in 3 taps or fewer (select child → select milestone →
  select status), matching the tablet's quick-action pattern already established for child
  events (009).
- A director can open any child's profile and see their full milestone portfolio, grouped by
  domain, with the age-appropriate band visually distinguished from the rest, without any
  additional navigation.
- A parent can find their child's milestone portfolio within the existing per-child area of the
  parent app and understand, without training, which milestones are current-focus for their
  child's age.
- Milestone descriptions and all surrounding UI read in the parent's/caregiver's/director's
  selected language (NL/FR/EN) with no untranslated strings.

**Main flow (caregiver)**: Caregiver opens a child's profile on the tablet → taps "Milestones" →
picks a domain (or sees all, age-appropriate ones surfaced first) → picks a milestone → taps a
status (emerging/achieved/not yet) → optionally adds a short note → saves. The new observation
appears immediately at the top of that milestone's history; no existing observation is ever
edited.

**Main flow (director)**: Director opens a child's profile on web → the "Milestones" tab shows
every domain as a labelled group, each with its milestones ordered by age band, the
age-appropriate band visually distinguished, and the most recent status per milestone visible
without opening anything further; clicking a milestone reveals its full observation history.

**Main flow (parent)**: Parent opens their child in the parent app → a "Development" (or
similarly warm-worded, non-clinical) section shows the same domain-grouped view, written in plain
language, with an option to download a PDF snapshot to share or keep.

**Loading/empty/error states**: Caregiver — empty state before any observation exists for a
milestone ("No observations yet" + icon, per `design-system.md`'s empty-state pattern); the
observation-entry sheet shows a clear save-confirmation and an inline error if the save fails
(matching the existing child-event entry pattern, including offline queueing). Director — empty
state for a child with zero observations ("No milestones recorded yet for {child}"). Parent —
empty state ("Nothing recorded yet — check back soon") rather than a blank screen; PDF download
failure shows a retryable inline error, not a silent failure.

**Accessibility**: Status conveyed with icon + color, never color alone (achieved/emerging/not
yet each get a distinct icon per `design-system.md`'s Status Indicators section). Caregiver tablet
touch targets meet the 48pt floor. WCAG AA contrast on all new text/badges.

**Offline behavior**: Caregiver app already has offline sync infrastructure (008) — a new
observation write queues offline exactly like existing child-event writes and syncs when
connectivity returns, using the same optimistic-local-then-sync pattern.

### Technical Requirements

**API impact**: New endpoints — list developmental domains/milestones reference data (shared,
authenticated read for all three surfaces), record an observation (caregiver/director write),
list a child's milestone portfolio (director read, full history), a parent-scoped portfolio query
(own children only), and an on-demand PDF export of a child's portfolio.

**Data-model impact**: `DevelopmentalDomain` and `DevelopmentalMilestone` as a **shared,
platform-wide reference catalog** (public schema) — not duplicated per tenant — following the
established `VaccineType` catalog precedent (013g/013h), since no per-tenant reference-data
seeding mechanism exists anywhere in this codebase and inventing one solely for this feature would
duplicate identical Belgian-framework data across every tenant schema for no benefit. See
Assumptions for why this supersedes BACKLOG.md's original "stored in tenant schema" phrasing.
`ChildMilestoneObservation` is tenant-scoped, append-only (no update/delete path), referencing
`MilestoneId` without a cross-schema foreign key (same pattern `VaccineRecord` already uses for
`VaccineTypeId`).

**Security considerations**: Tenant-scoped read/write on observations; caregiver/director write
access to observations (`DeviceOrStaffOrDirector`-equivalent policy, matching `ChildEventEndpoints`);
parent read access strictly scoped to their own linked child (matching
`GetParentDailySummaryQuery`'s `ChildContact` ownership check); reference catalog is read-only to
all three surfaces in this feature (no in-app editing UI — see Assumptions).

**Performance considerations**: Portfolio view must load a child's full observation history
efficiently — index `child_milestone_observations` by `child_id` (and `milestone_id` for
per-milestone history lookups).

**Testing requirements**: Happy path (record an observation, view portfolio grouped by domain,
age-band highlighting resolves correctly for a given age); key negative flows (a parent attempting
to access another family's child's portfolio is rejected; an invalid status value is rejected by
validation); regression edge case (a milestone recorded `achieved` then later `not_yet` preserves
both observations in history, current status reflects the latest); age-band boundary edge case (a
child exactly at a band boundary, e.g. 15 or 21 months, is included in that band).

## Clarifications

No `[NEEDS CLARIFICATION]` markers were needed — the BACKLOG.md prompt block plus the established
`VaccineType`/`VaccineRecord` catalog-vs-per-child-record precedent (013g/013c) supplied a
reasonable default for the one real architectural ambiguity (where the reference catalog lives).
See Assumptions for that decision and its reasoning.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver records a milestone observation (Priority: P1)

While caring for a child, a caregiver notices (or wants to check off) a developmental milestone
and wants to log it without breaking the flow of their day.

**Why this priority**: Without recording, there is no data for any other part of this feature to
show — this is the entire input side of the feature.

**Independent Test**: Can be fully tested by opening a child's profile on the caregiver app,
recording an observation for a milestone with a given status, and confirming it is saved and
appears in that milestone's history — independent of the director or parent views.

**Acceptance Scenarios**:

1. **Given** a child's profile open on the caregiver tablet, **When** the caregiver selects a
   domain, a milestone, and a status, **Then** the observation is saved and immediately visible
   in that milestone's history with the correct caregiver, timestamp, and status.
2. **Given** a milestone already has an `achieved` observation, **When** the caregiver later
   records a `not_yet` observation for the same milestone (a regression), **Then** both
   observations remain in history unedited, and the most recent one (`not_yet`) is shown as the
   current status.
3. **Given** the tablet is offline, **When** the caregiver records an observation, **Then** it is
   queued locally and synced once connectivity returns, matching the existing child-event offline
   pattern.
4. **Given** an existing observation, **When** anyone attempts to edit or delete it (via API),
   **Then** the system rejects the request — observations are append-only.

---

### User Story 2 - Director views a child's milestone portfolio (Priority: P1)

A director wants to see a child's full developmental picture — what's on track, what's emerging,
what's overdue — without digging through raw logs.

**Why this priority**: The recorded data has no operational value until someone can review it in
context; this is the other core half of the feature alongside recording.

**Independent Test**: Can be fully tested by seeding several observations across domains for a
child of a known age, opening that child's profile on web, and confirming the portfolio groups
correctly by domain with the age-appropriate band highlighted — independent of how the parent view
renders.

**Acceptance Scenarios**:

1. **Given** a child with observations across multiple domains, **When** a director opens that
   child's Milestones tab, **Then** milestones are grouped by domain, each showing its most
   recent status.
2. **Given** a child who is 18 months old, **When** the director views the portfolio, **Then**
   milestones in the 15–21 month band are visually highlighted as the current focus, distinct
   from earlier/later bands.
3. **Given** a milestone with multiple historical observations, **When** the director opens that
   milestone, **Then** the full observation history is visible in chronological order.
4. **Given** a child with no observations recorded yet, **When** the director opens the Milestones
   tab, **Then** a clear empty state is shown, not a blank or broken screen.

---

### User Story 3 - Parent views their child's shared milestone portfolio (Priority: P2)

A parent wants reassurance and insight into how their child is developing, in the same place they
already check daily updates.

**Why this priority**: Parent visibility is the feature's stated purpose ("share... with
parents"), but the platform is functional without it once recording (P1) and director review (P1)
exist — this extends the audience rather than changing the core mechanism.

**Independent Test**: Can be fully tested by seeding observations for a child, then opening the
parent app as that child's linked contact and confirming the same domain-grouped, age-highlighted
view is visible, worded warmly — independent of caregiver/director actions happening at the same
time.

**Acceptance Scenarios**:

1. **Given** a child with recorded observations, **When** their linked parent opens the
   Development section of the parent app, **Then** they see the same domain-grouped,
   age-highlighted portfolio, in plain, warm language (not clinical/database phrasing).
2. **Given** a parent who is not linked to a given child, **When** they attempt to access that
   child's portfolio, **Then** access is rejected.
3. **Given** a child with no observations yet, **When** the parent opens the Development section,
   **Then** a warm empty state is shown ("Nothing recorded yet — check back soon"), not an error.

---

### User Story 4 - Portfolio PDF export (Priority: P3)

A director or parent wants a point-in-time PDF snapshot of a child's milestone portfolio to share
or keep (e.g. for a pediatrician visit, or a family record).

**Why this priority**: A genuinely useful extra, but the in-app views (P1/P2) already deliver the
feature's full value; PDF is an additional distribution channel, not core functionality.

**Independent Test**: Can be fully tested by triggering a PDF export for a child with existing
observations and confirming the rendered document matches the current in-app portfolio content —
independent of the in-app views themselves.

**Acceptance Scenarios**:

1. **Given** a child with recorded observations, **When** a director or the child's parent
   triggers a PDF export, **Then** a PDF is generated on demand reflecting the current portfolio
   state, grouped by domain with age-appropriate highlighting preserved as a visual cue in the
   document.
2. **Given** a child with no observations, **When** a PDF export is triggered, **Then** the PDF
   still generates, showing the empty state text rather than failing.

---

### Edge Cases

- A new milestone is added to the reference catalog (seed/catalog update) — existing observations
  for other milestones are unaffected; the new milestone simply appears with no history until
  observed.
- A child's age crosses into the next milestone band between visits — the age-appropriate
  highlight is computed live from the child's current age at view time, not cached or
  snapshotted, so it always reflects the present.
- A child sits exactly on a band boundary (e.g. exactly 15 months, where one band is 15–21
  months) — inclusive boundaries mean the child is included in that band.
- A milestone is recorded, then later regresses (`achieved` → `not_yet`) — both observations are
  preserved; only the latest determines "current status."
- A parent attempts to access a milestone portfolio for a child they are not linked to — rejected,
  same as every other parent-scoped query in this codebase.
- A caregiver attempts to submit an observation with a status outside the fixed set
  (`emerging`/`achieved`/`not_yet`) — rejected by validation before it reaches the database.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a shared, platform-wide reference catalog of developmental
  domains (`motor_gross`, `motor_fine`, `language`, `cognitive`, `social`, `emotional`,
  `self_care`) and, per domain, developmental milestones with an age band (`age_from_months`,
  `age_to_months`) and NL/FR/EN descriptions, seeded from the standard Belgian developmental
  framework.
- **FR-002**: Caregivers MUST be able to record an observation for a child against any catalog
  milestone, capturing a status (`emerging`/`achieved`/`not_yet`), the observing caregiver, an
  observation date, and an optional free-text note.
- **FR-003**: Once recorded, an observation MUST be immutable — no update or delete path exists;
  a changed assessment (including a regression) is captured by recording a new observation for
  the same milestone.
- **FR-004**: The system MUST resolve, for any child, which milestone age band is
  currently age-appropriate based on the child's current age, and this resolution MUST be
  computed live (not cached against a stale age) every time a portfolio is viewed.
- **FR-005**: Directors MUST be able to view a child's full milestone portfolio, grouped by
  domain, showing each milestone's most recent status and the full chronological history behind
  it, with the age-appropriate band visually distinguished from other bands.
- **FR-006**: Parents MUST be able to view their own linked child's milestone portfolio, using the
  same domain-grouped, age-highlighted structure as the director view, worded in warm, plain
  language rather than clinical terminology.
- **FR-007**: Parent access to a milestone portfolio MUST be restricted to children the requesting
  parent is a linked contact of; access to any other child's portfolio MUST be rejected.
- **FR-008**: The system MUST support generating an on-demand PDF export of a child's current
  milestone portfolio, reflecting live data at generation time (not a stored, point-in-time
  document), available to both directors and the child's linked parents.
- **FR-009**: All user-facing strings introduced by this feature — including milestone and domain
  names/descriptions — MUST be available in NL/FR/EN, matching every prior feature's i18n
  convention.
- **FR-010**: Recording an observation MUST be tenant-scoped and restricted to caregivers/
  directors with access to that child, matching the authorization pattern already established for
  child events.
- **FR-011**: The reference catalog (domains and milestones) MUST be read-only within this
  feature — no in-app create/edit/deactivate UI is introduced (see Assumptions); catalog changes
  are a platform-level operation until a future feature (if ever) extends 013h's platform-admin
  pattern to this catalog.
- **FR-012**: An observation submitted with a status outside the fixed set
  (`emerging`/`achieved`/`not_yet`) MUST be rejected by validation before persistence.

### Key Entities

- **Developmental Domain**: A shared, platform-wide category of development (e.g. `motor_gross`,
  `language`) with a code and NL/FR/EN display names. Read-only reference data in this feature.
- **Developmental Milestone**: A shared, platform-wide milestone within a domain, with an age band
  (`age_from_months`–`age_to_months`), NL/FR/EN descriptions, and a sort order. Read-only
  reference data in this feature.
- **Child Milestone Observation**: A tenant-scoped, append-only record of one caregiver's
  assessment of one child against one milestone at a point in time — status, observation date,
  observing caregiver, and an optional note. Never updated or deleted; a new observation is always
  added instead.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can record a milestone observation in 3 taps or fewer from a child's
  profile.
- **SC-002**: A director can view any child's complete, domain-grouped milestone portfolio with
  the age-appropriate band highlighted, in a single navigation step from that child's profile.
- **SC-003**: A parent can locate their child's milestone portfolio within the existing per-child
  area of the parent app without additional guidance.
- **SC-004**: 100% of recorded observations remain present and unaltered in history after a
  subsequent (including regressing) observation is recorded for the same milestone.
- **SC-005**: Zero cross-tenant or cross-family data exposure — a parent can never view another
  family's child's milestone data.

## Assumptions

- **Reference catalog lives in the shared public schema, not per tenant** — this supersedes
  BACKLOG.md's original "tenant-agnostic but stored in tenant schema" phrasing. No mechanism for
  seeding reference data into a freshly-provisioned tenant schema exists anywhere in this
  codebase (confirmed: `TenantProvisioningService` only creates the schema, replays migrations,
  and upserts the director user). The established precedent for exactly this shape of data —
  admin-maintained, tenant-agnostic, referenced by tenant-scoped records without a cross-schema
  FK — is `VaccineType` (013g) plus its platform-admin management surface (013h). Following that
  precedent avoids inventing a new, redundant per-tenant seeding mechanism solely for this
  feature, and avoids identical Belgian-framework data being duplicated (and potentially drifting)
  across every tenant schema.
- **No platform-admin CRUD UI for the milestone catalog in this feature** — unlike 013h, this spec
  scopes the catalog to read-only, seeded-once reference data. The BACKLOG prompt describes
  seeded data using "the standard Belgian developmental framework (not a custom one)," implying a
  fixed, non-editable set for now; a future feature can extend 013h's platform-admin pattern to
  this catalog if a real need for in-app editing emerges.
- **PDF export is rendered on demand, not stored** — a milestone portfolio is a continuously
  growing, live view (unlike 015's fiscal attestations, which are a point-in-time legal document
  that must remain stable once filed). Rendering fresh each time, matching invoice PDFs' (014)
  precedent, avoids a stale, out-of-sync stored snapshot.
- Age-band boundaries (`age_from_months`/`age_to_months`) are inclusive on both ends.
- "Observed by" is the caregiver who is recording, consistent with how `child_events` derives
  `recorded_by` from the active shift — no separate PIN-confirmation step is required for a
  milestone observation (unlike medication/temperature events), since a developmental observation
  is not a medical/safety-critical action.
- Custom curriculum frameworks (Montessori, Pikler), learning-journal narrative format, and
  MeMoQ quality self-evaluation remain explicitly out of scope, per BACKLOG.md.
