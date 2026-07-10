# Quickstart: Waiting List Management

Validation scenarios proving the feature works end-to-end. Assumes a running local backend
(`dotnet run` in `backend/ChildCare.Api`) against a local/TestContainers PostgreSQL, a seeded
tenant with at least one `Location` (`MaxCapacity` set), an active `Contract` covering at
least one weekday, and a director account. See existing `specs/*/quickstart.md` files for the
shared local-setup prerequisites (unchanged by this feature).

## Scenario 1 — Director registers and reviews the waiting list (US1)

1. Log in as director; `POST /api/waiting-list` with child name/DOB, contact name, and a valid
   `locationId`.
2. `GET /api/waiting-list?locationId=...` (default `status=waiting`) — the new entry appears
   with `status: "waiting"` and `priority` after any existing entries.
3. Create a second entry with the same child first/last name and DOB as the first — both rows
   now appear with `isDuplicate: true`; creation is not blocked.
4. `GET /api/waiting-list?locationId=...&status=all` — confirm entries across every status are
   returned when explicitly requested.
5. Create an entry omitting `contactEmail`, `contactPhone`, and `requestedStartDate` — confirm
   it still saves successfully.

## Scenario 2 — Director reorders the priority queue (US2)

1. Create three `waiting` entries for the same location.
2. `POST /api/waiting-list/{id}/reorder` with `{ "direction": "up" }` on the last entry —
   confirm the returned list reflects the new priority order.
3. Create an entry for a *different* location and reorder it — confirm the first location's
   queue order is unaffected.
4. Transition an entry to `offered`, then attempt to reorder it — expect `409
   errors.waiting_list.not_reorderable_in_current_status`.

## Scenario 3 — Director moves an entry through its status lifecycle (US3)

1. `POST /api/waiting-list/{id}/status` with `{ "status": "offered" }` on a `waiting` entry
   that has a `contactEmail` — confirm status updates and (via test double/log) an email send
   was invoked.
2. Repeat on an entry with no `contactEmail` — confirm status updates with no email send
   attempted, and no error.
3. From `offered`, transition to `enrolled` — confirm success. From a separate `offered`
   entry, transition to `withdrawn` — confirm success.
4. From `offered`, transition back to `waiting` — confirm success and confirm no email send
   was invoked for this reverse transition.
5. From `enrolled` (or `withdrawn`), attempt any further transition — expect `409
   errors.waiting_list.invalid_status_transition`.
6. From `waiting`, transition directly to `withdrawn` — confirm success without requiring the
   `offered` step.

## Scenario 4 — Occupancy view honors closures (US4)

1. Seed `Location.MaxCapacity = 10` and one active `Contract` whose `ContractedDays` covers
   Monday/Wednesday, active for a date range including a target week.
2. `GET /api/waiting-list/occupancy?locationId=...&from=<Mon>&to=<Fri>` — confirm Monday and
   Wednesday show `freeCapacity: 9`, and Tuesday/Thursday/Friday show `freeCapacity: 10`
   (no contract covers those weekdays).
3. Create a `KdvClosureDay` (`Status = Published`) for that location on the Wednesday in range,
   then repeat the occupancy call — confirm Wednesday now returns `{ "freeCapacity": null,
   "closed": true }` instead of a numeric count.

## Scenario 5 — Director enrolls and links the child record (US5)

1. Transition an entry to `offered`, then `enrolled`.
2. `POST /api/waiting-list/{id}/link-child` with `{ "childId": "<existing child's id>" }` —
   confirm the entry's `childId` is set.
3. On a separate `enrolled` entry with no matching child, call the same endpoint with
   `{ "createNewChild": true }` — confirm a new `Child` record is created with the entry's
   name/DOB and the entry's `childId` is set to it.
4. Revisit an `enrolled` entry left unlinked — confirm `link-child` still succeeds later
   (no time-window restriction).

## Web (director) smoke check

1. Log in to `web/` as director, navigate to `/waiting-list`.
2. Confirm the list defaults to `waiting`-status entries for the first location, an empty
   location's list shows the empty-state sentence + icon (design-system.md), and the
   occupancy panel shows a closed day as "Closed" rather than a number.
3. Reorder an entry using the up/down buttons via keyboard only (Tab + Enter/Space, no mouse)
   — confirm it works without drag-and-drop.
4. Confirm all visible strings resolve via `next-intl` in at least NL and EN (switch locale).
