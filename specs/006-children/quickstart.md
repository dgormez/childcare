# Quickstart: Child File Management

## Prerequisites

- Docker Postgres running locally
- Two organisations registered via feature 001's `POST /api/organisations/register` — Org A (slug `org-a`) and Org B (slug `org-b`), each with a director account and a valid access token
- Org A has at least one location created via feature 004's `POST /api/locations`
- `dotnet build backend/ChildCare.sln` succeeds

## Scenario 1 — Create a child file with medical information (User Story 1, SC-001/SC-002)

1. As Org A's director, `POST /api/children` with `firstName`/`lastName`/`dateOfBirth` only → `201`, response includes a new `id`, no contract or group required.
2. `POST /api/children` again, this time also including `allergiesDescription`, `allergySeverity: "Severe"`, `medicalConditions`, `dietaryRestrictions`, `gpName`, `gpPhone`, `healthInsuranceNumber` → `201`, all fields present on `GET /api/children/{id}`.
3. `GET /api/children` → both children appear, `deactivatedAt: null` on each.

## Scenario 2 — Link a shared contact to two siblings (User Story 2, SC-004)

1. `POST /api/contacts` with a parent's `firstName`/`lastName`/`phone`/`locale` → `201`, note the `id`.
2. `POST /api/children/{child1Id}/contacts` with that `contactId`, `relationship: "Mother"`, `canPickup: true`, `isPrimary: true` → `201`.
3. `POST /api/children/{child2Id}/contacts` (a sibling) with the **same** `contactId`, `relationship: "Mother"`, `canPickup: true`, `isPrimary: true` → `201` — no second `Contact` row is created.
4. `PUT /api/contacts/{contactId}` changing the phone number → `200`; `GET /api/children/{child1Id}` and `GET /api/children/{child2Id}` both reflect the new phone number.

## Scenario 3 — Assign a child to a group, then move them (User Story 3, FR-008/FR-008a)

1. `POST /api/groups` with `name: "Baby Room"`, `locationId` → `201`, note the `groupId`.
2. `POST /api/children/{childId}/groups` with that `groupId`, `startDate: "2026-01-01"` → `201`.
3. `POST /api/groups` with `name: "Toddler Room"`, same `locationId` → `201`, note the second `groupId`.
4. `POST /api/children/{childId}/groups` with the second `groupId`, `startDate: "2026-07-01"` → `201`; `GET /api/children/{childId}/groups` shows two entries — the first with `endDate: "2026-06-30"`, the second with `endDate: null`.

## Scenario 4 — Record a vaccine and see the due alert (User Story 4)

1. `POST /api/children/{childId}/vaccinations` with `vaccineName: "DTP"`, `dateAdministered: "2026-01-01"`, `nextDueDate` one year in the future → `201`; `GET /api/children/{childId}/vaccinations` shows `isDue: false`.
2. `POST /api/children/{childId}/vaccinations` with a `nextDueDate` in the past → `201`; that entry shows `isDue: true`.

## Scenario 5 — Deactivate and reactivate a child (User Story 5, SC-005)

1. `POST /api/children/{childId}/deactivate` → `200`, `deactivatedAt` set.
2. `GET /api/children` (default) → the deactivated child is absent; `GET /api/children?includeDeactivated=true` → present.
3. `GET /api/children/{childId}` (director, `includeDeactivated` implied by direct id lookup) → medical info, contacts, group history, and vaccination history are all still fully retrievable.
4. `POST /api/children/{childId}/reactivate` → `200`, `deactivatedAt` cleared; child reappears in the default list.

## Scenario 6 — Tenant isolation (constitution Principle I, FR-017)

1. Note a child `id` created in Org A (Scenario 1).
2. As Org B's director (different access token, different `tenant_id` claim), `GET /api/children/{orgAChildId}` → `404 errors.child.not_found`.
3. `GET /api/children` as Org B → Org A's children never appear.

## Automated coverage

All scenarios above should have a corresponding TestContainers-backed integration test (constitution Principle V) — see `tasks.md` for the concrete task breakdown: `ChildCrudTests.cs` (Scenario 1), `ChildContactTests.cs` (Scenario 2), `ChildGroupAssignmentTests.cs` (Scenario 3), `ChildVaccinationTests.cs` (Scenario 4), `ChildDeactivationTests.cs` (Scenario 5). Scenario 6 extends the existing `TenantIsolationTests.cs` pattern (feature 002) with a `Child`-specific case.
