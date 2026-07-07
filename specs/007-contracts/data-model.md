# Phase 1 Data Model: Enrolment Contracts

## `Contract`

Table: `contracts`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `ChildId` | `Guid` | FK → `children.Id` |
| `LocationId` | `Guid` | FK → `locations.Id` |
| `PreviousContractId` | `Guid?` | FK → `contracts.Id` (self), nullable. Set on the successor contract created by an amendment (research.md R5); `null` for a fresh first-ever contract or for a contract that has no predecessor. |
| `StartDate` | `DateOnly` | Required. May be in the past (Clarifications — digitizing pre-existing enrolments). |
| `EndDate` | `DateOnly?` | Nullable = open-ended while `Active`/`Draft`; set when the contract transitions to `Ended` (via amendment or termination). |
| `ContractedDays` | owned collection, JSONB (`contracted_days`) | 1–5 `ContractedDay` entries (Monday–Friday only), each with its own `StartTime`/`EndTime` (Clarifications). |
| `DailyRateCents` | `int` | Whole-number cents, > 0 (FR-012). Parental contribution, not the gross KDV rate. |
| `Status` | `ContractStatus` (enum) | `Draft` \| `Active` \| `Ended`. |
| `Consent` | owned type, JSONB (`consent`) | `ContractConsent { PhotosInternal, PhotosWebsite, PhotosSocialMedia, VideoInternal, PhotosPress }`, all `bool`, default `false`. |
| `TariefCode` | `string?` | Reserved for Phase 3 IKT. Always `null` in this feature (FR-013). |
| `RateValidUntil` | `DateOnly?` | Reserved for Phase 3 IKT. Always `null` in this feature (FR-013). |
| `CreatedAt` | `DateTime` | UTC. |
| `UpdatedAt` | `DateTime` | UTC. |

**Indexes**: `(ChildId, Status)` — the day-overlap/one-active-per-location checks always filter by child and active status first. `(LocationId, Status)` — supports the location-deactivation guard's lookup.

**State transitions**:

- `Draft` → `Active` via `ActivateContractCommand` (FR-003), subject to FR-004 (one active per location) and FR-005/006 (day-overlap, locked per child).
- `Active` → `Ended` via `AmendContractCommand` (creates a new `Draft`-then-immediately-activated successor with `PreviousContractId` pointing back) or `TerminateContractCommand` (no successor).
- `Draft` terms are mutable in place via `UpdateContractCommand`; `Active`/`Ended` contracts are never mutated in place (FR-009's immutable-audit-trail guarantee).

### Owned type: `ContractedDay` (JSONB element, not a table)

| Field | Type | Notes |
|---|---|---|
| `Weekday` | `DayOfWeek` | Restricted to `Monday`..`Friday` by validation (System's `Saturday`/`Sunday` are rejected, not modeled out — KDV care days are Mon–Fri only per spec.md Assumptions). |
| `StartTime` | `TimeOnly` | Must be before `EndTime`. |
| `EndTime` | `TimeOnly` | Must be after `StartTime`. |

A `Contract` MUST have at least one `ContractedDay` and MUST NOT have two entries for the same `Weekday`.

### Owned type: `ContractConsent` (JSONB, single object, not a table)

| Field | Type | Notes |
|---|---|---|
| `PhotosInternal` | `bool` | Default `false`. |
| `PhotosWebsite` | `bool` | Default `false`. |
| `PhotosSocialMedia` | `bool` | Default `false`. |
| `VideoInternal` | `bool` | Default `false`. |
| `PhotosPress` | `bool` | Default `false`. |

## `ContractStatus` (enum)

`Draft` | `Active` | `Ended` — stored as a lower-cased string via `HasConversion`, matching every other status-like enum in this codebase (`UserRole`, `AllergySeverity`, `Gender`).

## New ports (Application layer)

- **`IContractPdfGenerator`**: `Task<byte[]> GenerateAsync(ContractPdfModel model, CancellationToken cancellationToken = default)`. `ContractPdfModel` carries the flattened, display-ready fields needed by the PDF (child name, location name, status, contracted days/hours, daily rate, all five consent flags, and `Locale` — `"nl"`/`"fr"`/`"en"`, defaulting to `"nl"` — spec.md FR-011) — the Infrastructure adapter has no direct DB access, matching `IProfilePhotoStorage`'s shape.
- **`IAdvisoryLockService`**: `Task<T> RunExclusiveAsync<T>(Guid key, Func<Task<T>> action, CancellationToken cancellationToken = default)` (research.md R2).

## Reused, unmodified from prior features

- `ILocationDeactivationGuard`, `IChildDeactivationGuard` (interfaces unchanged — this feature only adds implementations and DI registrations, research.md R3).
- `Child`, `Location` (referenced by FK only — no navigation collection added to either, consistent with `ChildGroupAssignment`'s FK-only relationship to `Child`/`Group`).
