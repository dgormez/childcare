# Quickstart: Monthly Menu

Validation scenarios proving the feature works end-to-end, once implemented. Assumes a running
local stack (`backend` on its dev port, `web` on `localhost:3000`, `parent-mobile` in Expo Go/
simulator) against a tenant seeded with at least one location, one director account, one parent
account linked to one child with an active contract at that location (existing seed data from
prior features — no new seed script needed).

## Prerequisites

- Backend migration applied: `dotnet ef database update --context TenantDbContext` (or the
  generated SQL script run manually, per this repo's no-auto-migrate convention) against the
  tenant schema.
- Director and parent test accounts already exist (reuse existing seeded accounts from prior
  features' quickstarts, e.g. 013d's).

## Scenario 1 — Director creates, publishes, and a parent sees the menu (User Stories 1 & 2)

1. Log in to `web` as the director. Navigate to the new Menu section (sidebar entry, flat route
   per `web/app/(app)/meal-list/page.tsx`'s existing pattern).
2. Select the location and the current month. Fill in soup/main/dessert for a few days. Save
   (`PUT /api/locations/{locationId}/monthly-menus/{year}/{month}`).
3. Confirm the menu does **not** yet appear in the parent app (log in as the parent, open the Menu
   tab — expect "Menu nog niet beschikbaar").
4. Back in `web`, click Publish (`POST .../publish`).
5. Refresh the parent app's Menu tab — expect the filled-in days to render with soup/main/dessert,
   any day with no entry as "—", and any closure day in that month greyed out with a label.

**Expected outcome**: parent sees exactly what the director published, nothing more/less.

## Scenario 2 — Director corrects a typo mid-month (User Story 5)

1. As director, un-publish the menu from Scenario 1 (`POST .../unpublish`).
2. Confirm the parent app's Menu tab reverts to "Menu nog niet beschikbaar".
3. Correct a day's `mainCourse` text and re-publish.
4. Confirm the parent app shows the corrected value.

**Expected outcome**: un-publish immediately hides the menu; re-publish immediately restores the
corrected version.

## Scenario 3 — Parent requests a preference change, director approves (User Stories 3 & 4)

1. As parent, open the child's meal-preference indicator on the Menu tab and tap "Voorkeur
   aanpassen". Submit a new texture and a note.
2. Confirm `POST /api/parent/children/{childId}/meal-preference-requests` returns `201` with
   `status: "pending"`, and a second submission attempt for the same child returns `409`.
3. As director, open the preference-request review queue
   (`GET /api/meal-preference-requests?status=pending`) — confirm the request appears with the
   child's active health records shown alongside it.
4. Approve the request. Confirm `GET /api/parent/children/{childId}/meal-preference` now reflects
   the new texture, and the parent received a decision notification (in-app `Notification` row;
   push if a token is registered in this environment).

**Expected outcome**: approval writes through to `MealPreference` (013d) and the parent is
notified; the request's `hasPendingRequest` gate correctly blocked the duplicate submission in
step 2.

## Scenario 4 — Director rejects with a reason (User Story 4, negative flow)

1. As parent, submit a second preference-change request for a *different* child (or after the
   first request in Scenario 3 has been decided) with a note.
2. As director, reject it with a reason via `POST /api/meal-preference-requests/{id}/reject`.
3. Confirm the parent's decision notification includes the stated reason (distinct body text from
   a reason-less rejection — verify by also rejecting one request with no reason and comparing).
4. Confirm `MealPreference` for that child is unchanged.

**Expected outcome**: rejection never silently drops the reason, and never mutates
`MealPreference`.

## Cross-references

- Endpoint shapes: [contracts/monthly-menu-api.md](./contracts/monthly-menu-api.md)
- Entity/field detail: [data-model.md](./data-model.md)
- Design decisions behind the above: [research.md](./research.md)
