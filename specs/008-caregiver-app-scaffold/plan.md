# Implementation Plan: Caregiver App Scaffold

**Branch**: `008-caregiver-app-scaffold` | **Date**: 2026-07-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-caregiver-app-scaffold/spec.md`

## Summary

Replaces the Habits walking-skeleton content of the `mobile/` Expo app with a real, authenticated caregiver app shell: email/password login against the existing feature-003 auth contract, SecureStore-only token storage with silent refresh-on-401, a generated typed API client (openapi-typescript + openapi-fetch, the first adoption of this pattern anywhere in the repo), a group view home screen showing the caregiver's children with medical quick-access, and a generic SQLite-backed offline queue + sync engine that ships with zero registered entity handlers (feature 009 registers `child_event`, feature 010 registers `attendance_record`). This feature also makes three small, necessary backend additions the mobile client cannot function without: a `GET /api/staff/me` self-service endpoint (no such self-lookup exists anywhere today), and `StaffOrDirector`-authorized, caregiver-location-scoped read access to children and groups (both endpoints are currently `DirectorOnly` end-to-end, per feature 006).

## Technical Context

**Language/Version**: TypeScript ~5.9 (mobile, Expo SDK 54 / React Native 0.81 / React 19) for the client; C# / .NET 10 (unchanged) for the three small backend additions

**Primary Dependencies**: `expo-router` (file-based navigation, already present), `expo-secure-store` (already present, token storage), `expo-sqlite` (already present, offline queue + read cache), `zustand` (already present, in-memory session/UI state) — **new**: `openapi-typescript` (dev-only, client generation) + `openapi-fetch` (runtime HTTP client, per PROJECT-BRIEF.md's decided-but-never-yet-adopted pattern), `react-i18next` + `expo-localization` (i18n, not present anywhere in mobile today), `@react-native-community/netinfo` (network state, not present today). **Removed**: `expo-apple-authentication`, `expo-auth-session`, `expo-web-browser` (Google/Apple OAuth — caregiver app is password-only per constitution; these packages exist only for the Habits skeleton's parent-app-style social login, which the caregiver app never uses)

**Storage**: On-device SQLite (`expo-sqlite`) for `offline_queue` and `read_cache` (research.md R1); backend storage unchanged (PostgreSQL, schema-per-tenant) — the two small backend query/endpoint additions read existing tables (`staff_profiles`, `staff_location_eligibility`, `children`, `groups`, `child_group_assignments`), no new backend tables or migration

**Testing**: Jest + `jest-expo` + `@testing-library/react-native` (already configured, existing `__mocks__/expo-secure-store.js` and `__tests__/screens/*.test.tsx` pattern to extend) for the mobile app; xUnit + TestContainers (unchanged) for the three backend additions

**Target Platform**: iOS/Android tablet (Expo managed workflow) for the client; existing Cloud Run deployment (unchanged) for the backend additions

**Project Type**: Mobile app + minimal backend extension — the first feature in this codebase to touch `mobile/`; every prior feature (001–007) was backend-only

**Performance Goals**: SC-001's task-completion framing (children list visible within 15s of opening the app on a normal connection); no explicit throughput target — a single caregiver's device making requests against a handful of locations/groups, not a high-volume API consumer

**Constraints**: SecureStore only for tokens (FR-003, constitution Principle VI); no fixed-interval background sync polling (FR-012a); offline reads never expire on a timer (FR-015a); landscape-first, 48pt minimum touch targets (FR-017); every user-facing string routed through i18n keys, NL/FR/EN (FR-016, constitution Principle IV); the two new/extended backend read paths must not weaken existing Director-facing behavor or tests — Staff-role scoping is additive, Director behavior is unchanged

**Scale/Scope**: One Expo app (already exists, being substantially rewritten, not created fresh) — 4 new/replaced screens (login, authenticated shell, group view, child detail), ~6 new service modules (API client, auth, sync engine, network status, i18n, offline queue), 2 new SQLite tables, 1 new backend endpoint (`GET /api/staff/me`) + 2 backend endpoints extended with an additional authorization policy and caller-scoped filtering (`GET /api/children`, `GET /api/groups`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | The new `GET /api/staff/me` and the extended `GET /api/children`/`GET /api/groups` routes are all non-tenant-exempt, going through the existing `TenantMiddleware`/`ICurrentTenantService` (feature 002) exactly like every other authenticated route — no new tenant-resolution path is introduced. On-device SQLite tables (`offline_queue`, `read_cache`) are tagged with the signed-in caregiver's tenant id and cleared on logout (FR-019), so a device never mixes cached data across organisations. |
| II. Regulatory Compliance by Design | ✅ Pass | This feature stores/displays no regulatory data itself (BKR, attendance) — it only makes existing feature-006 medical-alert data visible to caregivers, which is a read-access change, not a new compliance surface. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | `GetStaffMeQuery`, and the extended `ListChildrenQuery`/`ListGroupsQuery` (now caller-scoped), remain plain MediatR queries; endpoint files only extract the caller's claims from `HttpContext.User` and map to/from HTTP, matching the existing `AuthEndpoints.cs` precedent for claims extraction. On the mobile side, the sync engine and API client are plain service modules, not framework-coupled — screens call hooks (`useSyncStatus`, `useNetworkStatus`), never the SQLite layer directly. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass | This is the first feature to actually wire `react-i18next`/`expo-localization` in the mobile app (the Habits skeleton had zero i18n) — every new screen and every sync/offline/error message routes through translation keys, NL/FR/EN, from the first commit of this feature, not retrofitted later. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass | The three backend additions get the same TestContainers-backed integration test treatment as every prior feature. The mobile app has no direct database of its own beyond on-device SQLite, which is exercised for real (not mocked) in the sync-engine tests via `expo-sqlite`'s in-memory/test mode — only the network layer (API responses) is faked in mobile tests, matching how `IProfilePhotoStorage`/OAuth validators are faked on the backend rather than the database being faked. |
| VI. Secure Configuration & Storage | ✅ Pass | Tokens live only in `expo-secure-store`, never `AsyncStorage` (FR-003) — a stricter, explicitly-tested continuation of the one SecureStore call site the Habits skeleton already had. No new secrets are introduced; the API base URL is a plain (non-secret) Expo env var, matching the existing `EXPO_PUBLIC_*` convention. |
| VII. Monolith-First Simplicity | ✅ Pass | No new backend project — the two extended queries and one new query live in the existing `ChildCare.Application`/`ChildCare.Api` projects. The mobile app remains one Expo project (no new client app, no separate offline-sync package/library extracted prematurely — the sync engine is a handful of modules inside `mobile/services/`, not a published package, since only this one app currently needs it). |

**Overall**: 7 of 7 clean passes. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/008-caregiver-app-scaffold/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── staff-me-api.md         # New GET /api/staff/me
│   ├── children-groups-api.md  # Extended GET /api/children, GET /api/groups
│   └── mobile-offline-sync.md  # Internal contract: offline_queue/read_cache schema, sync engine behavior
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

### Source Code (repository root)

```text
mobile/
├── app/
│   ├── (auth)/
│   │   ├── login.tsx                       #   REWRITTEN — email+password+organisation slug, i18n, no OAuth buttons
│   │   └── _layout.tsx                     #   MODIFIED — drop register/forgot-password/reset-password/verify-email routes
│   ├── (app)/                              #   NEW — replaces (tabs)/
│   │   ├── _layout.tsx                     #   NEW — authenticated shell, offline banner, sync status
│   │   ├── index.tsx                       #   NEW — group view (home screen)
│   │   └── child/[id].tsx                  #   NEW — child detail / medical quick-access
│   ├── onboarding.tsx                      #   REMOVED — Habits-specific
│   ├── habit/                              #   REMOVED
│   ├── (tabs)/                             #   REMOVED — habits.tsx, subscription.tsx, settings.tsx, index.tsx
│   └── _layout.tsx                         #   MODIFIED — bootstrap i18n + sync engine instead of Habits store
├── services/
│   ├── apiClient.ts                        #   NEW — openapi-fetch instance + auth interceptor (research.md R2)
│   ├── auth.ts                             #   REWRITTEN — organisationSlug-aware login/refresh/logout (research.md R3)
│   ├── offlineQueue.ts                     #   NEW — SQLite offline_queue CRUD (research.md R1)
│   ├── readCache.ts                        #   NEW — SQLite read_cache CRUD (research.md R1)
│   ├── syncEngine.ts                       #   NEW — syncPendingQueue(), handler registry (research.md R4)
│   ├── localDb.ts                          #   MODIFIED — add offline_queue/read_cache table creation
│   ├── api.ts                              #   REMOVED — replaced by apiClient.ts
│   ├── googleAuth.ts / appleAuth.ts        #   REMOVED — caregiver app is password-only
│   └── generated/api-types.ts              #   NEW — openapi-typescript output (generated, gitignored or committed — research.md R2)
├── hooks/
│   ├── useNetworkStatus.ts                 #   NEW — netinfo subscription (research.md R5)
│   ├── useSyncStatus.ts                    #   NEW — { pendingCount, lastSyncedAt, isSyncing }
│   └── useSync.ts                          #   MODIFIED — generalized from Habits-specific polling to the new sync engine
├── store/
│   └── useStore.ts                         #   MODIFIED — drop Habits domain state; keep/extend auth slice
├── i18n/                                   #   NEW
│   ├── index.ts                            #   react-i18next init, expo-localization device-locale detection
│   └── locales/{nl,fr,en}.json             #   NEW translation resources
├── types/index.ts                          #   MODIFIED — AuthResponse gains `role`; login/refresh gain `organisationSlug`
├── __mocks__/                              #   MODIFIED — add expo-sqlite in-memory mock extensions, @react-native-community/netinfo mock
├── __tests__/
│   ├── screens/login.test.tsx              #   REWRITTEN for the new login screen
│   ├── screens/group-view.test.tsx         #   NEW
│   ├── services/syncEngine.test.ts         #   NEW — synthetic entity-type test double
│   └── services/offlineQueue.test.ts       #   NEW
├── package.json                            #   MODIFIED — dependency changes above
└── app.config.js                           #   MODIFIED — EXPO_PUBLIC_API_BASE_URL env var wiring

backend/
├── ChildCare.Application/
│   └── Staff/
│       └── GetStaffMeQuery.cs               #   NEW — self-scoped by caller's TenantUserId, returns eligible location ids
│   └── Children/
│       └── ListChildrenQuery.cs             #   MODIFIED — optional GroupId filter; optional caller-scoping params
│   └── Groups/
│       └── ListGroupsQuery.cs               #   MODIFIED — optional caller-scoping params (Staff → eligible locations only)
├── ChildCare.Api/
│   └── Endpoints/
│       ├── StaffEndpoints.cs                #   MODIFIED — new standalone GET /api/staff/me route (StaffOrDirector, outside the DirectorOnly group)
│       ├── ChildrenEndpoints.cs             #   MODIFIED — GET routes split into their own StaffOrDirector-authorized route group, separate from the DirectorOnly write group
│       └── GroupsEndpoints.cs               #   MODIFIED — GET /api/groups split into its own StaffOrDirector-authorized route group
├── ChildCare.Contracts/
│   └── Responses/
│       └── StaffMeResponse.cs               #   NEW
└── ChildCare.Api.Tests/
    ├── StaffMeTests.cs                      #   NEW
    └── CaregiverReadScopingTests.cs         #   NEW — Staff-role location-scoping on children/groups reads
```

**Structure Decision**: Mobile app (Expo/React Native) + a minimal, additive backend extension. This is the first feature to modify `mobile/` — every file under `mobile/app/(tabs)/`, `mobile/app/habit/`, and `mobile/app/onboarding.tsx` is Habits-skeleton content being removed, not a pattern being followed. The three backend changes are deliberately the smallest additions that unblock the mobile client (one new self-service query, two existing queries gaining an additional authorization policy and caller-aware filtering) — no new backend project, no new tables, no migration.

## Complexity Tracking

> Empty — no unresolved Constitution Check violations remain for this plan. The backend authorization/scoping work is flagged above as a deliberate, necessary, minimally-scoped addition (not a violation) — without it, the mobile client this feature exists to build would have no data to display.
