# Requirements Quality Checklist: Incident Reports

**Purpose**: Validate the quality, clarity, and completeness of the incident-reports spec/plan/
tasks before implementation — the standard checklist this project runs for every feature.
**Created**: 2026-07-12
**Feature**: [spec.md](../spec.md)

**Resolution pass**: every item below was checked against spec.md/plan.md/tasks.md/data-model.md/
contracts and either confirmed already sufficient or fixed directly — no item is left as deferred
debt, per this project's standing checklist-resolution rule.

## Requirement Completeness

- [x] CHK001 Are the full set of fields a caregiver can enter on the report form documented, including which are required vs. optional? [Completeness, Spec §FR-001/FR-002] — already complete, no change needed.
- [x] CHK002 Are requirements defined for what happens if `injuryType` is `none` but `firstAidGiven`/`doctorCalled` are also filled in (an incident with no injury but still some response)? [Gap] — fixed: added Story 1 Acceptance Scenario 5.
- [x] CHK003 Are requirements defined for who may view an individual incident report's detail via the caregiver-tablet path (own report only vs. any report at that location)? [Gap, Spec §Assumptions] — fixed: added FR-018, updated contracts/incident-reports-api.md's GET auth line.
- [x] CHK004 Is pagination behavior for the cross-KDV list (`GET /api/incident-reports`) specified (default page size, sort stability across pages)? [Gap, Spec §FR-009] — fixed: FR-009 now specifies default page size 25 and secondary sort by id.

## Requirement Clarity

- [x] CHK005 Is "immutable after 24 hours" unambiguous about which timestamp starts the clock — `createdAt` vs. `occurredAt`? [Clarity, Spec §FR-005] — already clear (Edge Cases + FR-005 both specify `created_at`), no change needed.
- [x] CHK006 Is "any director" in FR-007 (who may edit within the 24h window) unambiguous, e.g. any director in the organisation vs. only a director at the report's location? [Ambiguity, Spec §FR-007] — fixed: FR-007 reworded to state "any director in the organisation."
- [x] CHK007 Is the caregiver-tablet "own report" edit scope in FR-007 clearly bounded — does "reporting caregiver" mean any of the (possibly multiple) `reportedBy` entries, or the specific individual? [Ambiguity, Spec §FR-004/FR-007] — fixed: FR-007 clarified this is device-scoped, not author-matched (no individual write-identity check exists).
- [x] CHK008 Is "under one minute" (SC-001) tied to a specific, testable task definition (e.g., description + injury-type selection only, or the full optional field set)? [Measurability, Spec §SC-001] — fixed: SC-001 now scopes to description+injury-type only, measured from tap to confirmed submission.

## Requirement Consistency

- [x] CHK009 Do FR-004/FR-004a's `reported_by`-resolution description and the Key Entities section's description of `IncidentReport` agree on `reportedBy` being an array, not a single value? [Consistency, Spec §FR-004/Key Entities] — confirmed consistent (both already say "zero or more").
- [x] CHK010 Does the Edge Cases section's occurred-at/created-at discrepancy-visibility requirement align with FR-003's backdating requirement (no contradiction between "always shown together" and only being visible in the detail view)? [Consistency, Spec §Edge Cases/FR-003] — fixed: Edge Cases now scopes "shown together" to the detail view explicitly, matching the planned list-view column set.
- [x] CHK011 Do the plan.md endpoint list and contracts/incident-reports-api.md agree on exactly which failure codes (400/403/404/409) map to which validation failure? [Consistency, Plan/Contracts] — confirmed no conflict; plan.md intentionally doesn't restate contract-level detail.

## Acceptance Criteria Quality

- [x] CHK012 Can SC-002 ("100% of incident reports remain retrievable and unaltered... more than 24 hours after filing") be objectively verified without ambiguity about which fields count as "altered" (i.e., excluding `follow_up`)? [Measurability, Spec §SC-002] — already clear ("aside from follow_up" is explicit), no change needed.
- [x] CHK013 Is SC-004's "in under 30 seconds" grounded in a specific starting/ending action (opening the Incidents screen → PDF download beginning), so it's testable rather than subjective? [Measurability, Spec §SC-004] — fixed: SC-004 reworded with explicit start/end points and a "findable via a known filter" precondition.
- [x] CHK014 Is SC-005 ("100% of incident reports remain linked... after deactivation") paired with a concrete acceptance scenario demonstrating retrievability via the child filter specifically, not just non-deletion at the database level? [Traceability, Spec §SC-005/FR-008] — fixed: added Story 2 Acceptance Scenario 5.

