# Feature Specification: Enrolment Contracts

**Feature Branch**: `007-contracts`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Build the enrolment contract — the agreement between a KDV location and a child's family defining care days, rate, and consent. Contract record linking a child to a location with start/end dates, contracted weekdays and hours, daily rate in cents, and draft/active/ended status. Contract versioning (old ended, new created) with full audit trail. Photo/media consent types stored as JSONB, replacing the old single photo_consent boolean. Contract PDF generation via QuestPDF including terms, consent, and a signature line. A child may have at most one active contract per location. Split-location enrolment: a child may hold two simultaneous active contracts at different locations in the same organisation provided contracted weekdays don't overlap, enforced by a day-overlap validator on activation. Phase 1 = private KDVs only, no IKT rate fields yet but schema must allow for them later. Money stored in cents. Out of scope: IKT attest fields, e-signature, SEPA mandate, wisseldagen."

## Clarifications

### Session 2026-07-07

- Q: Should a director be able to end an active contract entirely (child leaves this KDV for good) without creating a successor contract, or does every "ended" contract require an amendment that produces a replacement? → A: Yes — support a standalone termination action distinct from amendment; amendment always produces a successor, termination never does.
- Q: Is "planned hours per day" a specific time-of-day range (e.g., 8:00–17:00) or just a numeric duration (e.g., 8 hours)? → A: A time-of-day range (start time and end time) per contracted day — matches how real KDV contracts are written and gives later features (attendance variance, late-pickup handling) something concrete to compare actual check-in/out times against.
- Q: Can each contracted weekday within one contract have its own hours (e.g., full day Mon/Wed, half day Fri), or must all contracted weekdays share one identical hours range? → A: Each contracted weekday has its own independent hours range — common real-world schedules mix full and half days across the week.
- Q: May a contract's start date be in the past (e.g., an organisation digitizing a family that has already been attending for months), or must it be today or later? → A: Past start dates are allowed — organisations onboarding onto the system need to record contracts for children already enrolled before the system existed.
- Q: Must a location be active (not deactivated) to create or activate a fresh contract there, matching the precedent set for group creation in feature 006? → A: Yes — a deactivated location cannot receive new or activated contracts, reusing the existing `errors.location.not_found` behavior from feature 004/006.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and activate an enrolment contract (Priority: P1)

A director creates a draft enrolment contract for a child at one of the organisation's locations, specifying the care period, which weekdays the child attends and the planned hours each day, the daily rate the family pays, and the photo/media consent choices. The director then activates the contract, making it the child's binding agreement at that location.

**Why this priority**: Without a contract, a child cannot be legitimately enrolled at a location — this is the foundational record every other capability in this feature (and future features like attendance and invoicing) depends on.

**Independent Test**: Can be fully tested by creating a draft contract for a child at a location with a set of weekdays/hours/rate, activating it, and confirming it appears as the child's active contract at that location with status `active`.

**Acceptance Scenarios**:

1. **Given** a child with no existing contract at a location, **When** a director creates a draft contract specifying weekdays, hours, daily rate, and consent choices, **Then** the contract is created with status `draft` and is not yet binding.
2. **Given** a draft contract with valid terms, **When** the director activates it, **Then** its status becomes `active` and it is returned as the child's current contract at that location.
3. **Given** a child who already has an active contract at a location, **When** a director attempts to activate a second contract for the same child at the same location, **Then** the activation is rejected and the existing active contract is unaffected.

---

### User Story 2 - Split-location enrolment across non-overlapping days (Priority: P1)

A director enrols a child at a second location on different weekdays than the child's existing contract at another location (e.g., Mon+Tue at Location A, Wed+Thu at Location B). The system allows both contracts to be active simultaneously because their contracted weekdays don't overlap, but rejects activation if any weekday is claimed by more than one active contract for that child.

**Why this priority**: Split-location care is an explicit, named business rule in this domain (children attending two KDVs on different days) and must be enforced correctly from the start — getting it wrong either blocks legitimate families or silently creates double-booked days that break attendance and billing.

