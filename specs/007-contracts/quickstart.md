# Quickstart: Enrolment Contracts

## Prerequisites

- Docker Postgres running locally
- Org A (slug `org-a`) registered via feature 001, director access token
- Org A has two locations created via feature 004's `POST /api/locations` (Location A, Location B)
- Org A has a child created via feature 006's `POST /api/children`
- `dotnet build backend/ChildCare.sln` succeeds

## Scenario 1 — Create and activate a contract (User Story 1, SC-001)

1. `POST /api/children/{childId}/contracts` with `locationId` (Location A), `startDate: "2026-01-01"`, `contractedDays: [{weekday: "Monday", startTime: "08:00", endTime: "17:00"}, {weekday: "Tuesday", startTime: "08:00", endTime: "17:00"}]`, `dailyRateCents: 3500`, `consent: {photosInternal: true, photosWebsite: false, photosSocialMedia: false, videoInternal: false, photosPress: false}` → `201`, `status: "draft"`.
2. `POST /api/contracts/{id}/activate` → `200`, `status: "active"`.
3. `POST /api/contracts/{id}/activate` again for a **second** draft contract at the **same** location for the same child → `409 errors.contract.already_active_at_location`.

## Scenario 2 — Split-location enrolment (User Story 2, SC-002, SC-005)

1. With the Location A contract from Scenario 1 active (Mon+Tue), create a second contract for the same child at Location B covering Wed+Thu, then activate it → `200`, `status: "active"` — both contracts are now simultaneously active.
2. Create a third contract at a third location covering Tue+Wed, then attempt to activate it → `409 errors.contract.day_overlap` (Tuesday conflicts with the Location A contract).
3. Concurrency check: create two draft contracts for the same child that would conflict with each other (not with an already-active one), then fire both `POST /api/contracts/{id}/activate` requests at the same time → exactly one returns `200`, the other returns `409 errors.contract.day_overlap`. Repeat across at least 20 trials (SC-002) to confirm no race ever lets both succeed.

## Scenario 3 — Amend a contract's terms (User Story 3)

1. With the Location A contract active (Mon+Tue, rate 3500), `POST /api/contracts/{id}/amend` with `effectiveStartDate: "2026-06-01"`, same `locationId`, `contractedDays` now Mon+Tue+Wed, `dailyRateCents: 4000`, same `consent` → `201`, a new contract `status: "active"`, `previousContractId` set.
2. `GET /api/contracts/{originalId}` → `status: "ended"`, `endDate: "2026-05-31"`.
3. `GET /api/children/{childId}/contracts` → both contracts appear, most-recent-first.

## Scenario 4 — Terminate a contract with no successor (User Story 3a)

1. With a second, independent active contract (e.g. the Location B one from Scenario 2), `POST /api/contracts/{id}/terminate` with `endDate: "2026-08-31"` → `200`, `status: "ended"`, no new contract created.
2. Create and activate a brand-new contract for the same child covering the same weekdays the terminated contract used → succeeds (terminated contracts don't count toward the day-overlap check).

## Scenario 5 — Generate a contract PDF (User Story 4, SC-004)

1. `GET /api/contracts/{activeContractId}/pdf` (no `locale` param) → `200`, `Content-Type: application/pdf`, body contains the child's name, location, contracted days/hours, daily rate, all five consent choices, and a signature line, with Dutch labels (the default).
2. `GET /api/contracts/{draftContractId}/pdf` → `200` (PDF still generated for a draft contract, clearly indicating draft status).
3. `GET /api/contracts/{activeContractId}/pdf?locale=fr` → `200`, same content with French labels.

## Scenario 6 — Deactivation guards (feature 004/006 extension points, research.md R3)

1. With an active contract at Location A, `POST /api/locations/{locationAId}/deactivate` → `409 errors.location.has_active_dependents`.
2. With an active contract for the child, `POST /api/children/{childId}/deactivate` → `409 errors.child.has_active_dependents`.
3. Terminate or let the contract end, then retry both deactivations → `200` in each case.

## Automated coverage

See `tasks.md` for the concrete task breakdown: `ContractLifecycleTests.cs` (Scenario 1), `ContractSplitLocationTests.cs` (Scenario 2, including the concurrency case), `ContractAmendmentTests.cs` (Scenario 3), `ContractTerminationTests.cs` (Scenario 4), `ContractPdfTests.cs` (Scenario 5), `ContractDeactivationGuardTests.cs` (Scenario 6). Tenant isolation extends the existing `TenantIsolationTests.cs` pattern (feature 002) with a `Contract`-specific case.