## Scenario Coverage

- [x] CHK015 Are requirements defined for a director attempting to export a PDF for an incident report that has zero optional fields filled in (minimal record)? [Edge Case, Gap] — fixed: added Edge Case + FR-012 clause requiring successful rendering with blank/omitted optional fields.
- [x] CHK016 Are requirements defined for concurrent edits — a caregiver and a director both editing the same report within the 24h window at the same time? [Gap, Exception Flow] — fixed: added Edge Case specifying last-write-wins, consistent with feature 013f's precedent for low-frequency administrative edits.
- [x] CHK017 Are requirements defined for what the caregiver tablet displays if `POST /api/incident-reports` fails for a reason other than validation (e.g. 5xx) while online — is it treated as a queueable offline-style failure or a hard error? [Gap, Exception Flow] — fixed: added Edge Case + FR-014 clause; a non-network failure is a normal retryable error, never silently queued.
- [x] CHK018 Is the interaction between the offline queue's replay order and the 24-hour immutability window addressed for a report that syncs more than 24 hours after its `occurred_at`/local creation? [Coverage, Spec §Edge Cases] — already resolved (clock starts at server `created_at`, not local time), no change needed.
- [x] CHK019 Are requirements defined for a director filtering the Incidents screen by a location the organisation has since deactivated (feature 004)? [Gap, Edge Case] — fixed: added Edge Case; deactivated locations remain selectable and their history remains reachable.

## Non-Functional Requirements

- [x] CHK020 Are the two new indexes (FR-017) tied to a concrete scale assumption (e.g. "responsive at N years / M reports per location") rather than an unquantified "remains responsive"? [Clarity, Spec §FR-017] — fixed: FR-017 now names "thousands of reports per location over several years" as the target scale.
- [x] CHK021 Are accessibility requirements (keyboard navigation, contrast, no color-only signaling for the reviewed/unreviewed indicator) explicitly stated for the director-web Incidents screen? [Completeness, Spec §UX Requirements] — fixed: FR-010 now explicitly requires an icon+color pairing for the reviewed/unreviewed indicator.
- [x] CHK022 Are i18n requirements (FR-016) explicit that PDF export text is also locale-covered, not just on-screen UI strings? [Clarity, Spec §FR-016/FR-012] — fixed: FR-016 now explicitly names the PDF export's labels and the `?locale=` convention.

## Dependencies & Assumptions

- [x] CHK023 Is the dependency on `IShiftAttributionService` (feature 009) validated as actually reusable for this feature's `locationId`/`groupId` resolution path (caregiver-tablet incident filing, not a `child_events` context)? [Assumption, research.md R1] — validated during planning (Explore agent confirmed exact method signature/query shape is context-agnostic), no change needed.
- [x] CHK024 Is the assumption that `Location.Dossiernummer` substitutes for "erkenningsnummer" clearly flagged as a premise correction rather than left implicit in the PDF requirement text? [Traceability, Spec §Assumptions] — already explicit, no change needed.
- [x] CHK025 Is the assumption that no director push-notification channel exists validated against the current codebase state (not just a prior feature's memory note) before this spec relies on it? [Assumption, Spec §Assumptions] — validated during specification via direct code check (Explore agent re-confirmed `TenantUser`/`Notification` state against current code, not just 013f's shipped-notes), no change needed.

## Ambiguities & Conflicts

- [x] CHK026 Is there any remaining ambiguity about whether `LocationId` on `IncidentReport` (added in data-model.md, not in the original BACKLOG schema) needs its own functional requirement in spec.md, or is it sufficiently covered by FR-009's location-filter requirement? [Gap, data-model.md vs. spec.md] — fixed: added FR-019 explicitly requiring `location_id` capture, and updated Key Entities to name it.
- [x] CHK027 Is a requirement & acceptance-criteria ID scheme consistently applied across spec.md (FR-/SC-) and cross-referenced correctly in plan.md/tasks.md? [Traceability] — confirmed consistent, no change needed.

## Notes

- All 27 items resolved during this pass — 12 required a spec/contract/task edit, 15 were
  confirmed already sufficient. See the resolution note on each item above.
