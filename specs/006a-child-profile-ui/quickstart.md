# Quickstart: Child Profile UI

## Prerequisites

- Backend running locally (`dotnet run` in `backend/ChildCare.Api`) against a Docker PostgreSQL
  instance with the new `AddPediatricianContactToChild` migration applied (script generated,
  run manually per this repo's convention — see research.md R7).
- `web/` running (`npm run dev`), API types regenerated (`npm run generate-api-client`) after
  the backend contract change.
- A director account and a caregiver-role account (or a paired kiosk device token) for the two
  personas this feature covers.

## Scenario 1 — Director creates a child from zero (US1)

1. Log into `web/` as a director, navigate to `/children`.
2. Click "New child" (FR-014).
3. Fill only First name, Last name, Date of birth. Submit.
4. **Expect**: record saves, director is redirected to `/children/{id}`'s "Profiel" tab
   (FR-003), all other fields shown empty.
5. Repeat, this time filling every optional field including both GP and Pediatrician name/phone.
6. **Expect**: all fields persist and are visible on the "Profiel" tab after reload.

## Scenario 2 — Director edits pediatrician contact independently of GP (US2)

1. Open an existing child's "Profiel" tab (a child with a GP contact already set, no
   pediatrician contact).
2. Enter a pediatrician name and phone. Save.
3. **Expect**: pediatrician fields persist; GP fields unchanged.
4. Clear the pediatrician fields. Save.
5. **Expect**: pediatrician fields cleared; GP fields still unchanged; save succeeds without
   requiring a GP value (no cross-field validation — Edge Cases).

## Scenario 3 — Tab navigation (SC-005)

1. On a child's detail screen, switch between "Profiel" and "Gezondheid".
2. **Expect**: no full page reload (client-side tab switch); existing 013c vaccine/health-record
   content on "Gezondheid" is unaffected.

## Scenario 4 — Caregiver views GP + pediatrician contact (US3)

1. Log into `mobile/` as a caregiver (or a paired kiosk device), open a child with both contacts
   set.
2. **Expect**: both GP and pediatrician contact shown, visually and linguistically distinct
   (FR-008, FR-010).
3. Open a child with only a pediatrician contact set (no GP).
4. **Expect**: only the pediatrician block renders; no empty/error state for the missing GP
   field.
5. Put the device in airplane mode after having previously loaded a child's screen once (so
   `CHILDREN_CACHE_KEY` holds that child). Reopen that child's screen.
6. **Expect**: cached GP/pediatrician contact still renders via the existing children-list cache
   (research.md R4) — no network error surfaced for this read.

## Validation checks (FR-013)

- Attempt to save a create/edit form with First name, Last name, or Date of birth empty.
  **Expect**: inline validation error, no request sent / request rejected with `400`, no record
  written.
- Attempt to save a pediatrician name longer than 200 characters, or phone longer than 30.
  **Expect**: inline validation error, matching the existing GP field's max-length behavior.