**Independent Test**: Can be fully tested by activating a contract for a child at Location A covering Mon+Tue, then activating a second contract for the same child at Location B covering Wed+Thu (succeeds), then attempting a third contract covering Tue+Wed at a third location (fails on the Tuesday conflict).

**Acceptance Scenarios**:

1. **Given** a child has an active contract at Location A for Mon+Tue, **When** a director activates a new contract for the same child at Location B for Wed+Thu, **Then** activation succeeds and the child has two simultaneously active contracts.
2. **Given** a child has an active contract at Location A for Mon+Tue, **When** a director activates a new contract for the same child at Location B for Tue+Wed, **Then** activation is rejected because Tuesday overlaps, and neither the new contract's status nor the existing contract is changed.
3. **Given** two staff members concurrently submit activation requests for two different new contracts of the same child that overlap on a weekday, **When** both requests are processed, **Then** exactly one activation succeeds and the other is rejected with an overlap error — the system never leaves the child with two active contracts sharing a weekday.

---

### User Story 3 - Amend contract terms with full audit trail (Priority: P2)

A director needs to change an active contract's terms (e.g., the family adds a weekday, or the rate changes). Rather than editing the contract in place, the director amends it: the current contract is ended (its end date is set) and a new contract is created with the updated terms, preserving the full history of what terms applied over what period.

**Why this priority**: Rate and schedule changes happen regularly (rate increases, families adjusting their care days) and the audit trail is required for billing disputes and historical reporting — but it is secondary to being able to create and activate a contract at all.

**Independent Test**: Can be fully tested by activating a contract, amending it with a new daily rate and a later start date for the new terms, and confirming the original contract now has an end date immediately before the new contract's start date, with both contracts individually retrievable.

**Acceptance Scenarios**:

1. **Given** a child has an active contract, **When** a director amends it with new terms effective a future date, **Then** the original contract's end date is set to the day before the new terms' start date, its status becomes `ended`, and a new contract is created and activated with the updated terms.
2. **Given** a contract has been amended, **When** anyone views the child's contract history, **Then** both the ended original and the new active contract are visible with their respective periods and terms.
3. **Given** a director amends a contract with the new terms starting the same day the old contract's period would otherwise continue, **When** the amendment is activated, **Then** the day-overlap validator only considers currently active contracts (not the one being ended) and does not block the same-day transition.

---

### User Story 3a - Terminate a contract with no successor (Priority: P2)

A family stops attending a location entirely (they move away, or withdraw from care). A director ends the child's active contract at that location with a final end date, without creating any replacement contract.

**Why this priority**: Distinct from amendment (US3) — a contract must be able to reach a true, final `ended` state that frees up the child's weekdays at that location without implying a continuation is coming. Without this, the child would appear permanently enrolled even after they've left.

**Independent Test**: Can be fully tested by terminating an active contract with an end date, confirming its status becomes `ended`, no new contract is created, and the child's previously contracted weekdays at that location no longer count toward the day-overlap check for future contracts.

**Acceptance Scenarios**:

1. **Given** a child has an active contract at a location, **When** a director terminates it with an end date, **Then** the contract's status becomes `ended`, its `end_date` is set, and no new contract is created.
2. **Given** a contract has been terminated, **When** a director later creates and activates a brand-new contract for the same child at the same or a different location, **Then** the terminated contract's former weekdays no longer block activation (only currently `active` contracts are considered).

---

### User Story 4 - Generate a signable contract PDF (Priority: P2)

A director generates a PDF of an enrolment contract to share with the family. The PDF includes the contracted weekdays and hours, the daily rate, all photo/media consent choices, and a signature line for the family to sign.

**Why this priority**: The PDF is the artifact families actually sign and keep — required for every real enrolment — but it is a downstream output of a contract that must already exist and be correctly modeled, so it follows contract creation and activation.

**Independent Test**: Can be fully tested by generating a PDF for an active contract and confirming the document contains the child's name, location, contracted days/hours, daily rate, each consent type's chosen value, and a signature line.

**Acceptance Scenarios**:

