# Contract: Contracts API (`/api/children/{childId}/contracts`, `/api/contracts/*`)

All requests/responses are JSON except the PDF endpoint. All error bodies are `{ "errorKey": "..." }` (constitution Principle IV). Every route requires the `DirectorOnly` policy and is **not** tenant-exempt: `TenantMiddleware` (feature 002) resolves `ICurrentTenantService`/`ITenantDbContext` before any handler runs.

## `GET /api/children/{childId}/contracts`

- `200` — `ContractResponse[]`, full history (all statuses), ordered most-recent-first (FR-017).
- `404 errors.child.not_found` — reused from feature 006, no duplicate key invented.

## `GET /api/contracts/{id}`

- `200` — `ContractResponse`.
- `404 errors.contract.not_found` — no contract with that id in the caller's own tenant schema.

## `POST /api/children/{childId}/contracts`

Request (`CreateContractRequest`): `locationId`, `startDate`, `endDate?`, `contractedDays[]` (each `{ weekday, startTime, endTime }`), `dailyRateCents`, `consent { photosInternal, photosWebsite, photosSocialMedia, videoInternal, photosPress }`.

- `201` — `ContractResponse` with `status: "draft"` (FR-001).
- `404 errors.child.not_found` / `404 errors.location.not_found` (reused — also covers a deactivated location, FR-004a, matching feature 006's CHK003 precedent).
- `422 errors.validation` — missing/invalid fields: `errors.contract.weekday_required` (empty `contractedDays`), `errors.contract.weekday_invalid` (Saturday/Sunday or duplicate weekday), `errors.contract.time_range_invalid` (`startTime >= endTime` for any day), `errors.contract.daily_rate_invalid` (`dailyRateCents <= 0`), `errors.contract.start_date_required`, `errors.contract.end_date_before_start_date`.

## `PUT /api/contracts/{id}`

Request: same shape as create.

- `200` — updated `ContractResponse`. Only valid while `status == "draft"` (edits apply in place, no versioning — research.md R5).
- `404 errors.contract.not_found`
- `409 errors.contract.not_draft` — the contract is `active` or `ended`; use amend or terminate instead.
- `422 errors.validation` — same field rules as create.

## `POST /api/contracts/{id}/activate`

- `200` — `ContractResponse` with `status: "active"` (FR-003).
- `404 errors.contract.not_found`
- `409 errors.contract.not_draft` — only a `draft` contract can be activated.
- `409 errors.contract.already_active_at_location` — the child already has another `active` contract at this contract's location (FR-004).
- `409 errors.contract.day_overlap` — one or more of this contract's weekdays is claimed by another currently `active` contract for the same child at a different location (FR-005). Checked and committed atomically per child (FR-006, research.md R2) — under two concurrent activation requests for the same child, exactly one succeeds and the other receives this error.

## `POST /api/contracts/{id}/amend`

Request (`AmendContractRequest`): `effectiveStartDate`, plus the same terms fields as create (`locationId`, `endDate?`, `contractedDays[]`, `dailyRateCents`, `consent`).

- `201` — the new successor `ContractResponse` (`status: "active"`, `previousContractId` set to the amended contract's id). The original contract's `endDate` is set to the day before `effectiveStartDate` and its `status` becomes `ended` (FR-007).
- `404 errors.contract.not_found`
- `409 errors.contract.not_active` — only an `active` contract can be amended.
- `422 errors.contract.amendment_start_date_invalid` — `effectiveStartDate` is on or before the current contract's own `startDate`.
- `409 errors.contract.already_active_at_location` / `409 errors.contract.day_overlap` — same checks as activation (FR-008), excluding the contract being ended from the overlap comparison.

## `POST /api/contracts/{id}/terminate`

Request (`TerminateContractRequest`): `endDate`.

- `200` — `ContractResponse` with `status: "ended"`, `endDate` set, no successor created (FR-009a).
- `404 errors.contract.not_found`
- `409 errors.contract.not_active` — only an `active` contract can be terminated.
- `422 errors.contract.termination_date_invalid` — `endDate` is before the contract's own `startDate`.

## `GET /api/contracts/{id}/pdf`

Query params: `locale` (optional — `nl`/`fr`/`en`, defaults to `nl` when omitted or unrecognized — FR-011).

- `200` — `application/pdf` body containing the contract's current terms, all five consent choices, status, and a signature line, with static labels rendered in the requested locale. Available for a contract in any status, including `draft` (US4 Scenario 2).
- `404 errors.contract.not_found`
