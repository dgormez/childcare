# Phase 0 Research: Staff App (Personal Rota & Leave)

## R1 — Consolidate into `StaffSchedule` rather than a parallel `staff_assignments` table

**Decision**: Extend the existing `StaffSchedule` entity (`backend/ChildCare.Domain/Entities/
StaffSchedule.cs`, feature 012) in place with `Status`, `CoverStaffId`, `Notes`, `CreatedBy`,
`IsPublished`, `PublishedAt`, rather than introducing the BACKLOG prompt's originally-sketched
`staff_assignments` table.

**Rationale**: `StaffSchedule` already represents exactly the same real-world fact this
feature's `staff_assignments` sketch wanted ("who works where, when") — a second table would
be two sources of truth for one concept, the same anti-pattern feature 025's shipped-note
warned against when `ProcessPaymentWebhookCommand` briefly duplicated `MarkInvoicePaidCommand`'s
logic instead of reusing it. The BACKLOG prompt's own instruction ("extends or replaces
staff_schedules from 012 — audit 012's schema and consolidate into this model") already pointed
at this outcome; the sketch's mismatched table/column names (`staff_members`, `users`) were
written without reading the actual 012 implementation.

**Alternatives considered**: A new parallel table with a migration to move existing rows —
rejected as pure added complexity with no benefit, since this is a pre-revenue project with no
production tenant data to migrate around (see R3).

## R2 — `StaffProfile.ContractedDays` storage shape

**Decision**: `List<DayOfWeek>` on `StaffProfile`, mapped as a native Postgres `text[]` column
via an EF Core value converter + `ValueComparer`, mirroring `MealPreference.DietaryType`'s
existing pattern (`TenantDbContext.cs`).

**Rationale**: `Contract.ContractedDays` (feature 007) uses an `OwnsMany(...).ToJson()` owned
collection specifically because each entry also carries independent `StartTime`/`EndTime` per
weekday. This feature's field has no sub-fields — it's purely "which weekdays does this staff
member normally work" — so the codebase's own precedent for a flat enum list
(`MealPreference.DietaryType`, converted to `text[]`) is the closer match, not the richer
owned-JSON pattern. Reusing `System.DayOfWeek` as the element type (rather than inventing a new
enum) avoids a redundant weekday representation next to the one `ContractedDay.Weekday` and
`DateOnly.DayOfWeek` already use throughout `PlannedDurationCalculator.cs` and similar code.

**Alternatives considered**: Reusing `Contract.ContractedDay` itself — rejected, since it
carries per-day `StartTime`/`EndTime` this field doesn't need (a staff member's daily hours
already live on each `StaffSchedule` row, not on a fixed weekly template); a bespoke
`Weekday` enum — rejected, `System.DayOfWeek` is already the established comparison type
everywhere else in this codebase.

## R3 — Reconciling `IsAbsent`/`AbsenceReason` with the new `Status` field

**Decision**: Add `Status` (`StaffScheduleStatus`: `Scheduled` default / `Confirmed` / `Absent`
/ `Covered`) as the new single source of truth for an assignment's state, and drop `IsAbsent`
as a persisted column — `IsAbsent` becomes a computed convenience (`Status == Absent`) rather
than a second stored boolean. `AbsenceReason` (`Sick`/`Leave`/`Holiday`) is kept as a nullable
column, populated only when `Status == Absent`, and is extended with no new values — a
`StaffLeaveRequest.Type` of `sick`/`annual`/`other` maps onto it as `Sick`/`Leave`/`Leave`
(`other` reuses the existing generic `Leave` reason; adding a fourth `AbsenceReason` value for
"other" was considered and rejected as needless granularity nothing reads differently).

**Rationale**: The spec's own corrected premise (R1, and spec.md's Key Entities) explicitly
calls out avoiding "two parallel absence representations." Since this is a pre-revenue project
with zero production tenant data (confirmed — no deployed tenant schema holds real rows yet;
`ProvisioningStatus`-tracked test/dev tenants only), the EF migration can safely rename/backfill
in place rather than needing a dual-write transition window a live product would require.

**Alternatives considered**: Keep `IsAbsent` as a redundant persisted column, kept in sync with
`Status` by application code — rejected; two columns that must always agree is exactly the
"two parallel representations" problem being fixed, and every future `Status`-writing code path
would need to remember to also update `IsAbsent`.

## R4 — Publish granularity and the "already-published week" bypass

