# Quickstart: Caregiver App Scaffold

## Prerequisites

- Backend running locally (`dotnet run --project backend/ChildCare.Api`), Development environment
- Org A registered (feature 001), a location created (feature 004), a director account
- A caregiver `StaffProfile` created for Org A via feature 005 (`POST /api/staff`, role `Staff`, invitation accepted), eligible for that location
- A child created (feature 006) and assigned to a group (feature 006) at that location
- `cd mobile && npm install`

## Scenario 1 — Generate the API client (research.md R2)

1. With the backend running in Development, run the client-generation script against `http://localhost:<port>/openapi/v1.json`.
2. Confirm `mobile/services/generated/api-types.ts` is produced/updated with no TypeScript errors when the mobile app subsequently type-checks.

## Scenario 2 — Caregiver signs in and stays signed in (User Story 1)

1. Launch the app (`npm start`), land on the login screen.
2. Enter Org A's slug, the caregiver's email, and password → land on the group view.
3. Force-quit and relaunch the app → still signed in, no login prompt (FR-002).
4. Manually expire the access token (or wait past its lifetime) and trigger any API call → the call succeeds after a silent, invisible refresh (FR-004).
5. Tap "Log out" → redirected to login; confirm SecureStore and the local SQLite caches are empty (FR-005).
6. As a director, deactivate the caregiver's account (feature 005); the caregiver's next API call (or refresh) fails cleanly and they're signed out without a retry loop (FR-006, US1 Scenario 5).

## Scenario 3 — Group view, with and without network (User Story 2)

1. Signed in with network connectivity, confirm the group view lists the assigned child, with photo, age, and (if applicable) an allergy icon (FR-007).
2. Tap the child's card → medical quick-access sheet shows allergy/medical-notes data (FR-008).
3. Enable airplane mode → the same list and medical data remain visible (FR-009, SC-002).
4. Pull-to-refresh while online → list reloads from the server (FR-011).
5. On a brand-new device/install with no cache and no network, attempt login → fails gracefully with a clear message (Edge Cases).

## Scenario 4 — Offline queue and sync (User Story 3, using the synthetic test entity)

1. With the app offline, exercise the sync engine's test harness (`_test_entity`) to queue several synthetic actions.
2. Confirm each appears as pending and the offline indicator is visible (FR-010, FR-012).
3. Restore connectivity → confirm all queued actions sync automatically in original order with no manual trigger (FR-012a, FR-013).
4. Queue 50+ synthetic actions, restore connectivity, confirm all eventually sync in order (SC-003).
5. Simulate a 409 response for one queued action → confirm it's discarded and marked synced-with-a-conflict-note by default (FR-014a).
6. Simulate an expired session mid-sync → confirm exactly one refresh-and-retry attempt, then a clear stop-and-surface if that also fails (FR-015).

## Scenario 5 — Backend caregiver-scoping (research.md R6)

1. As the caregiver (Staff role), `GET /api/staff/me` → `200`, returns their `staffProfileId` and the eligible location(s) from feature 005.
2. `GET /api/groups` as the caregiver → only groups at their eligible location(s), never another location's groups in the same tenant.
3. `GET /api/children?groupId={id}` for a group at their location → the assigned child appears. For a group at a *different* location in the same tenant (create a second location/group/child for this check) → empty array, not an error.
4. As the director, repeat steps 2–3 → full, unfiltered tenant visibility, unchanged from feature 006's original behavior.

## Automated coverage

`__tests__/screens/login.test.tsx` (Scenario 2), `__tests__/screens/group-view.test.tsx` (Scenario 3), `__tests__/services/syncEngine.test.ts` + `offlineQueue.test.ts` (Scenario 4), `StaffMeTests.cs` + `CaregiverReadScopingTests.cs` (Scenario 5) — see `tasks.md` for the concrete breakdown.
