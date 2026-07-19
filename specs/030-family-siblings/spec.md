# Feature Specification: Family Siblings

**Feature Branch**: `030-family-siblings`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Support parents who have multiple children enrolled at the same KDV. This affects the parent app (single login → multiple children), invoicing (optional family bundling), day-reservation requests (one action for multiple children), and child/contract records."

## Clarifications

### Session 2026-07-19

- Q: When family invoice bundling is enabled, does a bundled invoice replace the existing
  per-child `Invoice` record, or wrap multiple per-child records for combined
  display/PDF/payment? → A: Wrap — keep one `Invoice` row per child (preserving 018's
  per-child reporting and 014a's per-invoice payment-reminder logic unchanged) and add an
  optional grouping so siblings' invoices render as one PDF/one parent-app entry with one
  combined total. Matches the codebase's established pattern of extending existing entities
  additively rather than restructuring them (013j's variant dimension, 013f's/014a's opt-in
  per-location settings) specifically to avoid breaking features that already key off the
  per-child shape.
- Q: When a bundled family invoice is paid (including via a 014a PSP payment link), does
  payment cover the whole bundle in one action, or does each child's invoice still need
  separate payment? → A: One payment action covers the whole bundled group; the system then
  marks every grouped child invoice paid together. A bundle that still required N separate
  payments would contradict the feature's own goal (SC-003: reduce the parent's monthly
  document/payment count to one).
- Q: For bundling, when siblings' `ChildContact` links partially overlap (e.g. two contacts
  are linked to both children, but each child has a *different* contact marked primary) —
  which contact's invoice do the children bundle into? → A: Bundle strictly by matching
  **primary** contact (existing `ChildContact.IsPrimary`) — children bundle together only when
  they share the same primary invoicing contact at that location. A child whose primary
  contact differs is invoiced separately even if it shares a secondary/non-primary contact
  with a sibling. Reuses the single already-authoritative "who gets billed" field instead of
  inventing new family-resolution logic.

## Product Context

### Feature Type

Mixed — data-model change (Location/Invoice extensions, ContactRelationship enum), API-backend
capability (bulk day reservations, sibling billing), and user-facing UI across parent mobile and
director web.

### Primary Consumer