**Decision**: `IsPublished`/`PublishedAt` are per-row (per `StaffSchedule` entry), but the
`PublishScheduleWeekCommand` write flips every row for one `(LocationId, WeekStart)` at once —
so behaviorally publish operates at week granularity (matching feature 012's existing
Monday-anchored `copy-week` convention), while the underlying column stays per-row for a simple
reason: a row created or changed *after* a week is already published (sick-cover reassignment,
last-minute change) must default to visible immediately, not wait for a future re-publish.
`ReportSickCommand`/`AssignCoverCommand`/any update to a row whose week is already published
set `IsPublished = true` on the affected row(s) directly, bypassing the draft gate.

**Rationale**: Spec.md's Edge Cases and User Story 3 both require last-minute changes to reach
staff immediately — a per-week-only publish flag (with no way to distinguish an
already-visible week from a specific new row within it) can't express "this one new row is
visible even though I'm not re-publishing the whole week." Per-row `IsPublished` with a
week-scoped bulk-set command gets both behaviors from one column.

**Alternatives considered**: A separate `PublishedRosterWeek` table with a `(LocationId,
WeekStart)` primary key and `GetMyScheduleQuery` joining against it — rejected as more moving
parts for the same outcome; would also need its own special-case logic for "this row is newer
than the week's publish timestamp," which the per-row flag gets for free.

## R5 — Sick-cover eligible-candidate list

**Decision**: `GetSickCoverCandidatesQuery` reuses `StaffLocationEligibility` (exact same
`AnyAsync` check `CreateStaffScheduleCommand`/`UpdateStaffScheduleCommand` already use) filtered
to staff with no other `StaffSchedule` row overlapping the same date/time window at any
location (reusing `OverlapCheck.ExistsAsync`, the same private helper feature 012's create/
update commands already share) and excluding deactivated staff profiles, mirroring
`GetProjectedOnDutyQuery`'s existing exclusion pattern.

**Rationale**: Both eligibility and overlap-detection already exist as tested, shared code from
feature 012 — this query is a read composed from two existing checks, not new logic.

**Alternatives considered**: A bespoke "who's free today" heuristic ignoring existing
assignments — rejected, would let the director accidentally double-book a replacement at a
second location the same day, defeating the query's whole purpose.

## R6 — Push notification lockstep extension

**Decision**: Add `SchedulePublished`, `AssignmentChanged`, `LeaveRequestDecided` to the backend
`NotificationType` enum (`backend/ChildCare.Domain/Enums/NotificationType.cs`). Since
`staff-mobile` is a brand-new Expo project with no existing notification screen, its TS union
and `ICON_BY_TYPE`/navigation map are authored fresh (mirroring `parent-mobile/types/index.ts`
and `notifications.tsx`'s existing shape) rather than extended, covering only the notification
types relevant to a staff member (the three new ones — a staff member never receives a parent-
facing type like `InvoiceSent`).

**Rationale**: Matches this codebase's established lockstep pattern (feature 015's shipped-note:
enum + TS union + icon/nav map must move together). `staff-mobile` doesn't need the parent app's
full type union — only the subset it can actually receive — so its union is a fresh, narrower
list rather than a copy of `parent-mobile`'s.

**Alternatives considered**: Sharing one TS type-definitions package across `parent-mobile` and
`staff-mobile` — rejected as out of scope; this codebase has no shared-package setup across its
client apps yet (`design-decisions.md`'s color-tokens entry documents this as a known,
deliberately-deferred gap), so introducing one here for two enum-adjacent files would be a
larger, unrelated refactor.

## R7 — `staff-mobile` scaffold baseline

**Decision**: Scaffold `staff-mobile/` from `parent-mobile/`'s existing project conventions
(Expo Router, NativeWind/Tailwind, `theme/colors.js` token set copied verbatim per
`design-decisions.md`'s "one hand-maintained copy per platform" documented pattern, i18next +
`expo-localization`, openapi-fetch client generation, Jest + `@testing-library/react-native`),
not `mobile/`'s (caregiver tablet — kiosk/landscape/shared-device/PIN model, structurally wrong
fit for a personal phone app).

**Rationale**: `staff-mobile` is a personal, portrait, individually-authenticated phone app —
the same category as `parent-mobile`, not the shared kiosk tablet. `parent-mobile` is the
closer, already-proven template for this shape of app (personal JWT auth, `expo-notifications`
push, offline cache-fallback pattern from feature 013c).

**Alternatives considered**: Extending `mobile/` (caregiver tablet) with a "personal mode" —
rejected outright; `Workflows/classroom-operations.md`'s own design principle (feature 008a)
states the shared kiosk tablet structurally has no personal session to host this on, which this
feature's own spec.md Product Context already cites as the reason a new app is needed at all.
