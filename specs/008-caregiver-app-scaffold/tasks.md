---

description: "Task list for feature implementation"
---

# Tasks: Caregiver App Scaffold

**Input**: Design documents from `/specs/008-caregiver-app-scaffold/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires real infrastructure — TestContainers-backed xUnit tests for the three backend additions, and real (test-mode) SQLite for the mobile sync-engine/offline-queue tests, with only the network layer faked (matching how `IProfilePhotoStorage`/OAuth validators are faked elsewhere, never the database). Scope follows the project convention (global CLAUDE.md): happy path + key negative flows, not exhaustive per-path coverage.

**Organization**: Tasks are grouped by user story (spec.md: US1 P1 sign-in/session, US2 P1 group view + offline reads, US3 P1 offline queue + sync engine). The backend read-access additions (`GetStaffMeQuery`, extended `ListChildrenQuery`/`ListGroupsQuery`, the route-group split), the Habits-skeleton removal, i18n wiring, the generated API client, and the SQLite `offline_queue`/`read_cache` tables are shared prerequisites every story depends on, so they live in Foundational.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1/US2/US3) — omitted for Setup/Foundational/Polish tasks
- File paths are exact and repository-root-relative

## Path Conventions

This feature touches both `mobile/` (Expo/React Native/TypeScript) and `backend/` (C#/.NET) — the first feature in this codebase to touch `mobile/`.

---

## Phase 1: Setup

**Purpose**: Confirm baseline on both stacks before rewriting a large fraction of the mobile app and extending backend authorization.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds and `cd mobile && npm test` passes on this branch before starting

**Checkpoint**: Baseline confirmed on both stacks.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The backend read-access additions (self-service staff lookup, caregiver-scoped children/groups reads), the removal of every Habits-skeleton file, i18n wiring, the generated typed API client, and the two new SQLite tables. Every user story depends on this being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend: caregiver read access (research.md R6)

- [X] T002 [P] Create `StaffMeResponse(Guid StaffProfileId, string FirstName, string LastName, string Role, IReadOnlyList<Guid> EligibleLocationIds)` in `backend/ChildCare.Contracts/Responses/StaffMeResponse.cs`
- [X] T003 Create `GetStaffMeQuery` + `GetStaffMeQueryHandler` in `backend/ChildCare.Application/Staff/GetStaffMeQuery.cs`: resolves the caller's `StaffProfile` by matching `TenantUserId` against the `Guid` passed in from the endpoint's `ClaimTypes.NameIdentifier`, loads all `StaffLocationEligibility` rows for that profile, fails `errors.staff.profile_not_found` (404) if no profile exists for that user — depends on T002
- [X] T004 Add a standalone `GET /api/staff/me` route in `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs`, **outside** the existing `DirectorOnly` group (its own `app.MapGet(...).RequireAuthorization("StaffOrDirector")` registration — group + route policies compose additively in ASP.NET Core, so this cannot live inside the `DirectorOnly` group, research.md R6), extracting the caller's id via `ctx.User.FindFirst(ClaimTypes.NameIdentifier)` — depends on T003
- [X] T005 Extend `ListChildrenQuery` in `backend/ChildCare.Application/Children/ListChildrenQuery.cs` with an optional `Guid? GroupId` (joins `ChildGroupAssignment` where `EndDate == null`) and optional `string? CallerRole`, `IReadOnlyList<Guid>? CallerEligibleLocationIds` — when `CallerRole == "staff"`, additionally filter to children with a currently-active `ChildGroupAssignment` whose `Group.LocationId` is in the caller's eligible locations; `Director`/omitted callers see unchanged feature-006 behavior — depends on nothing new (extends existing file)
- [X] T006 Extend `ListGroupsQuery` in `backend/ChildCare.Application/Groups/ListGroupsQuery.cs` with the same optional `CallerRole`/`CallerEligibleLocationIds` parameters — when `Staff`, filter to `LocationId IN (caller's eligible locations)`; `Director`/omitted unchanged
- [X] T007 Split the `GET /` and `GET /{id:guid}` routes in `backend/ChildCare.Api/Endpoints/ChildrenEndpoints.cs` into their own `StaffOrDirector`-authorized `MapGroup("/api/children")` registration (separate from the existing `DirectorOnly` write-route group in the same file), passing the caller's role/id (extracted via `ClaimTypes.Role`/`ClaimTypes.NameIdentifier`, resolving eligible locations via a `StaffLocationEligibility` lookup when the role is `staff`) into `ListChildrenQuery`/`GetChildByIdQuery` — depends on T005
- [X] T008 Split the `GET /` route in `backend/ChildCare.Api/Endpoints/GroupsEndpoints.cs` into its own `StaffOrDirector`-authorized `MapGroup("/api/groups")` registration (separate from the existing `DirectorOnly` write-route group), same caller-role/id extraction as T007, into `ListGroupsQuery` — depends on T006
- [X] T009 [P] Add `errors.staff.profile_not_found` (404) to `backend/ERROR_KEYS.md` under a new "Caregiver App Scaffold (feature `008-caregiver-app-scaffold`)" section, documenting the `GET /api/staff/me` addition and the `StaffOrDirector` split on `GET /api/children`/`GET /api/groups`

### Mobile: remove the Habits walking skeleton

- [X] T010 Delete `mobile/app/onboarding.tsx`, `mobile/app/oauthredirect.tsx`, `mobile/app/habit/` (both files), `mobile/app/(tabs)/` (all four files), `mobile/app/(auth)/register.tsx`, `mobile/app/(auth)/forgot-password.tsx`, `mobile/app/(auth)/reset-password.tsx`, `mobile/app/(auth)/verify-email.tsx`, `mobile/services/api.ts`, `mobile/services/googleAuth.ts`, `mobile/services/appleAuth.ts`, and their corresponding `mobile/__tests__/screens/register.test.tsx` (and any other Habits-only test files) — leaves `mobile/services/notifications.ts` untouched (push token registration is feature 009's job, but the file itself is reusable infrastructure, not Habits-specific)
- [X] T011 Remove all Habits domain state (`habits`, `completions`, `lastSyncAt`, associated setters) from `mobile/store/useStore.ts`; keep and extend only the `auth` slice
- [X] T012 Update `mobile/types/index.ts`: remove `Habit`/`HabitCompletion`; update `AuthResponse`/`AuthState` to the real feature-003 shape — `AuthResponse.user` gains `role: string`; `AuthState` gains `role`, `organisationSlug`, `staffProfileId?`, `eligibleLocationIds?: string[]`

### Mobile: i18n, network dependencies, and API client generation

- [X] T013 [P] Add `react-i18next`, `expo-localization`, `@react-native-community/netinfo` to `dependencies` and `openapi-typescript`, `openapi-fetch` to `mobile/package.json` (openapi-fetch is a runtime dependency, openapi-typescript is dev-only); remove `expo-apple-authentication`, `expo-auth-session`, `expo-web-browser` (caregiver app is password-only, research.md R3)
- [X] T014 [P] Create `mobile/i18n/index.ts` (react-i18next init, `expo-localization` device-locale detection, fallback `nl`) and `mobile/i18n/locales/{nl,fr,en}.json` seeded with keys for: login form labels/errors, offline banner text, sync status text, logout confirmation (per FR-016) — depends on T013
- [X] T015 Add an `mobile/package.json` script `"generate-api-client"` running `openapi-typescript <backend dev OpenAPI URL> -o services/generated/api-types.ts` (research.md R2) — depends on T013
- [X] T016 Run the generation script against the local dev backend (with T004/T007/T008's new/changed routes already present) and commit the resulting `mobile/services/generated/api-types.ts` — depends on T004, T007, T008, T015
- [X] T017 Create `mobile/services/apiClient.ts`: `openapi-fetch` client typed against `api-types.ts`, with `EXPO_PUBLIC_API_BASE_URL` as the base URL and a middleware that attaches `Authorization: Bearer <token>` from the auth slice to every request — depends on T016

### Mobile: SQLite offline tables

- [X] T018 Extend `mobile/services/localDb.ts`: add `CREATE TABLE IF NOT EXISTS offline_queue (...)` and `read_cache (...)` per data-model.md's exact column list, both including `tenant_id`
- [X] T019 [P] Add/extend Jest mocks: confirm `mobile/__mocks__/expo-secure-store.js` still applies; add a `mobile/__mocks__/@react-native-community/netinfo.js` fake event-emitter mock — depends on T013

**Checkpoint**: Backend serves caregiver-scoped reads; the Habits skeleton is fully removed; i18n, the generated API client, and the two SQLite tables all exist. User story implementation can now begin.

---

## Phase 3: User Story 1 - Caregiver Signs In and Stays Signed In (Priority: P1) 🎯 MVP

**Goal**: A caregiver logs in with email/password (plus organisation slug), stays signed in across restarts, gets silently refreshed on token expiry, and is cleanly signed out on explicit logout or on account deactivation.

**Independent Test**: Log in with valid credentials, confirm the group view loads and the session survives a force-quit/relaunch; force an expired token and confirm silent refresh; log out and confirm SecureStore/caches are empty; deactivate the account mid-session and confirm clean sign-out with no retry loop.

### Tests for User Story 1

- [X] T020 [P] [US1] Backend integration test: `GET /api/staff/me` as a caregiver → `200` with own `staffProfileId`/`eligibleLocationIds`; as a director with no staff profile of their own → adjust to assert whichever role is used has a profile, or use a director who also has an optional staff profile per feature 005 — in `backend/ChildCare.Api.Tests/StaffMeTests.cs` — depends on T004
- [X] T021 [P] [US1] Rewrite `mobile/__tests__/screens/login.test.tsx`: valid organisationSlug/email/password submits and navigates to the group view; invalid credentials show the `errors.auth.invalid_credentials` i18n message; no OAuth buttons render — depends on T014
- [X] T022 [P] [US1] Test: `tryRestoreSession` with a valid stored refresh token restores the session without a fresh login prompt (session persists across restart, FR-002) — in `mobile/__tests__/services/auth.test.ts`
- [X] T023 [P] [US1] Test: an API call with an expired access token triggers exactly one silent refresh-and-retry via `apiClient.ts`'s middleware, transparent to the caller (FR-004) — same file
- [X] T024 [P] [US1] Test: logout clears SecureStore tokens, the auth slice, and both SQLite tables (`offline_queue`, `read_cache`) for the current tenant (FR-005, FR-019) — same file
- [X] T024a [P] [US1] Test: caregiver A logs in, populates `read_cache`/`offline_queue`, logs out; caregiver B then logs in on the same device/database — confirm zero rows or references to A's data remain reachable by B (FR-019's device-sharing scenario specifically, `/speckit-analyze` E1) — same file
- [X] T025 [P] [US1] Test: a refresh attempt that itself returns 401 (deactivated account) triggers a clean sign-out with no further retry (FR-006, US1 Scenario 5) — same file

### Implementation for User Story 1

- [X] T026 [US1] Rewrite `mobile/app/(auth)/login.tsx`: organisationSlug + email + password fields, i18n labels/errors, no social-login buttons — depends on T014, T017
- [X] T027 [US1] Update `mobile/app/(auth)/_layout.tsx`: drop the removed register/forgot-password/reset-password/verify-email screen registrations — depends on T010
- [X] T028 [US1] Rewrite `mobile/services/auth.ts`: `login(organisationSlug, email, password)`, `refresh()`, `logout()` against the real `LoginRequest`/`RefreshRequest`/`LogoutRequest`/`AuthSessionResponse` shapes (research.md R3), storing `organisationSlug` alongside tokens in SecureStore so silent refresh can resend it — depends on T012, T017
- [X] T029 [US1] Implement the `apiClient.ts` auth middleware: attach Bearer token; on `401`, call `refresh()` once, retry the original request once, and on a second `401` propagate a distinguishable "session invalid" signal rather than retrying again (FR-004, FR-006, FR-015's "attempt exactly one renewal" rule reused here for ordinary API calls too) — depends on T017, T028
- [X] T030 [US1] On successful login, call `GET /api/staff/me` and store `staffProfileId`/`role`/`eligibleLocationIds` in the auth slice (`mobile/store/useStore.ts`) for display purposes — depends on T028, T004
- [X] T031 [US1] Implement clean sign-out on a "session invalid" signal from T029: clear SecureStore, clear `offline_queue`/`read_cache` for the current tenant, reset the auth slice, redirect to login with a clear i18n message (FR-006) — depends on T029
- [X] T032 [US1] Update `mobile/app/_layout.tsx`: bootstrap `i18n`, `apiClient`, and `tryRestoreSession` on launch; remove the Habits-specific habits/completions bootstrap and `registerForPushNotifications()` call (out of scope, feature 009) — depends on T026, T028

**Checkpoint**: US1 fully functional and independently testable — a caregiver can sign in, stay signed in, get silently refreshed, log out cleanly, and get cleanly signed out on deactivation.

---

## Phase 4: User Story 2 - Caregiver Sees Their Children, Even Offline (Priority: P1)

**Goal**: After signing in, a caregiver sees every child at their eligible location(s) today, with medical quick-access, cached for offline viewing.

**Independent Test**: Log in, confirm the group view lists the right children with correct alert icons; enable airplane mode and confirm the same list and medical data remain visible; pull-to-refresh while online reloads from the server.

### Tests for User Story 2

- [X] T033 [P] [US2] Backend integration test: as a caregiver, `GET /api/groups` returns only groups at their eligible location(s); a group at a different location in the same tenant never appears — in `backend/ChildCare.Api.Tests/CaregiverReadScopingTests.cs` — depends on T008
- [X] T034 [P] [US2] Backend integration test: as a caregiver, `GET /api/children?groupId={id}` for an eligible group returns its children; for a group at a location the caregiver is not eligible for, returns `200` with an empty array (not an error) — same file — depends on T007
- [X] T035 [P] [US2] Backend integration test: as a director, both `GET /api/groups` and `GET /api/children` remain fully unfiltered (feature 006 behavior unchanged) — same file — depends on T007, T008
- [X] T035a [P] [US2] Backend integration test: a caregiver with **zero** `StaffLocationEligibility` rows gets `200` with an empty array from both `GET /api/groups` and `GET /api/staff/me`'s `eligibleLocationIds`, never a 4xx/5xx (FR-007 empty state, `/speckit-analyze` E2) — same file — depends on T004, T008
- [X] T036 [P] [US2] Mobile test: the group view loads children via `GET /api/groups` → `GET /api/children?groupId=` per group, shows name/photo/age and an allergy icon when applicable, and an (always-inactive-for-now) fever icon slot (FR-007) — in `mobile/__tests__/screens/group-view.test.tsx` — depends on T017
- [X] T037 [P] [US2] Mobile test: tapping a child's card opens the medical quick-access sheet with allergy/medical-notes data (FR-008) — same file
- [X] T038 [P] [US2] Mobile test: after a successful load, simulating offline (network status `false`) still renders the same children list and medical data from `read_cache` (FR-009, SC-002) — same file
- [X] T039 [P] [US2] Mobile test: pull-to-refresh while online re-fetches from the server and updates `read_cache` (FR-011) — same file
- [X] T040 [P] [US2] Mobile test: brand-new install, no cache, no network at login time → login fails with a clear i18n message rather than a broken/empty screen (Edge Cases) — implemented in `mobile/__tests__/screens/login.test.tsx` (more natural location than group-view.test.tsx since it's a login-time failure, not a group-view concern) — same file
- [X] T040a [P] [US2] Mobile test: a caregiver with zero assigned children (empty `GET /api/groups` result, or groups with no children) sees a clear empty state, not a blank/broken screen (FR-007, `/speckit-checklist` CHK002) — same file
- [X] T040b [P] [US2] Backend integration test: two caregivers eligible for different locations in the same tenant each see only their own location's groups/children — neither ever sees the other's (FR-007a, `/speckit-checklist` CHK001) — in `backend/ChildCare.Api.Tests/CaregiverReadScopingTests.cs`

### Implementation for User Story 2

- [X] T041 [US2] Create `mobile/services/readCache.ts`: typed `get(cacheKey)`/`set(cacheKey, data)` over the `read_cache` table, always writing `expires_at = NULL` (FR-015a) — depends on T018
- [X] T042 [US2] Create `mobile/hooks/useNetworkStatus.ts`: wraps `NetInfo.addEventListener`, exposes `{ isConnected }` (research.md R5) — depends on T013, T019
- [X] T043 [US2] Create `mobile/app/(app)/_layout.tsx`: authenticated shell (tab bar/header), renders the offline banner (i18n text) when `useNetworkStatus().isConnected` is false — depends on T014, T042
- [X] T044 [US2] Create `mobile/app/(app)/index.tsx` (group view): on load, call `GET /api/groups` then `GET /api/children?groupId=` per group via `apiClient`, cache the combined result via `readCache.ts`, render child cards (name/photo/age/allergy icon/fever-icon slot); on load failure with a cache hit, render from `read_cache` instead; pull-to-refresh re-fetches (FR-007, FR-009, FR-011) — depends on T030, T041, T043
- [X] T045 [US2] Create `mobile/app/(app)/child/[id].tsx`: medical quick-access sheet reading allergy/medical-notes fields from the cached child data (FR-008) — depends on T044
- [X] T046 [US2] Handle the brand-new-install/no-cache/no-network login failure path in `mobile/app/(auth)/login.tsx`/`services/auth.ts`: a login attempt with no network and nothing previously cached surfaces a clear "can't sign in without a connection" i18n message (Edge Cases) — depends on T026, T028

**Checkpoint**: US1 and US2 both work independently — a caregiver can see and offline-view their children without any offline-write capability yet.

---

## Phase 5: User Story 3 - Actions Taken Offline Are Never Lost (Priority: P1)

**Goal**: A generic offline queue and sync engine exists, ready for features 009/010 to register entity handlers against — proven correct now, via a synthetic test entity, since no real handler exists yet.

**Independent Test**: Using the synthetic `_test_entity` handler, queue several actions offline, confirm they show as pending, restore connectivity, and confirm they sync in original order with none lost; confirm the FR-014a conflict default and the FR-015 single-refresh-retry-then-stop behavior.

### Tests for User Story 3

- [X] T047 [P] [US3] Test: `offlineQueue.enqueue()` while offline records a row with `synced_at = NULL`; `getPending()` returns rows ordered by `created_at` ascending — in `mobile/__tests__/services/offlineQueue.test.ts` — depends on T018
- [X] T048 [P] [US3] Test: `syncEngine.registerSyncHandler('_test_entity', ...)` + `syncPendingQueue()` sends queued rows to a faked API client sequentially, in order, and marks each `synced_at` on success (FR-012, FR-013) — in `mobile/__tests__/services/syncEngine.test.ts`
- [X] T049 [P] [US3] Test: a row that fails with a simulated transient (500/network) error remains pending and is retried on the next `syncPendingQueue()` call, never discarded (FR-014) — same file
- [X] T050 [P] [US3] Test: a row that fails with a simulated `409` and no registered `onConflict` handler is discarded and marked synced with a conflict note by default (FR-014a) — same file
- [X] T051 [P] [US3] Test: a row blocked by a simulated `401` triggers exactly one `refresh()` call and one retry of that row; if the retried call also fails, the whole sync run stops with a clear surfaced error rather than continuing to the next row (FR-015) — same file
- [X] T052 [P] [US3] Test: 50+ synthetic queued rows all eventually sync, strictly in `created_at` order, with none silently dropped (FR-013, SC-003) — same file
- [X] T053 [P] [US3] Test: `syncPendingQueue()` is invoked by the app shell exactly on network-reconnect, app-foreground, and pull-to-refresh events — never on a timer (FR-012a) — implemented as a dedicated shell test in `mobile/__tests__/screens/app-layout.test.tsx` (network/foreground triggers) plus an assertion in the existing pull-to-refresh test in `group-view.test.tsx`

### Implementation for User Story 3

- [X] T054 [US3] Create `mobile/services/offlineQueue.ts`: `enqueue()`, `getPending()`, `markSynced()`, `markSyncError()` per the internal contract (contracts/mobile-offline-sync.md) — depends on T018
- [X] T055 [US3] Create `mobile/services/syncEngine.ts`: `registerSyncHandler()`, `syncPendingQueue()` implementing sequential replay, the FR-014a default conflict handling, the FR-015 single-refresh-retry-then-stop rule, and per-row transient-error retry-on-next-run (FR-014) — depends on T028 (for `refresh()`), T054
- [X] T056 [US3] Create `mobile/hooks/useSyncStatus.ts`: `{ pendingCount, lastSyncedAt, isSyncing }` derived from `offlineQueue`/`syncEngine` state — depends on T055
- [X] T057 [US3] Wire `syncPendingQueue()` to fire on network reconnect (`useNetworkStatus` transitioning to connected), app foreground, and pull-to-refresh, in `mobile/app/(app)/_layout.tsx`/`mobile/app/(app)/index.tsx` — no other trigger (FR-012a) — depends on T042, T043, T044, T055
- [X] T058 [US3] Render the sync status (pending count / syncing indicator) alongside the offline banner in `mobile/app/(app)/_layout.tsx`, using `useSyncStatus()` (FR-010) — depends on T056, T057

**Checkpoint**: All three user stories are independently functional — the app authenticates, shows children online/offline, and has a proven-correct (if not yet used by any real feature) offline write path ready for feature 009.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all stories and both stacks.

- [X] T059 [P] Run `dotnet test backend/ChildCare.sln` and confirm no regressions in pre-existing tests (features 001–007) — the `StaffOrDirector` split on `GET /api/children`/`GET /api/groups` must not change any existing Director-role test's expected behavior — 185/185 passing
- [X] T060 [P] Run `cd mobile && npm test` and confirm the full mobile suite passes, including the rewritten login tests and the new group-view/sync-engine/offline-queue tests — 46/46 passing across 9 suites
- [X] T061 Manually walk through every scenario in `quickstart.md` (or confirm via the automated tests already covering them) and confirm all five pass end-to-end — all five confirmed via the automated coverage listed in quickstart.md's own "Automated coverage" section
- [X] T062 Confirm `mobile/services/generated/api-types.ts` has no stale/unused types left over from the removed Habits-era hand-rolled request shapes, and that `tsc`/type-check passes cleanly across the rewritten files — `npx tsc --noEmit` clean; no Habits references remain outside the deleted files. Found and fixed a real bug during this pass: `apiClient.ts` created the openapi-fetch client with `baseUrl: () => baseUrl`, which both failed to type-check (openapi-fetch's `baseUrl` is a fixed string, not a getter) and was silently wrong at runtime (read once at creation, before `configureApiBaseUrl()` ever runs). Fixed by creating the client against a placeholder origin and rewriting every request's URL to the real, dynamically-configured base URL in the auth middleware's `onRequest`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–5)**: All depend on Foundational phase completion
  - US1 has no dependency on other stories
  - US2 depends on US1's auth/apiClient/staff-me wiring (T028, T029, T030) to have an authenticated session and identity to work with
  - US3 depends on US1's `refresh()` (T028) for its own single-retry-on-401 rule, but is otherwise independent of US2 — it could be built in parallel with US2 by a different contributor
- **Polish (Phase 6)**: Depends on all three user stories being complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Foundational plumbing (entities/config/ports) before commands/queries/services
- Services before screens/hooks that consume them
- Story complete before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (T002, T009, T013, T014 once T013 lands, T019 once T013 lands) — the backend chain (T002→T003→T004→T007) and (T005/T006→T007/T008) is otherwise sequential per-file; the Habits-removal chain (T010→T011→T012) is sequential; the API-client chain (T013→T015→T016→T017) is sequential
- Once Foundational completes, US1's tests (T020–T025) can all run in parallel
- US2's and US3's tests can each run in parallel within their own story once Foundational + US1 exist
- US2 and US3 implementation can proceed in parallel with each other (different files: `readCache.ts`/group-view screens vs `offlineQueue.ts`/`syncEngine.ts`), both depending only on US1's auth wiring

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Backend test: GET /api/staff/me in backend/ChildCare.Api.Tests/StaffMeTests.cs"
Task: "Rewrite login.test.tsx in mobile/__tests__/screens/login.test.tsx"
Task: "Session-restore test in mobile/__tests__/services/auth.test.ts"
Task: "Silent-refresh test in mobile/__tests__/services/auth.test.ts"
Task: "Logout-clears-everything test in mobile/__tests__/services/auth.test.ts"
Task: "Deactivated-account clean-sign-out test in mobile/__tests__/services/auth.test.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenario 2)
5. Deploy/demo if ready — a caregiver can sign in and stay signed in, even with nothing to look at yet

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo (infrastructure only, no user-visible change until feature 009)
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- This is the first feature touching `mobile/` — every file under `mobile/app/(tabs)/`, `mobile/app/habit/`, `mobile/app/onboarding.tsx` is Habits-skeleton content being removed, not an existing pattern being extended
- The three backend additions are deliberately minimal (one new query, two existing queries gaining optional caller-scoping params, no new tables/migration) — necessary plumbing this feature cannot function without, not scope creep
- The sync engine (US3) ships with zero registered production entity handlers — its own tests use a synthetic `_test_entity` registration, unregistered after use (research.md R4)
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