Parent (child switcher already existed pre-feature; this adds bulk day-reservations, combined
family invoice, previous-children view) and Director (sibling-billing settings, the first web UI
for managing a child's linked family contacts).

### Workflow Boundary

Primarily **Billing & Payments** (`Workflows/billing.md` — sibling discount, family invoice
bundling) and **Child Lifecycle** (parent/family relationship visibility, previous-children
view). Also extends **Parent Communication**'s existing day-reservation flow (013a) to a
multi-child bulk action. No new workflow — this is a cross-cutting extension of three existing,
already-documented workflows, not a new business capability. Actors: Parent (multi-child),
Director (per-location billing settings, contact management), System (primary-contact-based
grouping resolution). Cross-platform impact: parent-mobile (bulk day reservations, combined
invoice view, previous-children view), director web (sibling-billing settings, new Contacts
tab), backend (all of the above). No caregiver-tablet surface.

### User Impact

This enables parents with multiple enrolled children to report absences for all of them in one
action and receive one combined invoice per family, and enables directors to configure sibling
discounts and manage a child's family contacts from the web for the first time, resulting in
less repetitive parent effort and billing that reflects real household structure.

### UX Requirements

**Parent persona** (parent-mobile, phone/portrait, per platform-rules.md): job is "act on all my
children in one place without repeating myself." Success criteria: the "all children" option on
the day-reservation form appears only when it would actually save a step (2+ active children);
a combined family invoice reads as one clear document, not a confusing merge. Main flow: existing
single-child screens are unchanged; the bulk option and previous-children view are additive entry
points, not a new navigation model (design-decisions.md's "parent app uses timeline as primary
home screen" decision is unaffected). Loading/empty/error states: bulk submission shows per-child
partial results, never a single opaque success/failure; previous-children entry point has no
empty-state affordance since it's hidden entirely when not applicable (FR-017). Accessibility: 48pt
touch targets on the new bulk-selection toggle and previous-children list cards, per
platform-rules.md. Offline: bulk day-reservation submission reuses the existing single-submission
offline-queue behavior (008) per child.

**Director persona** (director web, desktop-first, per platform-rules.md): job is "configure
per-location billing policy and manage family records without leaving the child profile or
location settings screens I already use." Success criteria: sibling-billing settings live on the
existing Invoicing tab (no new settings surface); the new Contacts tab follows the same tab
pattern 006a/013c already established on the child-detail screen. Loading/empty/error states: an
empty Contacts section prompts adding the first contact; duplicate-detection surfaces inline
during contact creation, not as a separate blocking step. Accessibility: standard director-web
focus-ring/keyboard-navigation requirements (platform-rules.md) — no touch-target floor.

### Technical Requirements

API impact, data-model impact, security, and testing requirements are detailed in
research.md/data-model.md/contracts/ (produced by `/speckit-plan`) — summarized: no new tables
(additive `Location`/`Invoice` columns, `ContactRelationship` enum extension only); new/extended
endpoints listed in contracts/family-siblings-api.md; authorization reuses the existing
`ICurrentParentContactResolver` link-check and `DirectorOnly` policies throughout (no new
authorization model); testing per Constitution V (real-PostgreSQL integration tests for the
money-correctness logic — sibling-discount tie-breaking, bundling group assignment, paid-cascade).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Report all children absent in one action (Priority: P1)

A parent with two or more children enrolled at the same location wakes up to a sick child (or a
household-wide reason like a family trip) and wants to report every enrolled sibling absent
without repeating the same form once per child.

**Why this priority**: The single most repetitive friction point today — `DayReservationForm`
already lists every linked child but forces one submission per child. This is the highest-volume
parent interaction (013a) affected by having siblings, so it's the first thing worth fixing.

**Independent Test**: Can be fully tested by a parent with 2 linked active children at the same
location submitting an absence request with "all children" selected, and verifying one
reservation record is created per child in a single API call, each independently visible in
"My requests" and independently actionable by the director.

**Acceptance Scenarios**:

1. **Given** a parent linked to 2 active children at the same location, **When** they open the
   absence-request form, **Then** they see an option to apply the request to all their children
   at that location in addition to picking a single child.
2. **Given** the parent selects "all children" and submits one absence request, **When** the
   submission completes, **Then** one reservation record exists per child, each subject to that
   child's own location's approval-mode settings (013f) independently.
3. **Given** the parent selects "all children" but the request type is disabled (013f) for one
   of the children's locations, **When** they submit, **Then** the reservation is created for
   the children where it's allowed and the parent sees which child(ren) were skipped and why.
4. **Given** a parent has only one active linked child, **When** they open the form, **Then**
   the "all children" option does not appear (nothing to bundle).
5. **Given** siblings enrolled at two different locations, **When** the parent selects "all
   children", **Then** each reservation is created against its own child's own location, each
   respecting that location's independent settings.

---

### User Story 2 - Sibling discount applied automatically on invoices (Priority: P2)

A director wants families with more than one enrolled child at their location to automatically
receive a discount on the second and subsequent children's invoices, without a manual step each
billing cycle.

**Why this priority**: Direct revenue/pricing feature and a named competitive gap ("D-care
handles this poorly") — but it depends on nothing from User Story 1 and can ship independently
once configured.

**Independent Test**: Can be fully tested by a director setting a sibling discount percentage on
a location, then generating invoices for a family with 2 children enrolled at that location, and
verifying the second child's invoice includes a separate, clearly labeled discount line reducing
the total, while the first (or only) child's invoice is unaffected.

**Acceptance Scenarios**:

1. **Given** a location with a sibling discount of 10% configured, **When** monthly invoices are
   generated for a parent with 2 children enrolled at that location, **Then** the child with the
   earlier contract start date is billed at full price and the other child's invoice includes an
   explicit discount line item reducing their total by 10%.
2. **Given** a location with no sibling discount configured (default), **When** invoices are
   generated for a multi-child family, **Then** no discount line appears on any invoice —
   behavior is unchanged from today.
3. **Given** a family with 3 children enrolled at the same location, **When** invoices are
   generated, **Then** the discount applies to every child except the one with the earliest
   contract start date at that location (i.e. all but the "first" child).
4. **Given** two children of the same parent enrolled at two different locations, **When**
   invoices are generated, **Then** no sibling discount applies at either location (discount
   only applies within one location, per the existing constraint).
5. **Given** two children linked to two different, unrelated parent accounts (no shared parent),
   **When** invoices are generated, **Then** no sibling discount applies — the discount is scoped
   to children sharing a parent contact, not merely enrolled at the same location.

---

### User Story 3 - One combined invoice per family (Priority: P3)

A director wants to optionally send one combined monthly invoice per family (covering all
enrolled siblings' charges) instead of one invoice per child, to reduce the number of documents
a parent has to reconcile and pay.

**Why this priority**: Valuable but explicitly opt-in per location and independent of pricing
(User Story 2) — a location can adopt the discount without bundling, or bundling without a
discount.

**Independent Test**: Can be fully tested by a director enabling family invoice bundling on a
location, generating monthly invoices for a 2-child family, and verifying exactly one PDF is
produced covering both children's line items (contracted days, extras, any sibling discount),
while a family with only one enrolled child at that location still gets a normal single invoice.

**Acceptance Scenarios**:

1. **Given** family invoice bundling is enabled on a location, **When** monthly invoices are
   generated for a parent with 2 children enrolled there, **Then** one PDF is produced containing
   both children's line items, clearly attributed per child, with one combined total.
2. **Given** family invoice bundling is disabled (default), **When** invoices are generated for
   the same family, **Then** each child receives their own separate invoice PDF — unchanged from
   today.
3. **Given** bundling is enabled and one sibling's contract started mid-month while the other's
   has been active all month, **When** the combined invoice is generated, **Then** each child's
   line items reflect their own correct billing period within the same document.
4. **Given** bundling is enabled, **When** the parent views their invoices in the parent app,
   **Then** they see the one combined invoice rather than per-child entries for that period.
5. **Given** two children each have a different primary contact (e.g. one child's primary is
   the mother, the other's is the father, even though both parents are linked to both
   children), **When** bundling runs, **Then** each child's invoice bundles only with siblings
   sharing that same primary contact — these two children are invoiced separately, not
   silently merged or dropped.

---

### User Story 4 - Director sees and manages a child's linked family contacts (Priority: P2)

A director looking at a child's profile wants to see every parent/guardian account linked to
that child, their relationship, and who's marked as the primary (invoicing) contact — and when
adding a new contact, wants to be told if that person is already a registered contact for
another enrolled child (e.g. a sibling) so they link the existing account instead of creating a
duplicate.

**Why this priority**: The backend for this already exists (contact linking endpoints from
006/013) but has no web UI at all — directors currently cannot see or manage this without a
database query. This blocks correct sibling data entry, which every other story depends on being
accurate.

**Independent Test**: Can be fully tested by a director opening a child's profile, viewing a new
Contacts section listing linked parents/guardians with relationship and primary flag, adding a
new contact whose email matches an existing contact, and confirming the UI offers to link the
existing contact instead of creating a new one.

**Acceptance Scenarios**:

1. **Given** a child with two linked contacts (mother, father), **When** a director opens the
   child's profile, **Then** they see both contacts listed with their relationship and which one
   is marked primary.
2. **Given** a director is adding a new contact to a child, **When** they enter an email/phone
   that matches an existing contact record (e.g. a parent already linked to a sibling), **Then**
   the system surfaces that existing contact as a suggested match to link instead of creating a
   new one.
3. **Given** a director proceeds anyway with a genuinely new person, **When** they save, **Then**
   a new contact is created and linked with the chosen relationship.
4. **Given** a child has no linked contacts yet, **When** a director views the Contacts section,
   **Then** they see an empty state prompting them to add the first contact (which becomes
   primary automatically, per existing behavior).
5. **Given** a director changes which contact is primary, **When** they save, **Then** the
   previous primary is cleared and the new one is set, without deleting either link (existing
   backend behavior — surfaced in the UI for the first time).

---

### User Story 5 - A departed sibling doesn't disappear without a trace (Priority: P3)

A parent whose older child has graduated out of the KDV (contract ended, child deactivated)
while a younger sibling remains enrolled should still be able to find the older child's history
(past daily reports, past invoices) without it cluttering their day-to-day view of active
children.

**Why this priority**: Lowest priority — a real but infrequent edge case (a family's enrollment
overlap ending) that doesn't block the core sibling-management value of Stories 1-4.

**Independent Test**: Can be fully tested by deactivating one of two siblings, confirming the
parent's default view shows only the remaining active child, and confirming a "previous
children" view still surfaces the deactivated child with access to their historical data.

**Acceptance Scenarios**:

1. **Given** a parent has one active and one deactivated child, **When** they open the app's
   default child list, **Then** only the active child appears.
2. **Given** the same parent, **When** they open a "previous children" view, **Then** the
   deactivated child appears there with their name, photo, and enrollment period.
3. **Given** the parent taps into the deactivated child from that view, **When** the child's
   historical daily reports/invoices are available, **Then** they can still view them read-only.
4. **Given** a parent has zero deactivated children, **When** they look for a "previous children"
   entry point, **Then** none is shown (no empty affordance for a state that can't happen yet).

---

### Edge Cases

- Twins (or any siblings) with identical group/contracted days: both must appear independently
  in every existing multi-child list (already true today) and both must be independently
  selectable in the new "all children" bulk day-reservation option.
- Shared custody: a child linked to two parent accounts. Both parents see the child in their own
  multi-child views; only one `ChildContact` link (per parent-child pair) is marked primary, and
  that flag is per child (already enforced) — it determines which parent's invoice each child's
  charges appear under when that parent has other children to bundle with, not a family-wide
  setting.
- A parent's two children are enrolled at two different locations, one with a sibling discount
  configured and one without: only the location with a discount configured applies it, and only
  to that location's own charges.
- A location turns family bundling off after having sent bundled invoices in a prior month:
  already-generated invoices are unaffected (invoices are generated per period, not retroactively
  regenerated); the next generation run produces separate invoices again.
- A contact is linked to a child as a non-primary "Emergency Contact" or "Authorised Pickup"
  relationship, not `Mother`/`Father`/`Guardian`: such a contact is not itself a parent-app user
  by default (no `TenantUserId`) and is excluded from sibling-discount/bundling logic, which only
  considers linked contacts that have an active parent-app account.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The day-reservation form (absence, extra day, exchange) MUST offer an "apply to
  all my children" option whenever the requesting parent has 2 or more active linked children,
  and MUST NOT show that option when they have only 1.
- **FR-002**: Submitting a bulk day reservation MUST create one independent reservation record
  per selected child, each evaluated against its own location's reservation settings (013f) and
  own existing validation rules (013a) — a rule that blocks one child's reservation MUST NOT
  block the others.
- **FR-003**: When a bulk submission is partially blocked (e.g. one child's location has that
  request type disabled), the parent MUST be told which child(ren) succeeded and which were
  skipped and why, in the same submission response.
- **FR-004**: A location MUST support an optional sibling-discount percentage, defaulting to 0
  (no discount), configurable by a director.
- **FR-005**: When generating monthly invoices, if a location has a sibling discount configured
  and a parent contact has 2 or more active children enrolled at that same location, the
  system MUST apply the discount to every such child's invoice except the one with the earliest
  contract start date at that location, as a distinct, clearly labeled line item (not a silent
  reduction of the base charge).
- **FR-006**: Sibling-discount eligibility MUST be based on children sharing the same active
  primary parent contact at the same location (the identical rule FR-008 uses for bundling) —
  children who merely attend the same location without a shared primary contact, or who share
  only a non-primary contact, MUST NOT receive the discount.
- **FR-007**: A location MUST support an optional "family invoice bundling" toggle, defaulting to
  off (one invoice per child, current behavior).
- **FR-008**: When family invoice bundling is enabled for a location, monthly invoice generation
  MUST group the invoices of children who share the same primary invoicing contact at that
  location for the period into one combined PDF and one combined parent-facing total, with each
  child's charges attributed per line — the underlying per-child invoice record MUST continue to
  exist individually (unchanged for reporting/payment-tracking purposes per Clarifications),
  only its presentation and payment are combined.
- **FR-009**: A combined family invoice MUST correctly reflect each child's own contract period
  within the shared document when siblings' contracts started on different dates.
- **FR-009a**: Recording a payment against a bundled family invoice MUST mark every grouped
  child invoice as paid together, in one action (per Clarifications) — the parent MUST NOT be
  required to pay each grouped child's invoice separately.
- **FR-010**: The parent app's invoice list MUST show a bundled family invoice as a single entry
  when bundling is enabled, and continue showing one entry per child's invoice when it is not.
- **FR-011**: A child's profile in the director web app MUST display all contacts (parents/
  guardians/others) linked to that child, each with their relationship and whether they are the
  primary contact for that child.
- **FR-012**: A director MUST be able to view, add, and remove a child's linked contacts, and
  change which linked contact is primary, from the child's profile.
- **FR-013**: When a director adds a new contact to a child, the system MUST check for an
  existing contact record with a matching email or phone number and, if found, offer to link the
  existing contact instead of creating a duplicate.
- **FR-014**: The `ContactRelationship` values available when linking a contact MUST include a
  foster-parent relationship and a general "other" relationship, in addition to the existing
  Mother/Father/Guardian/Emergency Contact/Authorised Pickup values.
- **FR-015**: The parent app MUST provide a "previous children" view listing the parent's
  deactivated (no longer active) linked children, separate from the default active-children
  view, showing each one's name, photo, and enrollment period.
- **FR-016**: A deactivated child reachable from the "previous children" view MUST still expose
  their historical daily reports and invoices to the linked parent in read-only form.
- **FR-017**: The "previous children" entry point MUST NOT be shown to a parent with zero
  deactivated linked children.
- **FR-018**: All new user-facing strings introduced by this feature MUST be delivered via i18n
  keys (NL/FR/EN), consistent with existing features.

### Key Entities

- **Location sibling-billing settings**: Two new per-location settings — a sibling discount
  percentage (0 by default) and a family-invoice-bundling toggle (off by default) — extending
  the existing per-location invoicing configuration (014/014a).
- **Family invoice group**: A presentation/payment grouping over existing per-child invoices —
  when bundling is enabled, invoices of children sharing the same primary contact at a location
  for the same period are grouped into one PDF, one parent-app entry, and one payment action,
  while each underlying per-child invoice record is retained unchanged for reporting (018) and
  payment-tracking (014a) purposes (per Clarifications).
- **Contact / child-contact link**: No new entity — this feature surfaces and extends the
  existing parent/guardian-to-child relationship (a person record optionally linked to a parent
  login, and its per-child link carrying relationship and primary-contact status) that already
  underlies every "multiple children, one parent" capability in the product; this feature adds
  foster-parent and other relationship options to it and gives it its first web admin UI.
- **Bulk day reservation**: Not a new stored entity — a single parent-facing submission action
  that fans out into one existing reservation record per selected child.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A parent with multiple enrolled children can report all of them absent in one
  submission, in under 30 seconds, instead of repeating the absence form once per child.
- **SC-002**: A director can configure a sibling discount for a location in under 1 minute, and
  the discount appears correctly on the next invoice run with zero manual per-family adjustment.
- **SC-003**: A director can enable combined family invoicing for a location in under 1 minute,
  reducing the number of separate invoice documents a multi-child family receives per month to
  one.
- **SC-004**: A director can find every contact linked to a child, and correctly link a sibling's
  existing parent contact instead of creating a duplicate, without leaving the child's profile
  page.
- **SC-005**: 100% of locations that do not opt into a sibling discount or bundling see zero
  change in existing invoice output (backward compatible by default).
- **SC-006**: A parent whose child has left the KDV can still locate that child's historical
  records without them appearing in daily active-child views.

## Assumptions

- The existing `Contact`/`ChildContact` many-to-many relationship (introduced in feature 006 and
  extended in 013) already implements the "family membership" data model the original backlog
  entry proposed as a new `family_memberships` table — including per-child-contact primary
  designation and enforcement. This feature extends and surfaces that existing model (adding two
  relationship values, adding a web UI, adding sibling-aware billing logic) rather than
  introducing a duplicate table.
- "Family" for discount/bundling purposes means "children sharing an active parent contact at
  the same location," not a separate explicit family/household record — consistent with how
  co-parenting and shared custody already work via multiple contacts per child.
- Sibling discount/bundling are per-location, opt-in settings (default off/0%), matching the
  precedent set by 013f (per-location reservation modes) and 014a (per-location payment
  reminders) — a feature that changes billing output must not change any existing location's
  invoices unless a director explicitly opts in.
- "Earliest contract start date at that location" is the tie-breaker for which sibling is
  full-price; this is a reasonable, deterministic default absent a more specific rule from the
  business, and mirrors common real-world sibling-discount policy (the longest-enrolled child is
  full price). If two siblings' contracts share the exact same start date (e.g. twins signed on
  one contract date), the earlier-created contract record is the secondary, fully deterministic
  tie-breaker — this only affects which of two identical-priced siblings is labeled "full price"
  vs. "discounted," not the total amount billed to the family.
- Custody-schedule-aware reservations/billing (which parent has the child on which day) and
  split/divided invoicing between two parents remain out of scope, per the original backlog
  entry — this feature does not change that.
- Deactivation (`Child.DeactivatedAt`) remains an explicit director action (existing behavior);
  this feature does not introduce automatic deactivation on contract end, only a parent-facing
  view of children already deactivated by that existing mechanism.
