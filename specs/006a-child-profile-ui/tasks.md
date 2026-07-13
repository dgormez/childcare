---

description: "Task list for feature 006a-child-profile-ui"
---

# Tasks: Child Profile UI

**Input**: Design documents from `/specs/006a-child-profile-ui/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — Constitution Principle V requires integration tests against
TestContainers PostgreSQL for the happy path plus key negative flows; this repo's convention
(CLAUDE.md) also expects web/mobile component tests for new UI.

**Organization**: Tasks are grouped by user story (spec.md) to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US3)

## Path Conventions

Existing monorepo: `backend/ChildCare.*`, `web/`, `mobile/` (see plan.md's Project Structure).

---

## Phase 1: Setup

**Purpose**: One new dependency; nothing else to initialize (extends existing five-project
backend and existing `web`/`mobile` apps, Constitution Principle VII).

- [X] T001 [P] Add `@radix-ui/react-tabs` to `web/package.json` dependencies and run `npm install` in `web/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: `PediatricianName`/`PediatricianPhone` end-to-end on the backend, plus the shared
`Tabs` UI primitive — every user story depends on both.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Add `PediatricianName` (`string?`) and `PediatricianPhone` (`string?`) properties to `Child` in `backend/ChildCare.Domain/Entities/Child.cs`, positioned immediately after `GpPhone` (mirrors that field pair exactly)
- [X] T003 [P] Add `PediatricianName`/`PediatricianPhone` parameters to `CreateChildCommand` in `backend/ChildCare.Application/Children/CreateChildCommand.cs`, positioned after `GpPhone`
- [X] T004 [P] Add `PediatricianName`/`PediatricianPhone` parameters to `UpdateChildCommand` in `backend/ChildCare.Application/Children/UpdateChildCommand.cs`, positioned after `GpPhone`
- [X] T005 Add `RuleFor(x => x.PediatricianName).MaximumLength(200).WithMessage("errors.child.pediatrician_name_too_long")` and the equivalent `PediatricianPhone` rule (`MaximumLength(30)`, `errors.child.pediatrician_phone_too_long`) to `backend/ChildCare.Application/Children/CreateChildCommandValidator.cs` (depends on T003)
- [X] T006 Add the same two validation rules to `backend/ChildCare.Application/Children/UpdateChildCommandValidator.cs` (depends on T004)
- [X] T007 [P] Add `PediatricianName`/`PediatricianPhone` fields to both `CreateChildRequest` and `UpdateChildRequest` in `backend/ChildCare.Contracts/Requests/ChildRequests.cs`, positioned after `GpPhone`
- [X] T008 [P] Add `PediatricianName`/`PediatricianPhone` fields to `ChildResponse` in `backend/ChildCare.Contracts/Responses/ChildResponse.cs`, positioned after `GpPhone`
- [X] T009 Map `PediatricianName`/`PediatricianPhone` in `backend/ChildCare.Application/Children/CreateChildCommandHandler.cs` (entity assignment, mirrors line ~23's `GpName = request.GpName`) and `backend/ChildCare.Application/Children/UpdateChildCommandHandler.cs` (mirrors line ~25's `child.GpName = request.GpName;`), plus `backend/ChildCare.Application/Children/ChildMapper.cs` (mirrors line ~20's `c.GpName,` in the response projection) (depends on T002, T003, T004, T008)
- [X] T010 Wire `req.PediatricianName`/`req.PediatricianPhone` from `CreateChildRequest`/`UpdateChildRequest` into the `CreateChildCommand`/`UpdateChildCommand` construction in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs`'s `POST /` and `PUT /{id:guid}` handlers (depends on T007, T009)
- [X] T011 Add `c.Property(x => x.PediatricianName).HasMaxLength(200);` and `c.Property(x => x.PediatricianPhone).HasMaxLength(30);` to the `Child` entity configuration in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (~lines 285-311, alongside the existing `GpName`/`GpPhone` config) (depends on T002)
- [X] T012 Generate the EF Core migration `AddPediatricianContactToChild` (additive nullable columns on `children`) and its SQL script in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`, per this repo's manual-apply convention (depends on T011)
- [X] T013 [P] Create the shadcn/ui `Tabs` primitive in `web/components/ui/tabs.tsx` (Radix `@radix-ui/react-tabs`, styled per `design-system.md`'s existing component conventions — no new visual language) (depends on T001)
- [X] T014 Regenerate `web/lib/generated/api-types.ts` via `npm run generate-api-client` against the local backend running with the new migration applied (depends on T010, T012)
- [X] T015 [P] Regenerate `mobile/services/generated/api-types.ts` via `npm run generate-api-client` against the same backend (depends on T010, T012)

**Checkpoint**: Backend supports `PediatricianName`/`PediatricianPhone` end-to-end (entity →
command → validator → contract → endpoint → migration); the `Tabs` primitive exists; both
generated API clients include the new fields. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director creates a new child profile (Priority: P1) 🎯 MVP

**Goal**: A director can create a child record from `/children` (required fields only block
save) and lands on that child's new "Profiel" tab, which displays every field.

**Independent Test**: Create a child with only First name/Last name/Date of birth — confirm it
saves and appears in the child list. Create a second child populating every optional field
(including both GP and pediatrician contacts) — confirm all fields persist and display on the
resulting Profiel tab.

### Tests for User Story 1

- [X] T016 [P] [US1] Integration test: `POST /api/children` with only required fields succeeds (201, record persists); with every optional field including `PediatricianName`/`PediatricianPhone` populated, all persist and round-trip on `GET /api/children/{id}`, in `backend/ChildCare.Api.Tests/ChildCrudTests.cs`
- [X] T017 [P] [US1] Integration test: `POST /api/children` missing `FirstName`, `LastName`, or `DateOfBirth` returns `400` with the existing `errors.child.*_required` keys and writes nothing, in `backend/ChildCare.Api.Tests/ChildCrudTests.cs`
- [X] T018 [P] [US1] Integration test: `POST /api/children` with `PediatricianName` over 200 chars or `PediatricianPhone` over 30 chars returns `400` with `errors.child.pediatrician_name_too_long`/`errors.child.pediatrician_phone_too_long`, in `backend/ChildCare.Api.Tests/ChildCrudTests.cs`
- [X] T019 [P] [US1] Component test: `ChildFormDialog` (create mode) submits successfully with required-only fields, submits successfully with every field populated, and shows inline validation without submitting when a required field is empty, in `web/__tests__/children.test.tsx`

### Implementation for User Story 1

- [X] T020 [US1] Create `web/components/children/ChildFormDialog.tsx` — a Radix `Dialog` form (mirrors `web/components/InviteParentDialog.tsx`'s `useState`-based pattern, no react-hook-form) covering first/last name, date of birth, gender, nationality, allergies description + severity, medical conditions, dietary restrictions, GP name/phone, pediatrician name/phone, health insurance number, kindcode; supports a `mode: "create" | "edit"` prop (edit mode wired in US2) and calls `apiClient.POST`/`apiClient.PUT` accordingly, mapping `errorKey` to translated inline messages (depends on T014)
- [X] T021 [US1] Create `web/components/children/ChildProfileTab.tsx` — read-only display of every field listed in T020 for a given `ChildResponse` (depends on T014)
- [X] T022 [US1] Restructure `web/app/(app)/children/[id]/page.tsx` to use the `Tabs` primitive (T013) with two tabs, "Profiel" (renders `ChildProfileTab`, T021) and "Gezondheid" (the existing vaccine/health-record content from this file, moved under the tab unchanged — no behavior change to 013c's functionality), defaulting to the "Profiel" tab (depends on T013, T021)
- [X] T023 [US1] Add a "New child" button to `web/app/(app)/children/page.tsx` that opens `ChildFormDialog` in create mode; on successful save, `router.push` to `/children/{id}` (FR-003) (depends on T020)
- [X] T024 [P] [US1] Add NL/FR/EN i18n keys for the create form and Profiel tab (`children.form.*`, `children.profile.*`, `children.newChild`) to `web/i18n/locales/{en,fr,nl}.json`, following the existing `children.*`/`children.health.*` nesting convention (depends on T020, T021, T022, T023)
- [X] T024a [P] [US1] Component test: switching between "Profiel" and "Gezondheid" tabs updates the visible content without a route change/navigation event (SC-005), in `web/__tests__/children.test.tsx` (depends on T022)

**Checkpoint**: A director can create a child from zero via the web UI and see it on a working
"Profiel" tab. This is independently demoable as the MVP.

---

## Phase 4: User Story 2 - Director edits a child's general profile and medical contacts (Priority: P1)

**Goal**: A director can edit any field on an existing child's "Profiel" tab, including setting,
changing, or clearing the pediatrician contact independently of the GP contact.

**Independent Test**: Open an existing child's Profiel tab, edit several fields (including
setting a pediatrician contact where none existed), save, and confirm the changes persist on
reload without affecting the unrelated GP contact.

### Tests for User Story 2

- [X] T025 [P] [US2] Integration test: `PUT /api/children/{id}` updates `PediatricianName`/`PediatricianPhone` without altering `GpName`/`GpPhone`, and clearing the pediatrician fields (setting both to `null`) succeeds and persists as cleared, in `backend/ChildCare.Api.Tests/ChildCrudTests.cs`
- [X] T026 [P] [US2] Integration test: `PUT /api/children/{id}` clearing a required field (`FirstName`/`LastName`/`DateOfBirth`) returns `400` and does not save, in `backend/ChildCare.Api.Tests/ChildCrudTests.cs`
- [X] T027 [P] [US2] Component test: `ChildFormDialog` (edit mode) pre-fills existing values, saves an edited pediatrician contact via `PUT`, and independently clears it on a subsequent edit, in `web/__tests__/children.test.tsx`

### Implementation for User Story 2

- [X] T028 [US2] Add an "Edit" action on `ChildProfileTab` (T021) that opens `ChildFormDialog` (T020) in edit mode, pre-filled from the current `ChildResponse`; on successful save, refresh the tab's data (depends on T020, T021)

**Checkpoint**: Directors can now both create and edit every profile/medical-contact field
end-to-end through the web UI.

---

## Phase 5: User Story 3 - Caregiver views a child's GP and pediatrician contact (Priority: P2)

**Goal**: The caregiver-facing child screen shows both GP and pediatrician contact, sourced from
the existing cached `ChildResponse` (research.md R3/R4) — no new offline mechanism.

**Independent Test**: View the caregiver child screen for a child with both contacts set, and
separately for a child with only one or neither set; put the device offline after a prior load
and confirm the cached values still render.

### Tests for User Story 3

- [X] T029 [P] [US3] Component test: the child screen renders both GP and pediatrician contact when both are present, renders only the populated one when the other is absent, and renders neither block (no error/placeholder) when both are absent, in `mobile/__tests__/screens/child-detail.test.tsx`
- [X] T030 [P] [US3] Component test: with the device offline and a previously cached `ChildResponse` (via `CHILDREN_CACHE_KEY`) containing GP/pediatrician values, the child screen still renders them from cache, in `mobile/__tests__/screens/child-detail.test.tsx`

### Implementation for User Story 3

- [X] T031 [US3] Add a GP + pediatrician contact block to `mobile/app/(app)/child/[id].tsx` (~lines 160-235), following the existing card pattern (`{!!child.<field> && (<View className="bg-surface dark:bg-surface-dark rounded-xl p-4 mb-3">...)}`) used by the allergy/medical/dietary blocks immediately above it; render GP and pediatrician as visually distinct labeled rows, each independently optional (depends on T015)
- [X] T032 [P] [US3] Add NL/FR/EN i18n keys (`child.gpName`, `child.gpPhone`, `child.pediatricianName`, `child.pediatricianPhone`) to `mobile/i18n/locales/{en,fr,nl}.json`, nested under the existing `child.*` namespace (depends on T031)

**Checkpoint**: All three user stories are independently functional — create, edit, and
caregiver read access all work end-to-end.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validation against the spec's non-story-specific requirements.

- [X] T033 [P] Verify every new string introduced in T024/T032 resolves in all three locales (NL/FR/EN) with no fallback/missing-key warnings (Constitution IV)
- [X] T034 Run `quickstart.md`'s four scenarios end-to-end manually against the running stack
- [X] T035 [P] Confirm `PUT /api/children/{id}` full-record-replace semantics are preserved for the two new fields (an edit that omits `pediatricianName` in its request body clears it, consistent with `UpdateChildRequest`'s existing full-replace contract) — add a regression note or test if behavior is surprising

---

## Phase 7: Convergence

- [X] T036 Add a profile-photo display + upload control to `web/components/children/ChildProfileTab.tsx`, wired to `POST /api/children/{id}/photo/upload-url` then a direct signed-URL `PUT` (mirrors `web/components/health/HealthRecordAttachmentControl.tsx`'s pattern), reloading the child on success per FR-002/FR-004 (missing)
- [X] T037 Add a web component test in `web/__tests__/children.test.tsx`: editing an existing child and clearing a required field (e.g. first name) shows inline validation and does not call `apiClient.PUT`, per US2 Acceptance Scenario 5 (partial)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001 for T013) — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion. No dependency on US2/US3.
- **User Story 2 (Phase 4)**: Depends on Foundational completion **and** US1's `ChildFormDialog`/`ChildProfileTab` (T020, T021) — extends them rather than duplicating, so it is not independently buildable before US1, but is independently *testable* once both exist.
- **User Story 3 (Phase 5)**: Depends on Foundational completion only (T015) — fully independent of US1/US2, could be built in parallel with either.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- T002-T008 (Phase 2) are all `[P]` — different files, no interdependency until T009.
- T013 (`Tabs` primitive) can proceed in parallel with T002-T012 (backend track) — different
  stack entirely.
- Once Phase 2 completes, **User Story 3 (Phase 5)** can be built fully in parallel with **User
  Story 1 (Phase 3)** — different platform (mobile vs. web), no shared files.
- Within US1, T016-T019 (tests) are `[P]`; T020/T021 can proceed in parallel (different files)
  before T022/T023 depend on them.

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Backend field additions — different files, launch together:
Task: "Add PediatricianName/PediatricianPhone to Child.cs"
Task: "Add PediatricianName/PediatricianPhone params to CreateChildCommand.cs"
Task: "Add PediatricianName/PediatricianPhone params to UpdateChildCommand.cs"
Task: "Add PediatricianName/PediatricianPhone fields to ChildRequests.cs"
Task: "Add PediatricianName/PediatricianPhone fields to ChildResponse.cs"

# Independent frontend primitive — launch alongside the backend track:
Task: "Create web/components/ui/tabs.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (backend field + Tabs primitive — blocks everything).
3. Complete Phase 3: User Story 1 (create + Profiel tab display).
4. **STOP and VALIDATE**: a director can create a child and see every field on its Profiel tab.
5. Demo if ready — this alone closes the "no UI path to create a child" gap that motivated this
   feature.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. User Story 1 → create + view (MVP).
3. User Story 2 → edit, layered on US1's form component.
4. User Story 3 → caregiver read access, independent of US1/US2, buildable in parallel with
   either.
5. Polish → i18n/quickstart verification across all three.
