---

description: "Task list for feature implementation"
---

# Tasks: Web Admin Scaffold

**Input**: Design documents from `/specs/007a-web-admin-scaffold/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires TestContainers-backed
xUnit tests for the two small backend additions; Vitest (already configured in `web/`) covers
the frontend. Scope follows project convention (global CLAUDE.md): happy path + key negative
flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 login + shell, US2 P2 staff
management, US3 P3 device management). The API client migration, auth session plumbing, the two
backend additions needed for the shell (director/org name exposure), i18n wiring, design tokens,
and shared UI primitives (confirm dialog / empty state / error state) are prerequisites every
story depends on, so they live in Foundational. Habits-template removal is Setup, mirroring
feature 008's placement.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1/US2/US3) — omitted for
  Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

This feature touches both `web/` (Next.js/TypeScript) and `backend/` (C#/.NET) — the first
feature to build real screens in `web/` (007a follows 008's mobile-scaffold shape).

---

## Phase 1: Setup

**Purpose**: Remove the Habits-template surface and put base tooling (i18n, design tokens,
shadcn) in place before rebuilding on top.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds and `cd web && npm test` passes
  on this branch before starting
- [X] T002 Delete `web/app/(app)/habits/`, `web/app/(app)/subscription/page.tsx`,
  `web/app/(app)/settings/page.tsx`, `web/app/(auth)/register/page.tsx`,
  `web/app/(auth)/forgot-password/page.tsx`, `web/app/(auth)/reset-password/page.tsx`,
  `web/app/verify-email/page.tsx`, `web/app/payment-success/page.tsx`, `web/lib/api.ts`
  (superseded by T007), and their Habits-only entries in `web/lib/types.ts`
  (`Habit`/`HabitCompletion`/`SubscriptionStatus`) and any corresponding tests under
  `web/__tests__/` — director-invite/password-reset flows are feature 003 backend capability
  with no web UI yet; out of this feature's scope to rebuild, only to stop referencing removed
  Habits pages from them
- [X] T003 [P] Install `next-intl` in `web/package.json`; create `web/i18n/locales/{nl,fr,en}.json`
  (empty top-level objects) and the `next-intl` request-config/provider wiring in
  `web/app/layout.tsx`, mirroring `mobile/i18n`'s locale-file structure (flat namespaced keys)
- [X] T004 [P] Install `openapi-typescript` and `openapi-fetch` in `web/package.json`; add a
  `generate-api-client` script to `web/package.json` matching `mobile/package.json`'s pattern
  (`openapi-typescript ${NEXT_PUBLIC_API_BASE_URL:-http://localhost:5000}/openapi/v1.json -o
  lib/generated/api-types.ts`); commit the generated file (matches
  `mobile/services/generated/api-types.ts`'s precedent — CI never runs a live backend to
  regenerate it, so it must not be gitignored)
- [X] T005 [P] Create `web/theme/colors.ts` porting `mobile/theme/colors.js`'s `light`/`dark`
  token objects to TypeScript (research.md R6); update `web/tailwind.config.ts` to consume it
  with the same kebab-case/`-dark`-suffix flattening `mobile/tailwind.config.js` uses
- [X] T006 [P] Initialize shadcn/ui in `web/` (`components.json`, `web/components/ui/`) and add
  the `table`, `button`, `input`, `dialog`, `badge`, `dropdown-menu` primitives, restyled to
  consume `theme/colors.ts` tokens rather than shadcn's default palette (research.md R7)

**Checkpoint**: Habits template gone, base tooling in place.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The typed API client, auth session plumbing, the two minimal backend additions
needed to render the shell (FR-005a), and shared UI primitives every story reuses.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend: director & organisation name exposure (research.md R4, contracts/auth-name-exposure-api.md)

- [X] T007 [P] Add `string Name` to `AuthenticatedUser` in
  `backend/ChildCare.Contracts/Responses/AuthSessionResponse.cs`
- [X] T008 Populate `Name` from `TenantUser.Name` in
  `backend/ChildCare.Application/Auth/LoginCommandHandler.cs`,
  `RefreshTokenCommandHandler.cs`, `GoogleSignInCommandHandler.cs`, and
  `AppleSignInCommandHandler.cs` — depends on T007
- [X] T009 [P] Create `OrganisationResponse(string Name)` in
  `backend/ChildCare.Contracts/Responses/OrganisationResponse.cs`
- [X] T010 Create `GetCurrentOrganisationQuery`/`GetCurrentOrganisationQueryHandler` in
  `backend/ChildCare.Application/Organisations/GetCurrentOrganisationQuery.cs`, reading
  `PublicDbContext.Tenants` filtered by `ICurrentTenantService.TenantId` — depends on T009
- [X] T011 Add `GET /api/organisations/me` (`DirectorOnly`) to
  `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs` — depends on T010
- [X] T012 [P] Extend `backend/ChildCare.Api.Tests/AuthEndpointTests.cs`: assert `user.name` is
  present and correct on login, refresh, Google, and Apple sign-in responses — depends on T008
- [X] T013 [P] Create `backend/ChildCare.Api.Tests/OrganisationEndpointTests.cs`: `GET
  /api/organisations/me` returns the correct tenant name; non-director role gets `403`;
  unauthenticated gets `401` — depends on T011

### Frontend: typed API client & auth session (research.md R1, R2)

- [X] T014 Create `web/lib/apiClient.ts`: `openapi-fetch` client typed against
  `lib/generated/api-types.ts`, `baseUrl` from `NEXT_PUBLIC_API_BASE_URL`, with a middleware
  porting `mobile/services/apiClient.ts`'s 401-refresh-and-retry logic (no placeholder-origin
  rewrite needed — see research.md R1) — depends on T004
- [X] T015 Rewrite `web/lib/auth.ts` on top of `apiClient.ts`: `login`/`loginWithGoogle`/
  `logout`/`tryRestoreSession` now also carry the organisation name (an added
  `fetchOrganisation()` call to `GET /api/organisations/me` after session establishment) and
  `user.name` from the extended `AuthenticatedUser` — depends on T011, T014
- [X] T016 Update `web/components/AuthProvider.tsx`: `Session` gains `organisationName: string`,
  populated during `tryRestoreSession`/`login` — depends on T015
- [X] T017 [P] Update `web/lib/types.ts`: remove Habits-only types (if any remain after T002),
  keep only types not superseded by generated OpenAPI types

### Shared UI primitives (used by US2 and US3)

- [X] T018 [P] Create `web/components/ConfirmDialog.tsx` (shadcn `Dialog`-based, i18n'd title/
  body/confirm/cancel props) for the PIN-reset, deactivate/reactivate, and device-revoke
  confirmation steps (spec FR-010, FR-014)
- [X] T019 [P] Create `web/components/EmptyState.tsx` (icon + one-sentence message, per
  design-system.md's empty-state pattern) for the Staff/Devices empty states (FR-011, FR-015)
- [X] T020 [P] Create `web/components/ErrorState.tsx` (inline error message + retry button, per
  design-system.md — never a raw error/stack trace) for the Staff/Devices/shell error states
  (FR-012, FR-016)

**Checkpoint**: Foundation ready — API client, session (with org/director name), and shared UI
primitives all in place. User story implementation can now begin.

---

## Phase 3: User Story 1 - Director logs in and reaches a real working app (Priority: P1) 🎯 MVP

**Goal**: A director can sign in (email/password or Google), land in a sidebar shell showing
their organisation and their own name, stay signed in across browser restarts, and log out.

**Independent Test**: Open the app, sign in with a known director account, confirm the sidebar
renders with organisation and director name — independent of any content screen existing.

### Tests for User Story 1

- [X] T021 [P] [US1] `web/__tests__/auth.test.ts` (extend): login success renders the shell with
  organisation/director name; login failure shows a localized inline error and stays on
  `/login`; session persists across a simulated remount (restore via `tryRestoreSession`);
  explicit logout clears the session and redirects to `/login`

### Implementation for User Story 1

- [X] T022 [US1] Rebuild `web/app/(auth)/login/page.tsx` on `theme/colors.ts` tokens and i18n
  keys (`web/i18n/locales/*.json` under a `login` namespace) — depends on T014, T003
- [X] T023 [US1] Update `web/components/GoogleSignInButton.tsx`: restyle on design tokens, i18n
  its error string, keep the existing Google Identity Services flow — depends on T003, T005
- [X] T024 [US1] [P] Create `web/components/Sidebar.tsx`: collapsible nav, organisation name +
  director name (from `AuthProvider`'s `Session`) rendered as a skeleton/neutral loading state
  until that data resolves (FR-005b — never blank space or a flash of placeholder text), real
  links to Staff/Devices, inert/disabled placeholder entries for Locations/Contracts/Children
  (research.md R5) — depends on T016
- [X] T025 [US1] Rewrite `web/app/(app)/layout.tsx` to render `Sidebar.tsx`, keep the existing
  auth-guard redirect-to-login-when-unauthenticated behavior, add a session-expired redirect
  path (edge case: expired refresh token mid-session → redirect to `/login` with a clear
  message), and a catch-all route within `(app)/` for not-yet-built placeholder sections
  (Locations/Contracts/Children) that renders a "not yet available" message instead of a broken
  route or raw 404 if a director navigates directly to one of those URLs — depends on T024
- [X] T026 [US1] Add `login`, `sidebar`, `session` i18n key namespaces to
  `web/i18n/locales/{nl,fr,en}.json` covering every string introduced in T022–T025 — depends on
  T022, T023, T024, T025

**Checkpoint**: User Story 1 fully functional — a director can log in, see the real shell, and
log out, independent of Staff/Devices existing.

---

## Phase 4: User Story 2 - Director finds and manages a staff member (Priority: P2)

**Goal**: A director can search/filter the staff list and reset a PIN or deactivate/reactivate a
staff member from the browser.

**Independent Test**: Sign in, navigate to Staff, search for a known staff member, perform a PIN
reset and a deactivate/reactivate action, confirm the change reflects in the list.

### Tests for User Story 2

- [X] T027 [P] [US2] Create `web/__tests__/staff.test.ts`: table renders staff rows with
  resolved location names; search/filter narrows visible rows; PIN reset success shows
  confirmation; PIN reset conflict (`409 errors.pin.not_unique_at_location`) shows an inline
  form error; deactivate/reactivate require confirmation and update row state; empty tenant
  shows `EmptyState`; API failure shows `ErrorState` with working retry

### Implementation for User Story 2

- [X] T028 [US2] Create `web/components/StaffTable.tsx` (shadcn `Table`): columns name, role,
  location(s) — resolved by joining `EligibleLocationIds` against `GET /api/locations` client-
  side (data-model.md's StaffRow) — active/deactivated status badge, row actions (reset PIN,
  deactivate/reactivate) — depends on T006, T014, T018, T019, T020
- [X] T029 [US2] Create `web/app/(app)/staff/page.tsx`: fetches `GET /api/staff`, search input
  (client-side name filter), renders `StaffTable.tsx`, loading skeleton — depends on T028
- [X] T030 [US2] Wire PIN reset: a `ConfirmDialog`-hosted 4-digit-PIN input calling `PUT
  /api/staff/{id}/pin`, surfacing `errors.pin.not_unique_at_location` inline — depends on T029
- [X] T031 [US2] Wire deactivate/reactivate: `ConfirmDialog` calling `POST
  /api/staff/{id}/deactivate` / `POST /api/staff/{id}/reactivate` — depends on T029
- [X] T032 [US2] Add `staff` i18n key namespace to `web/i18n/locales/{nl,fr,en}.json` covering
  table headers, search placeholder, action labels, confirmation copy, empty/error state text —
  depends on T028, T029, T030, T031

**Checkpoint**: User Stories 1 AND 2 both work independently.

---

## Phase 5: User Story 3 - Director manages paired devices (Priority: P3)

**Goal**: A director can see paired tablets (location, group, paired-by, paired-at) and revoke
one.

**Independent Test**: Sign in, navigate to Devices, confirm paired tablets are listed, revoke
one, confirm it's no longer active.

### Backend: devices list endpoint (research.md R3, contracts/devices-list-api.md)

- [X] T033 [P] [US3] Add `DeviceSummaryResponse` to
  `backend/ChildCare.Contracts/Responses/RoomShiftResponses.cs`
- [X] T034 [US3] Create `ListDevicesQuery`/`ListDevicesQueryHandler` in
  `backend/ChildCare.Application/Devices/ListDevicesQuery.cs`, mirroring
  `ListStaffQuery`'s batch-resolution pattern: query `DevicePairing`, batch-load `Location`/
  `Group`/`TenantUser` names by id, assemble `DeviceSummaryResponse` list — depends on T033
- [X] T035 [US3] Add `GET /api/devices` (`DirectorOnly`) to
  `backend/ChildCare.Api/Endpoints/DevicePairingEndpoints.cs` — depends on T034
- [X] T036 [P] [US3] Create `backend/ChildCare.Api.Tests/DeviceListingTests.cs`: returns correct
  summaries with resolved names; tenant isolation (a second tenant's devices never appear);
  empty tenant returns `200 []`; non-director gets `403` — depends on T035

### Tests for User Story 3 (frontend)

- [ ] T037 [P] [US3] Create `web/__tests__/devices.test.ts`: table renders device rows; revoke
  requires confirmation and updates row state; empty tenant shows `EmptyState`; API failure
  shows `ErrorState` with working retry

### Implementation for User Story 3 (frontend)

- [ ] T038 [US3] Create `web/components/DevicesTable.tsx` (shadcn `Table`): columns location,
  group, paired by, paired at, status — depends on T006, T014, T018, T019, T020, T035
- [ ] T039 [US3] Create `web/app/(app)/devices/page.tsx`: fetches `GET /api/devices`, renders
  `DevicesTable.tsx`, loading skeleton — depends on T038
- [ ] T040 [US3] Wire revoke: `ConfirmDialog` calling `POST /api/devices/{id}/revoke` — depends
  on T039
- [ ] T041 [US3] Add `devices` i18n key namespace to `web/i18n/locales/{nl,fr,en}.json` covering
  table headers, action labels, confirmation copy, empty/error state text — depends on T038,
  T039, T040

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final validation across all stories.

- [ ] T043 [P] Update `backend/ERROR_KEYS.md` with a new "Web Admin Scaffold (feature
  `007a-web-admin-scaffold`)" section noting `GET /api/devices` and `GET
  /api/organisations/me` introduce no new error keys (standard `401`/`403` only) — for
  discoverability alongside every other feature's endpoints
- [ ] T044 Run `specs/007a-web-admin-scaffold/quickstart.md`'s validation scenarios end-to-end
  against a local backend + `web/` dev server
- [ ] T045 Confirm `dotnet build backend/ChildCare.sln`, `dotnet test backend/ChildCare.sln`,
  `cd web && npm run typecheck && npm test && npm run build` all pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phase 3+)**: All depend on Foundational phase completion.
  - US1 has no dependency on US2/US3.
  - US2 has no dependency on US1's implementation beyond the shell existing to navigate from
    (Foundational's `AuthProvider`/`apiClient` already provide the session US2 reads).
  - US3 has no dependency on US1/US2 beyond the same shared Foundational plumbing; its backend
    piece (T033–T036) can start immediately after Foundational.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — no dependency on US2/US3.
- **User Story 2 (P2)**: Can start after Foundational — independently testable without US3.
- **User Story 3 (P3)**: Can start after Foundational — independently testable without US2; its
  backend tasks (T033–T036) have no frontend dependency and can run in parallel with US1/US2
  frontend work.

### Parallel Opportunities

- T003–T006 (Setup) can all run in parallel.
- T007, T009 (Foundational, different files) can run in parallel; T012/T013 (tests) can run in
  parallel with each other once their dependencies land.
- T018–T020 (shared UI primitives) can all run in parallel.
- Once Foundational completes: US1, US2's backend-independent frontend work, and US3's backend
  work (T033–T036) can all proceed in parallel if staffed.

---

## Parallel Example: Foundational Phase

```bash
# Backend name-exposure additions (can start together):
Task: "Add string Name to AuthenticatedUser in backend/ChildCare.Contracts/Responses/AuthSessionResponse.cs"
Task: "Create OrganisationResponse(string Name) in backend/ChildCare.Contracts/Responses/OrganisationResponse.cs"

# Shared UI primitives (can start together once shadcn is initialized):
Task: "Create web/components/ConfirmDialog.tsx"
Task: "Create web/components/EmptyState.tsx"
Task: "Create web/components/ErrorState.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: a director can log in and see the real shell
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → validate independently → MVP
3. Add US2 → validate independently
4. Add US3 → validate independently
5. Polish

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- No task in this feature touches `mobile/` or introduces a database migration
