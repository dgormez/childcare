---

description: "Task list for feature 009a: Child Events — Custom Type & Growth Check Rename"
---

# Tasks: Child Events — Custom Type & Growth Check Rename

**Input**: Design documents from `/specs/009a-child-events-custom-type/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/child-events-api-delta.md, quickstart.md

**Tests**: Included — constitution Principle V requires TestContainers-backed integration tests for backend changes; existing mobile Jest conventions extended.

**Organization**: Tasks are grouped by user story (US1 = `custom` type, US2 = `growth_check` rename) after a small Foundational phase both stories build on.

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

Existing `backend/` (5-project .NET solution) and `mobile/` (Expo) layout, per plan.md's Project Structure — no new top-level directories.

---

## Phase 1: Setup

No new project setup required — this feature extends feature 009's existing `ChildCare.Domain`/`Application`/`Api`/`Api.Tests` and `mobile/` structure directly. Skipped.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The enum/wire-mapping change both user stories build on top of.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T001 In `backend/ChildCare.Domain/Enums/ChildEventType.cs`: rename the `Measurement` enum member to `GrowthCheck`; add a new `Custom` member.
- [x] T002 In `backend/ChildCare.Domain/Enums/ChildEventTypeExtensions.cs`: remove the `"measurement"` wire-string mapping entirely (both directions); add an explicit `"growth_check"` case (multi-word, needs mapping like `FeedingBottle`/`FeedingSolid` — research.md R3); `Custom` falls through the existing default `ToString().ToLowerInvariant()` case (single word, no special-case entry needed).

**Checkpoint**: Enum compiles; every existing reference to `ChildEventType.Measurement` now fails to compile, surfacing every call site that needs updating in Phase 3/4 below.

---

## Phase 3: User Story 1 - Caregiver logs a one-off observation with its own title (Priority: P1) 🎯 MVP

**Goal**: A caregiver can record a `custom` event (`{ label, text? }`, label required) via the existing quick-entry sheet, and it renders on the timeline with `label` as its headline.

**Independent Test**: Log in as a caregiver, open a child's quick-action sheet, select "Custom", enter a label (and optionally detail text), save, and confirm the event appears on the timeline with the label as its headline — independently of US2.

### Tests for User Story 1

- [x] T003 [P] [US1] Add `Custom` payload validation test cases to `backend/ChildCare.Api.Tests/ChildEvents/ChildEventPayloadValidationTests.cs`: rejects missing/empty/whitespace-only `label`, rejects `label` over 100 characters, rejects unexpected fields, accepts label-only, accepts label+text.
- [x] T004 [P] [US1] Add an integration test (new or existing `ChildEvents` integration test file under `backend/ChildCare.Api.Tests/ChildEvents/`) asserting `POST /api/child-events` with `eventType: "custom"` returns `201` and round-trips `payload.label`/`payload.text` correctly.
- [x] T005 [P] [US1] Add a mobile offline-queue test (existing conventions, e.g. `mobile/services/__tests__/childEvents.test.ts` or the sync-engine's existing test file) asserting a `custom` event recorded while offline queues and syncs exactly like any other `child_event` (no type-specific branching) — closes the gap where FR-005's "identical to every other type" claim for offline sync was otherwise only exercised implicitly.
- [x] T006 [P] [US1] Add a Jest test in `mobile/components/__tests__/` (existing conventions) asserting `QuickActionSheet` renders a "Custom" free-text entry point and blocks submission with no label entered.
- [x] T007 [P] [US1] Add a Jest test asserting `EventTimeline` renders a `custom` event's `payload.label` as its headline and `payload.text` (if present) as secondary detail.
- [x] T008 [P] [US1] Add a Jest test asserting `EditEventModal` can edit an existing same-day `custom` event's `label`/`text`, following the same test pattern already used for `note`/`activity` in that file.

### Implementation for User Story 1

- [x] T009 [US1] In `backend/ChildCare.Application/ChildEvents/ChildEventPayloadValidator.cs`: add `ChildEventType.Custom => ["label", "text"]` to `AllowedFields`, and a new `case ChildEventType.Custom` switch arm calling `RequireString(payload, "label", failures)` plus a new length-bound check (max 100 chars) on `label`; `text` needs no required-field call (optional, matches `Medication.reason`'s optional-field pattern).
- [x] T010 [US1] In `mobile/types/index.ts`: add `"custom"` to the closed `ChildEventType` TS union.
- [x] T011 [US1] In `mobile/components/QuickActionSheet.tsx`: add a "Custom" entry to the free-text bucket alongside `activity`/`note` (research.md R4) — a label input (required) plus an optional text input, submitting `{ label, text }` as the payload.
- [x] T012 [US1] In `mobile/components/EventTimeline.tsx`: add a `custom` render case showing `payload.label` as the headline and `payload.text` (if present) as detail text beneath it.
- [x] T013 [P] [US1] Add `custom` i18n keys (label prompt, placeholder, quick-action entry name) to `mobile/i18n/locales/nl.json`, `fr.json`, and `en.json`.
- [x] T014 [US1] In `mobile/components/EditEventModal.tsx`: add `custom` to the same-day edit form so an existing `custom` event's `label`/`text` can be edited, following the existing per-type pattern already used for `note`/`activity`.

**Checkpoint**: User Story 1 is fully functional and independently testable — a caregiver can record and see `custom` events end-to-end, with no dependency on US2.

---

## Phase 4: User Story 2 - Existing `measurement` events keep working under their new name (Priority: P2)

**Goal**: Every existing `measurement` row reads back as `growth_check` with unchanged data after a one-time migration; new writes use `growth_check` only, and `measurement` is rejected as a wire value going forward.

**Independent Test**: Seed a `measurement` row (or use one recorded before this feature's migration), run the `backfill-growth-check` CLI command, and confirm the row reads back as `growth_check` with unchanged weight/height/head-circumference values — independently of US1.

### Tests for User Story 2

- [x] T015 [P] [US2] Update `backend/ChildCare.Api.Tests/ChildEvents/ChildEventPayloadValidationTests.cs`'s existing `measurement` test fixtures to `growth_check` (same assertions, renamed wire string only — data-model.md's "byte-for-byte same rule" requirement).
- [x] T016 [P] [US2] Add a new test file `backend/ChildCare.Api.Tests/ChildEvents/BackfillGrowthCheckCommandTests.cs` (TestContainers PostgreSQL, constitution Principle V): seeds `measurement` rows across 2+ tenant schemas, runs the command, asserts every row now reads `growth_check` with unchanged payload values, and asserts a schema with zero `measurement` rows completes as a no-op.
- [x] T017 [US2] Add an integration test asserting `POST /api/child-events` with the literal `eventType: "measurement"` returns `400 errors.child_events.invalid_event_type` (FR-008) — depends on T001/T002 already landing.

### Implementation for User Story 2

- [x] T018 [US2] Create `backend/ChildCare.Api/Cli/BackfillGrowthCheckCommand.cs`, mirroring `MigrateTenantsCommand.cs`'s tenant-loop structure: loop every `Ready` tenant from `PublicDbContext.Tenants`, run `UPDATE "<schema>".child_events SET "EventType" = 'growth_check' WHERE "EventType" = 'measurement'` via `PublicDbContext.Database.ExecuteSqlRawAsync` (schema name interpolated from the trusted `Tenants` table, never from request input — correction made during implementation: `ITenantDbContextResolver` has no raw-SQL member, so `PublicDbContext`'s own connection is used directly, matching `TenantMigrationRolloutTests.cs`'s existing pattern), print a per-tenant row-count line and a final summary, return a non-zero exit code if any tenant fails (research.md R1).
- [x] T019 [US2] In `backend/ChildCare.Api/Program.cs`: add a `backfill-growth-check` CLI dispatch branch before the web host builds, mirroring the existing `migrate-tenants` branch (contracts/child-events-api-delta.md).
- [x] T020 [P] [US2] In `mobile/types/index.ts`: rename `"measurement"` to `"growth_check"` in the closed `ChildEventType` union.
- [x] T021 [US2] In `mobile/components/QuickActionSheet.tsx` and `mobile/components/EditEventModal.tsx`: rename all `measurement` references to `growth_check`.
- [x] T022 [P] [US2] Rename the `measurement` i18n keys to `growth_check` (same translated display text otherwise, just the key and any visible "Measurement" label updated to read as a growth check) in `mobile/i18n/locales/nl.json`, `fr.json`, and `en.json`.

**Checkpoint**: Both user stories now work independently — `custom` events record correctly, and `growth_check` fully replaces `measurement` with no data loss.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T023 Update the constitution's Development Workflow & Phase Discipline section (`.specify/memory/constitution.md`) to list `growth_check`/`custom` instead of `measurement` in its `child_events` event-type description — a PATCH-level documentation-accuracy fix (plan.md's Constitution Check note), not a principle change.
- [x] T024 Run `quickstart.md` end-to-end: migration verification steps, backend curl validation, and mobile manual validation.
- [x] T025 Grep the full `backend/` and `mobile/` trees (excluding generated/build output) for any remaining literal `"measurement"`/`Measurement` reference and confirm none remain outside historical spec/plan documentation (spec.md Assumptions, SC-004).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: No dependencies — start immediately. BLOCKS both user stories (both need the updated enum to compile against).
- **User Story 1 (Phase 3)**: Depends on Phase 2. No dependency on US2 — independently testable and shippable on its own.
- **User Story 2 (Phase 4)**: Depends on Phase 2. No dependency on US1 — independently testable and shippable on its own.
- **Polish (Phase 5)**: Depends on both user stories being complete (T025's repo-wide grep needs both renames/additions done).

### Within Each User Story

- Tests before implementation (T003-T008 before T009-T014; T015-T017 before T018-T022).
- Backend validator/enum changes before mobile changes that assume the new wire values exist.

### Parallel Opportunities

- T003-T008 (US1 tests, different files) can run in parallel.
- T015-T016 (US2 tests, different files) can run in parallel.
- T013 and T020/T022 (i18n/type-union files, different files from their story's other tasks) can run in parallel with their story's non-i18n implementation tasks.
- US1 (Phase 3) and US2 (Phase 4) can be implemented in parallel by different developers once Phase 2 is complete — they touch almost entirely disjoint file sets except `mobile/components/EditEventModal.tsx`/`QuickActionSheet.tsx` and the i18n locale files, where both stories add/rename distinct keys/cases (coordinate to avoid a merge conflict, not a functional dependency).

---

## Parallel Example: Phase 2 → both stories

```bash
# After T001/T002 land:
Task: "US1 — Custom payload validator + mobile quick-entry + timeline render"
Task: "US2 — growth_check backfill CLI + test-fixture rename + mobile rename"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational.
2. Complete Phase 3: User Story 1 (`custom` type).
3. **STOP and VALIDATE**: Record a `custom` event end-to-end per quickstart.md's mobile validation steps.
4. Note: US2's rename is not optional follow-on scope — it's committed in this feature's spec (bundled per the 2026-07-09 product decision) — but Phase 3 alone is a coherent, demoable increment if sequencing pressure requires it.

### Incremental Delivery

1. Foundational → both stories' enum dependency ready.
2. US1 → validate independently → `custom` events work end-to-end.
3. US2 → run `backfill-growth-check` against a dev tenant → validate independently → `growth_check` fully replaces `measurement`.
4. Polish → constitution doc update, full quickstart pass, repo-wide `measurement` grep.

---

## Notes

- [P] tasks touch different files with no dependency on an incomplete task.
- Commit after each task or logical group, per this repo's existing convention.
- The `backfill-growth-check` CLI command (T018/T019) MUST be run against every tenant schema before deploying the build containing T001/T002's enum change to production (research.md R2) — call this out explicitly in the PR description as a deploy-order requirement, not just in this tasks file.

## Phase 6: Convergence

- [x] T026 Remove the now-unreferenced `childEvents.note.text` i18n key from `mobile/i18n/locales/nl.json`, `fr.json`, and `en.json` per FR-010 (unrequested) — left orphaned after `EditEventModal.tsx`'s shared `text` field label was repointed to `fieldLabels.text` during T014's implementation.