1. **Given** an active contract with specific terms and consent choices, **When** a director requests the contract PDF, **Then** a PDF is produced containing all contracted terms, all five consent choices, and a signature line.
2. **Given** a draft (not yet activated) contract, **When** a director requests the contract PDF, **Then** the PDF is still produced (so it can be reviewed with the family before activation) and clearly indicates the contract is not yet active.

---

### Edge Cases

- Two staff members attempt to activate two different new contracts for the same child that would create a weekday conflict at the same instant — the system MUST guarantee only one succeeds (see US2, Scenario 3).
- A contract's care period ends and a new (amended or freshly created) contract's period begins on the immediately following day — this is a normal transition and MUST NOT be blocked by the overlap validator, which only ever evaluates other currently-`active` contracts.
- A contract is created for a child who has no prior contract at any location (their first enrolment) — creation and activation proceed normally; there is no separate "waiting list" record to update (see Assumptions).
- A director attempts to activate a draft contract whose weekdays overlap with another **draft** (not yet active) contract for the same child — this MUST be allowed, since the validator only checks against active contracts; the conflict is only caught when the second contract itself is activated.
- A contract has an open-ended period (`end_date` is null) and a new contract is later created for the same child at the same location — creating/activating the new one must go through the same single-active-contract-per-location and day-overlap rules as any other contract.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a director to create a draft contract linking a child to a location, specifying a start date (which MAY be in the past, to support recording enrolments that began before the system was adopted), an optional end date, one or more contracted weekdays (Monday–Friday, each weekday appearing at most once) each with its own independent planned start/end time, a daily rate in cents (a whole number greater than zero), and photo/media consent choices. The child and location a contract belongs to are fixed at creation and MUST NOT be changed by a later edit (FR-001a) — a contract enrolled at the wrong child or location must be replaced, not repointed.
- **FR-001a**: System MUST allow a director to edit a `draft` contract's terms (weekdays/hours, daily rate, end date, consent) in place via a full replacement of those terms (the same shape as creation, not a partial patch) — this MUST NOT be possible once the contract is `active` or `ended` (see FR-007/FR-009a for the only ways a non-draft contract's terms change).
- **FR-002**: System MUST support contract statuses of `draft`, `active`, and `ended`, and MUST NOT treat a `draft` contract as binding (it does not participate in enrolment or attendance until activated).
- **FR-003**: System MUST allow a director to activate a `draft` contract, transitioning it to `active`.
- **FR-004**: System MUST reject activation of a contract if the child already has another `active` contract at the same location — checked independently of, and never satisfied by, the FR-005 check below (a child with zero prior contracts trivially passes both).
- **FR-004a**: System MUST reject creation and activation of a contract at a location that is deactivated, reusing the existing `errors.location.not_found` behavior established in features 004/006.
- **FR-005**: System MUST reject activation of a contract if any of its contracted weekdays overlaps with a weekday contracted by any other currently `active` contract for the same child at a **different** location in the same organisation (the split-location day-overlap rule) — this is a distinct check from FR-004, which only ever compares contracts at the *same* location; a single activation attempt MUST pass both independently.
- **FR-006**: System MUST perform the checks in FR-004 and FR-005 atomically at activation time such that concurrent activation attempts for the same child cannot both succeed when they would violate either rule.
- **FR-007**: System MUST allow a director to amend an `active` contract by supplying the **complete** set of new terms (weekdays/hours, daily rate, end date, and consent choices — a full replacement, not a partial patch merged onto the existing contract) and an effective start date for those terms; doing so MUST set the original contract's `end_date` to the day immediately before the new terms' effective start date, transition the original contract's status to `ended`, and create a new contract with the updated terms.
- **FR-008**: System MUST activate the new contract created by an amendment (FR-007) subject to the same validation as FR-004–FR-006, excluding the contract being ended from the overlap check.
- **FR-009**: System MUST preserve every `ended` contract indefinitely as an immutable historical record (full audit trail) — amending or ending a contract MUST NOT delete or overwrite its prior terms.
- **FR-009a**: System MUST allow a director to terminate an `active` contract by setting its end date and transitioning its status to `ended`, without creating any successor contract (distinct from amendment, FR-007). A terminated contract's weekdays MUST NOT count toward the day-overlap check (FR-005) for any future contract activation.
- **FR-010**: System MUST store five independent boolean photo/media consent choices per contract: `photos_internal`, `photos_website`, `photos_social_media`, `video_internal`, `photos_press`. Any consent flag not explicitly provided as `true` on a create, draft-edit, or amend request — including when the entire consent object is omitted — MUST default to `false` (no consent granted); consent is never inferred or defaulted to `true`, since these flags govern photographing/filming minors.
- **FR-011**: System MUST allow generating a PDF document for any contract (draft, active, or ended) that includes the child's name, the location, the contracted weekdays and hours, the daily rate, all five consent choices, the contract's status, and a signature line, rendered in a locale the requesting director selects at generation time (defaulting to Dutch, the primary market, when not specified) — unlike JSON error responses, the PDF is a fixed set of bytes rendered once server-side, so the language cannot be resolved client-side after the fact.
- **FR-012**: System MUST store all monetary amounts (the daily rate) as whole-number cents, never as floating-point values.
- **FR-013**: System MUST include fields on the contract for a future tariff code and a rate-valid-until date, both nullable and unused (not exposed or enforced) in this feature, so that Phase 3 subsidy-rate support can be added without a schema migration.
- **FR-014**: System MUST restrict all contract management operations (create, activate, amend, PDF generation) to director-level staff.
- **FR-015**: System MUST scope every contract to the organisation (tenant) of the director performing the action — a director MUST NOT be able to view, create, or act on a contract belonging to a child or location in a different organisation.
- **FR-016**: System MUST present all user-facing contract-related messages (validation errors, consent labels, PDF text) via i18n keys rather than hardcoded language-specific strings.
- **FR-017**: System MUST allow listing a child's full contract history (all statuses, all locations) ordered most-recent-first.

