# Research: Meal List (Maaltijdenlijst)

## R1 — Allergen severity source of truth

**Decision**: Read `Child.AllergySeverity` (`Mild`/`Moderate`/`Severe`, feature 006) as the sole
source for the meal list's RED/AMBER/GREY flag. `HealthRecord.RecordType = Allergy` (013c) rows
are not additionally queried for this feature.

**Rationale**: `HealthRecord` has no severity field — it's a structured detail record (title,
description, validity window) with no RED/AMBER/GREY equivalent. `Child.AllergySeverity` is
already the field every existing consumer (006's child summary, 013c's dashboards) treats as
authoritative for severity. Re-deriving severity from free-text `HealthRecord.Description` would
be unreliable and duplicate an existing, already-structured field.

**Alternatives considered**: Add a new severity field to `HealthRecord` — rejected as unnecessary
schema churn; `Child.AllergySeverity` already exists and is authoritative today.

## R2 — "Expected" children aggregation

**Decision**: Compute "expected but not checked in" by joining active `Contract`s (007) whose
`ContractedDays` include today's weekday against today's `AttendanceRecord` (010) for the same
child/location, excluding any child who already has a record for today. No new table.

**Rationale**: This aggregation doesn't exist anywhere in the codebase yet (confirmed by
searching for any "expected"/projected-presence concept in `ChildCare.Application`). Feature 012's
"projected on-duty count" for staff is the closest precedent for a projection-over-contract
pattern, but it's staff-side, not child-side — this feature builds the child-side equivalent
using the same underlying idea (contract terms projected forward), not a shared implementation.

**Alternatives considered**: Track "expected" as a materialized daily job — rejected; the
attendance/contract data needed is already query-able live, and pre-computing it would add a
staleness risk (a contract could be amended intraday) for no measurable performance benefit at
this feature's scale (≤40 children/location).

## R3 — Standing medication indicator

**Decision**: A child's pill icon is shown when a `HealthRecord` row exists with
`RecordType = MedicationStanding`, `DeletedAt == null`, and today's date falls within
`ValidFrom`/`ValidUntil` (treating a null bound as open-ended on that side).

**Rationale**: `MedicationStanding` is an existing `HealthRecordType` enum value (013c) —
this feature is its first real consumer beyond the health-records CRUD screen itself.

**Alternatives considered**: Read from `child_events` (009) `medication` events instead — rejected;
`child_events.medication` records a specific administered dose (a point-in-time event), not an
ongoing standing-order state, which is what the meal-list's "kitchen prepares a reminder" use
case actually needs.

## R4 — Caregiver-tablet group scoping

**Decision**: Scope the caregiver-tablet meal-list read to the device's own `GroupId` claim
(`DeviceTokenClaims.GroupId`, feature 008a), the same claim `RoomShiftEndpoints` and other
caregiver-tablet endpoints already read directly off the authenticated device token — no new
scoping mechanism.

**Rationale**: Feature 008a's device token already carries `location_id`/`group_id` claims
specifically so downstream endpoints don't need a separate `StaffLocationEligibility` lookup for
device-authenticated requests. Reusing this is both the established pattern and strictly simpler
than adding a new eligibility check.

**Alternatives considered**: Scope by `StaffLocationEligibility` (the per-caregiver-account
pattern) — rejected; that pattern applies to individually-authenticated staff actions (PIN
confirmation), not device-token-authenticated reads, which is what a shared room tablet uses for
routine viewing per Constitution's Technology Stack Constraints.

## R5 — Print approach

**Decision**: A CSS `@media print` stylesheet on the director-web meal-list page — no PDF
generation.

**Rationale**: The BACKLOG prompt is explicit ("Print button; CSS print stylesheet; no PDF
needed"), and this is the first genuinely print-only screen in the codebase (feature 007's
`IContractPdfGenerator`/QuestPDF precedent is for a retained legal document, not an ephemeral
daily kitchen sheet). Introducing QuestPDF here would be the wrong tool for a same-day,
throwaway operational document with no signature/audit requirement.

**Alternatives considered**: Reuse `IContractPdfGenerator`'s QuestPDF pattern — rejected as
unnecessary weight for a page that only needs browser-native print.

## R6 — Mobile offline read pattern

**Decision**: Follow `mobile/services/healthSummary.ts`'s exact fetch-then-cache-fallback shape
(`getCached`/`setCached` from `readCache.ts`) for the new `mealList.ts` service — fetch on load,
cache on success, fall back to cache on failure, distinguishing "loaded" from "unavailable" as a
tagged union.

**Rationale**: This is an established, already-tested pattern in this codebase for exactly this
class of caregiver-tablet read-only screen (013c's health summary being the most recent
precedent) — no reason to invent a second shape.

**Alternatives considered**: Register `meal_list` as an `offline_queue` entity type (feature 008's
sync engine) — rejected; that infrastructure exists for offline *writes*, and this feature has no
write path on the caregiver tablet (meal-preference edits are director-web only, per spec).

## R7 — Migration application

**Decision**: Author `AddChildMealPreferences` as a standard EF Core migration; generate its SQL
script and apply it manually to each tenant schema (this repo's existing `migrate-tenants`-style
rollout), per `CLAUDE.md`'s "EF Core never auto-migrates in production" convention and
Constitution VI.

**Rationale**: Same convention every prior migration-adding feature (012a, 013c, 006a) has
followed — no exception applies here (this is not new-tenant-schema provisioning, Constitution
VI's only carve-out).

**Alternatives considered**: None — this is a fixed project convention, not an open design
choice.

## R8 — Test-suite regression check

**Decision**: Extend `TenantMigrationRolloutTests`' schema-revert helper for the new
`child_meal_preferences` table's FK to `children`, following the same fix every migration-adding
feature since 003 has needed (012a, 013c, 006a's shipped-notes all flag this). A second,
separate test — `LegacyVaccinationMigrationTests`' `RevertToPreVaccineHealthRecordsAsync` —
needed the identical class of fix (drop the new table, add its migration name to the
`__EFMigrationsHistory` cleanup `LIKE` clause), discovered only when that test actually failed
after this feature's migration shipped, not by inspection beforehand — its own doc comment
explicitly warned "this must be extended again for any future migration," and this is that next
migration.

**Rationale**: This is a known, recurring gap in any test that reverts a tenant schema to an
earlier migration state whenever a new tenant-schema table is added afterward — `TenantDbContext`'s
custom `MigrateAsync()` computes "pending" as the range from the last-*applied* migration to
latest, so leaving a chronologically-later migration's history row in place while an earlier one
is reverted collapses that range to empty and `MigrateAsync()` silently no-ops. Checking every
test that performs this style of revert (not just `TenantMigrationRolloutTests`) avoids a
late-discovered CI failure — worth grepping for `RevertTo` calls generally on any future
migration-adding feature, not just the one test file prior shipped-notes name.

**Alternatives considered**: None — this is a mechanical, well-precedented fix.
