# Quickstart: Meal List (Maaltijdenlijst)

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against a Docker PostgreSQL
  instance with the new `AddChildMealPreferences` migration applied (script generated, run
  manually per this repo's convention — see research.md R7).
- `web/` running (`npm run dev`), API types regenerated (`npm run generate-api-client`) after the
  backend contract change.
- A director account, a caregiver-role account (or a paired kiosk device token), and at least
  two children checked in today across two different groups at the same location (feature 010's
  check-in flow).
- At least one of those children has `AllergySeverity = Severe` set (feature 006) and a
  `HealthRecord` row with `RecordType = MedicationStanding` valid for today (feature 013c).

## Scenario 1 — Caregiver views own-group meal list (US1)

1. Log into `mobile/` as a caregiver (or paired kiosk device) for a specific room/group.
2. From the room home screen, open the meal list.
3. **Expect**: only children checked in to this device's own group appear (FR-011); a child with
   no `child_meal_preferences` row shows "Geen voorkeur" (FR-005); the child with
   `Severe` allergy severity shows a RED indicator with a paired icon, not color alone (FR-006,
   FR-007); the child with standing medication shows a pill icon (FR-008).
4. Check a child out (feature 010). Reopen the meal list.
5. **Expect**: that child no longer appears (FR-004).

## Scenario 2 — Director views and prints the full-location list (US2)

1. Log into `web/` as a director, navigate to "Maaltijdenlijst" from the sidebar.
2. **Expect**: children from both groups appear, grouped by group/section (FR-003).
3. Click Print (or use the browser's print preview).
4. **Expect**: allergy severity remains distinguishable in the grayscale preview via icon shape
   alone (FR-007, SC-003).

## Scenario 3 — Director edits a child's meal preferences (US3)

1. From a child's profile in `web/`, open the meal-preference editor.
2. Set texture to `mixed`, dietary tags to `["halal"]`, portion size to `small`, and a note.
   Save.
3. **Expect**: `200` response with the saved values; reopening the Maaltijdenlijst page shows
   this child with the new texture/dietary/portion values (FR-002).
4. Update only the texture field, leaving other fields as previously set.
5. **Expect**: only texture changes; dietary tags/portion/notes remain as previously saved
   (partial-upsert semantics, data-model.md).

## Scenario 4 — "Inclusief verwacht" toggle (US4)

1. Ensure a child has an active contract covering today's weekday at this location but has not
   been checked in yet today.
2. Open the meal list (web or mobile) with the toggle off.
3. **Expect**: that child does not appear anywhere (FR-004, FR-009).
4. Enable "Inclusief verwacht".
5. **Expect**: the child appears in a separate "Verwacht" section, visually distinct from the
   present-children groups (FR-009).

## Offline check (mobile, FR-014)

1. Load the caregiver-tablet meal list once while online (populates the cache).
2. Put the device in airplane mode. Reopen the meal list.
3. **Expect**: the previously-cached meal list still renders — no network error surfaced for
   this read (research.md R6).

## Validation checks (contracts/meal-list-api.md)

- Attempt `PUT /api/children/{childId}/meal-preferences` with an `additionalNotes` value over
  the max length. **Expect**: `422` validation error, no record written.
- Attempt `GET /api/locations/{locationId}/meal-list` as a device token paired to a different
  location's group. **Expect**: response is scoped to the device's own group regardless of the
  requested `locationId` (or `404`, per the endpoint's implementation choice — never another
  group's data).
- Attempt the same request as a parent-role account. **Expect**: `403` — this endpoint is never
  reachable by the parent app (FR-012).
