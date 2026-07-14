# Research: Monthly Menu

Phase 0 output. All items below resolve a design question the spec left to planning (per
spec.md's Technical Requirements) rather than a `[NEEDS CLARIFICATION]` marker — none remained
after `/speckit-clarify`.

## R1 — Reuse `MealPreference` (013d) as the single write target for approved requests

**Decision**: `ApproveMealPreferenceChangeRequestCommand`'s handler sends the existing
`UpsertMealPreferenceCommand` (`backend/ChildCare.Application/MealPreferences/
UpsertMealPreferenceCommand.cs:13`) via `IMediator`, rather than writing to
`ChildCare.Domain.Entities.MealPreference` directly.

**Rationale**: `UpsertMealPreferenceCommand` already implements the create-or-update-by-`ChildId`
upsert semantics this feature needs (`UpsertMealPreferenceCommandHandler`, lines 53-77 —
find-or-create, null-coalesce merge). Mirrors the existing precedent of one command handler
calling another via `IMediator` (`SubmitDayReservationCommand.cs:137-139` calling
`MarkAbsentCommand` for auto-approved absences) rather than duplicating the target entity's write
logic in a second place.

**Alternatives considered**: A second direct EF write against `MealPreferences` in the approve
handler — rejected, since it would create two independent write paths for the same table and risk
them drifting (e.g. one enforcing a validation rule the other forgets).

## R2 — Preference-change request authorization mirrors day-reservations (013a) exactly

**Decision**: Every parent-facing meal-preference-request endpoint resolves the requester via
`ICurrentParentContactResolver.ResolveAsync(tenantUserId, ct)` then checks
`db.ChildContacts.AnyAsync(cc => cc.ContactId == contact.Id && cc.ChildId == request.ChildId,
ct)`, the identical two-step check used in `SubmitDayReservationCommand.cs:53-59` and
`GetReservationAvailabilityQuery.cs:37-40`.

**Rationale**: This is already "the shared authorization primitive" for parent-facing writes
(`ICurrentParentContactResolver.cs:5-11`'s own doc comment) — no dedicated
`IParentChildAccessService` exists, and introducing one for this feature alone would diverge from
every other parent-facing handler in the codebase.

**Alternatives considered**: A new shared access-check service — rejected as unnecessary
abstraction; the two-line inline check is the established pattern and duplicating it a third time
is consistent with, not a violation of, this codebase's actual convention (extracting it is a
separate, cross-cutting refactor out of this feature's scope).

## R3 — Decision notification reuses `IExpoPushSender` + `Notification` row, mirroring `DayReservationNotificationService`

**Decision**: A new `MealPreferenceRequestNotificationService` (in
`ChildCare.Application/MealPreferenceRequests/`) follows `DayReservationNotificationService.cs`'s
exact shape: resolve the parent `Contact` for the request's `ChildId`/`RequestedBy`, write an
in-app `Notification` row when a linked `TenantUserId` exists, and attempt an Expo push only if
`Contact.PushToken` is set (wrapped in try/catch, logged not thrown — lines 71-84 of the
precedent). A rejection with a non-blank `DecisionNotes` uses a distinct i18n `BodyKey`
(`meal_preference_requests.rejected_body_with_note`) from a bare rejection
(`meal_preference_requests.rejected_body`), never interpolating a possibly-null value — the exact
pattern at `DayReservationNotificationService.cs:45-49,56-66`.

**Rationale**: Spec Clarification #1 explicitly calls for reusing this pattern rather than
inventing a new one. A new `NotificationType.MealPreferenceRequestDecided` value is added
alongside the existing `NewMessage, Announcement, TemperatureAlert, DayReservationDecided`
(`NotificationType.cs:5-8`).

**Alternatives considered**: None — the spec's own Clarifications section already settled this.

## R4 — New parent-facing closure-day read reuses `IClosureCalendarReader`, not `ListClosureDaysQuery`

**Decision**: The parent-facing monthly-menu read calls
`IClosureCalendarReader.ListPublishedClosureDatesAsync(locationId, from, to, ct)`
(`IClosureCalendarReader.cs:6-7`) for the displayed month's date range, not
`ListClosureDaysQuery` (`ClosureCalendarEndpoints.cs`'s existing `DirectorOnly` year-scoped query,
which also joins `ClosureNotificationDeliveries` — director-facing delivery-status data a parent
has no use for and no authorization to see).

**Rationale**: `IClosureCalendarReader` is already the interface `SubmitDayReservationCommand`
uses for a parent-triggered closure check (`SubmitDayReservationCommand.cs:86`) — it is the
existing "closure days, no director-only baggage" seam. No parent-scoped closure read exists
today (`GET /api/closures` is unconditionally `DirectorOnly` —
`ClosureCalendarEndpoints.cs:16`); this feature adds the first one, as an additive gap consistent
with the pattern prior features' shipped-notes describe (007a, 009, 013d all added a small
additive read endpoint discovered only once the UI needed it).

**Alternatives considered**: Reusing `ListClosureDaysQuery` directly and filtering its response
client-side — rejected: it is `DirectorOnly`-gated and returns director-facing fields (delivery
status) that would need stripping either way, so calling the narrower reader interface directly is
simpler and correctly scoped from the start.

## R5 — Location resolution for a parent-facing menu read: every active-contract location, not one

**Decision**: `GetParentMonthlyMenusQuery` resolves the distinct set of `LocationId`s from every
`Contract` with `Status = Active` across every child linked to the requesting parent
(`db.ChildContacts` → `Contract.ChildId`/`Contract.Status`), the same aggregation
`SubmitDayReservationCommand.cs:74-78` already performs for a single child's exchange-request
closure check, extended here across all of a parent's linked children.

**Rationale**: Spec Clarification #2. No ready-made "this parent's location(s)" resolver or
endpoint exists (`GetParentChildrenQuery`'s response has no `LocationId` — research finding for
this plan); this query is the first to need it, built directly from `Contract`, not a new
intermediate table.

**Alternatives considered**: Only ever showing one implicit "primary" location — rejected per the
spec's explicit clarification; would silently hide a real published menu for a multi-location
child, the same class of gap 013a's own code comments warn against.

## R6 — One pending request per child, enforced in the command handler

**Decision**: `SubmitMealPreferenceChangeRequestCommandHandler` checks
`db.MealPreferenceChangeRequests.AnyAsync(r => r.ChildId == request.ChildId && r.Status ==
MealPreferenceChangeRequestStatus.Pending, ct)` before inserting, returning a distinct
`DuplicatePendingRequest` failure (mapped to `409 Conflict`) rather than a `422` validation error
— this is a state conflict (an existing row), not a shape/field validation failure, matching this
codebase's `422`-for-FluentValidation-only convention established in feature 013h's shipped-notes
correction.

**Rationale**: Spec Clarification #3 and FR-012. Enforced in the handler (not a DB unique
constraint) because "one pending row" is a status-dependent invariant, not a static uniqueness
constraint EF/Postgres can express directly against a mutable `Status` column without a partial
unique index; a partial unique index (`WHERE status = 'pending'`) is the stronger option but is
deferred as unnecessary for this feature's scale (a handful of requests per tenant at a time) —
noted as a possible hardening follow-up, not required now.