### Key Entities

- **Contract**: The enrolment agreement between a child and a location. Holds the care period (start date, optional end date), the contracted weekdays each with its own independent planned start/end time, the daily rate in cents, status (`draft`/`active`/`ended`), the five photo/media consent booleans, and the reserved (unused) future tariff fields. Linked to exactly one `Child` and one `Location`. An amendment creates a new `Contract` row linked to the prior one for history traversal; a termination simply ends a contract with no successor row.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can create and activate a new enrolment contract for a child in under 2 minutes.
- **SC-002**: 100% of attempts to activate a contract that would create a same-weekday conflict — whether at the same location or a different one — are rejected, with zero exceptions observed across repeated trials of at least 20 truly simultaneous conflicting activation attempts for the same child.
- **SC-003**: Every contract amendment preserves the complete prior contract unmodified, such that a family's full contract history remains reconstructible at any later time.
- **SC-004**: A generated contract PDF contains 100% of the contract's current terms and consent choices with no manual re-entry by staff.
- **SC-005**: Directors can enrol a child across two locations on non-overlapping days without any workaround or manual conflict-checking outside the system.

## Assumptions

- "Waiting list" in this system is not a separate tracked entity — a child who has not yet been enrolled anywhere is simply a `Child` record with no `active` contract (consistent with feature 006's own assumption that a child file can exist before any enrolment). Contract activation therefore has no separate "waiting list status" to update; the child having an active contract is itself the signal that they are enrolled.
- Contracted hours per weekday are a single planned time range per day (e.g., 8:00–17:00), independently set per weekday; split shifts within the same day are out of scope for this feature.
- A contract's PDF is generated on demand (not stored) and always reflects the contract's current terms at generation time; no versioned PDF archive is required by this feature.
- "Full audit trail" means every historical contract row remains queryable with its original terms — it does not require a separate change-log/event table beyond the chain of ended-and-superseding contracts itself.
- Signature line on the PDF is a blank line/space for a wet-ink signature; digital/electronic signature capture is explicitly out of scope (Phase 2, per the feature description).
- Only director-level staff manage contracts in this feature; caregiver-level staff have no contract permissions (consistent with features 004–006's `DirectorOnly` pattern).
- The organisation's day-of-week convention is Monday–Friday only (KDV care days); weekend contracted days are out of scope.