**Alternatives considered**: A Postgres partial unique index (`CREATE UNIQUE INDEX ... WHERE
status = 'pending'`) — considered but not adopted for this feature; the application-layer check is
sufficient at this scale and matches how every other single-active-record invariant in this
codebase is enforced (e.g. `MealPreference`'s one-row-per-child is a full unique index because it
is a permanent 1:1 relationship, whereas "one pending request" is a transient state that clears
itself on every decision).

## R7 — Migration and `TenantMigrationRolloutTests` follow the established pattern

**Decision**: One new tenant migration
`{yyyyMMddHHmmss}_AddMonthlyMenuAndMealPreferenceRequests.cs` adds three tables
(`monthly_menus`, `monthly_menu_days`, `meal_preference_change_requests`).
`backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs`'s `RevertToPreExtensionSchemaAsync`
(starting line 142) gets three new `DROP TABLE` statements in FK-dependency order
(`monthly_menu_days` before `monthly_menus`, since the former FKs to the latter;
`meal_preference_change_requests` is independent, FKs only to `children`) plus an
`__EFMigrationsHistory` clause for the new migration name.

**Rationale**: Every migration-adding feature since 012a has needed this exact fix
(012a, 013c, 006a, 013d, 013g, 013h per their shipped-notes) — treated as a known, mechanical step
rather than something to rediscover. `LegacyVaccinationMigrationTests.cs` is scoped specifically
to feature 013c's own backfill migration and does not need touching (research finding for this
plan) — no legacy-data backfill applies to this feature's new tables.
