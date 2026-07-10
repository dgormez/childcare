# Phase 0 Research: Caregiver Scheduling (Weekly Staff Rota)

## R1: Does feature 010's live BKR ratio actually consume `staff_schedules` data?

**Decision**: No. This feature does not wire `staff_schedules` into feature 010's live BKR
computation in any way.

**Rationale**: BACKLOG.md's original prompt for feature 012 states "the live BKR count in
feature 010 uses staff_schedules to know who is on duty right now" — but `staff_schedules`
didn't exist until this feature. Reading the actual shipped implementation
(`backend/ChildCare.Application/Attendance/GetBkrRatioQuery.cs`) shows the on-duty qualified
staff count is computed as:

```csharp
var qualifiedStaffCount = await db.RoomShifts
    .Where(s => s.LocationId == request.LocationId && s.CheckedOutAt == null)
    .Join(db.StaffProfiles, s => s.StaffProfileId, p => p.Id, (s, p) => p)
    .Where(p => p.QualificationLevel != QualificationLevel.StudentVolunteer)
    .Select(p => p.Id)
    .Distinct()
    .CountAsync(cancellationToken);
```

This is sourced from `RoomShifts` — feature 008a's real-time PIN check-in/check-out log —
not from any planned schedule. This is, if anything, a *more* correct signal than a schedule
would be: a caregiver who is scheduled but calls in sick before the director updates the
rota is already excluded, because they simply never check in. Wiring `staff_schedules` into
this query would be redundant at best, and at worst could introduce a discrepancy between
"scheduled" and "actually present" that the current design correctly avoids by only trusting
physical presence.

**Alternatives considered**:
- *Wire `staff_schedules` + absence + qualification directly into `GetBkrRatioQuery`,
  replacing or supplementing the `RoomShifts` join.* Rejected — this would regress accuracy
  (a schedule is a plan, not a fact) and duplicate a concern feature 010 already resolved
  correctly. It would also silently change feature 010's live compliance behavior from a
  different feature's PR, which is exactly the kind of scope creep the standing "flag for
  future features" notes in BACKLOG.md's shipped-notes exist to prevent.
- *Do nothing with the "BKR integration" bullet at all.* Rejected — the spirit of the
  original request (directors want visibility into whether a day is adequately staffed
  *before* it arrives, not just in the live moment) is real and valuable; dropping it
  entirely would under-deliver relative to the feature's actual intent.
- *Chosen: a separate, explicitly-labeled "projected on-duty count" derived from
  `staff_schedules` + absence + qualification, exposed only to the rota builder for
  forward-looking planning.* This satisfies the planning need without touching feature 010's
  proven-correct live computation, and keeps the two concerns (planned vs. actual)
  structurally separate — consistent with `Workflows/classroom-operations.md`'s existing
  distinction between the room shift register (actual) and this feature's rota (planned).

This correction is recorded in spec.md's Clarifications section (Session 2026-07-10) and
reflected in the revised FR-006/FR-007/FR-009b and SC-003.

## R2: Overlap validation concurrency safety

**Decision**: Reuse `IAdvisoryLockService.RunExclusiveAsync(Guid key, ...)` (feature 007,
`backend/ChildCare.Application/Common/IAdvisoryLockService.cs`), keyed on `staffId`, wrapping
the check-then-write in `CreateStaffScheduleCommandHandler` and
`UpdateStaffScheduleCommandHandler`.

**Rationale**: This is the exact same class of problem feature 007 solved for the
split-location contract day-overlap validator — two concurrent writes for the same staff
member must not both pass an overlap check that reads-then-writes non-atomically. Feature
007's `IAdvisoryLockService` already exists, is a proven pattern in this codebase, and is
explicitly named as the reusable port for "any future feature needing to serialize
concurrent requests touching the same aggregate" (007's shipped-notes). No new lock
mechanism is introduced.

**Alternatives considered**: A database-level `EXCLUDE` constraint using `tstzrange` overlap
operators. Rejected for this feature — it would require a PostgreSQL extension
(`btree_gist`) not currently used anywhere in this schema, and the advisory-lock pattern is
already established, tested, and sufficient at this feature's scale (single-tenant write
volume, not a high-concurrency hot path).

## R3: Caregiver own-schedule read endpoint pattern

**Decision**: `GET /api/staff-schedules/me`, `StaffOrDirector` policy, resolving the
requesting caregiver's `StaffProfile.Id` from the JWT's `ClaimTypes.NameIdentifier` claim —
identical pattern to `GET /api/staff/me` (feature 008,
`backend/ChildCare.Api/Endpoints/StaffEndpoints.cs`, `GetStaffMeQuery`).

**Rationale**: This endpoint exists purely so feature 027 (Staff App) has a ready-made read
API when it's built — see spec.md's Assumptions for the full scope decision (no
caregiver-facing UI ships in this feature). `GET /api/staff/me` is the exact established
precedent for "a personal, self-service read scoped to the caller's own tenant-user
identity, registered as a standalone route outside the `DirectorOnly` route group" (since
ASP.NET Core composes group + route `RequireAuthorization` calls additively/AND — a route
needing a more permissive policy than its group must be a standalone route, per feature
008's research.md R6).

**Alternatives considered**: Serving this via the device-token-authenticated tablet route
(mirroring 008a's kiosk endpoints). Rejected — per feature 009's research.md R4, device-token
routes carry no individual caregiver identity, so "my own schedule" cannot be resolved
there. A personal read requires a personal, tenant-user-authenticated route.

## R4: Rota-copy conflict and closure-day handling

**Decision**: `CopyWeekCommand` performs a single bulk operation per target week: for each
source-week entry, compute the corresponding target-week date; skip (and record in the
response) any date that either (a) has a `KdvClosureDay` for that location/date, or (b)
already has an existing `StaffSchedule` entry for that staff/date/start_time; insert all
other entries in one transaction.

**Rationale**: Both skip conditions were resolved via `/speckit-clarify` (see spec.md
Clarifications) using the recommended default — skip-and-report over block-the-whole-copy,
consistent with FR-009's existing closure-day-exclusion requirement. A single transaction
keeps the bulk-insert atomic from the caller's perspective (either all non-skipped entries
land, or none do, on any unexpected DB error) while still allowing partial success by design
(skipped slots are an expected, reported outcome, not a failure).

**Alternatives considered**: None beyond what the clarify session already evaluated — "block
the whole copy on any conflict" was the rejected alternative, since it turns one bad slot
into a full manual re-entry and doesn't match the "major time saver" intent behind the
copy feature.

## R5: Deactivated-staff schedule handling

**Decision**: `GetProjectedOnDutyQuery` excludes any `StaffSchedule` entry whose
`StaffProfile.DeactivatedAt` is non-null, without deleting or mutating the underlying row.
No new column is added to `StaffSchedule` for this — the exclusion is a join-time filter
against the already-existing `StaffProfile.DeactivatedAt`.

**Rationale**: Resolved via `/speckit-clarify` (spec.md Clarifications). Reusing the
existing `DeactivatedAt` field (rather than adding a redundant flag to `StaffSchedule`)
avoids a second source of truth that could drift — the same reasoning as feature 006's
`IChildDeactivationGuard` pattern of checking deactivation state at read time rather than
propagating a denormalized flag.

## R6: Web rota-builder UI pattern

**Decision**: A week-grid page at `web/app/(app)/scheduling/page.tsx` — rows are staff
members, columns are the 7 days of the selected week, cells show an assigned shift
(location/group/time) or are empty/click-to-assign. Follows the same high-density,
full-row-click (not small icon buttons) convention established by `web/app/(app)/staff/page.tsx`
and `web/app/(app)/closures/page.tsx` (feature 007a/011), per `platform-rules.md`'s
Director Web density rules and `reference-products.md`'s "avoid hidden actions" guidance.

**Rationale**: This is exactly the "Staff scheduling and room assignment" example
`platform-rules.md` names for Director Web (Airtable/Notion/Linear reference). Reusing the
existing table/grid conventions from `closures`/`staff` avoids introducing a new UI pattern
for what is structurally a similar problem (a grid of assignable cells against
dates/entities), consistent with `design-system.md`'s "shared components reused rather than
reimplemented" rule.

**Alternatives considered**: A calendar-library component (e.g., a full scheduling/Gantt
widget). Rejected — no such dependency exists in `web/` yet, and introducing one for a single
feature's grid would violate the "no reinventing a component that already exists" /
"prefer simple interactions" guidance when a plain data-grid (already proven in `closures`)
is sufficient at this feature's scale.

## R6: Staff-location eligibility enforcement (found via `/speckit-converge`, finding F1)

**Decision**: `CreateStaffScheduleCommand` and `UpdateStaffScheduleCommand` both reject an
entry that would assign a staff member to a location they have no `StaffLocationEligibility`
row for (`403 errors.staff_schedules.not_eligible`, FR-017). The director-web grid also
filters its staff rows to eligible-for-this-location staff (plus anyone with an existing
entry there, so nothing is ever hidden), using `StaffResponse.eligibleLocationIds` already
returned by `GET /api/staff` — no new endpoint needed.

**Rationale**: Neither the original BACKLOG.md prompt nor the initial spec.md mentioned
eligibility. It surfaced during `/speckit-converge`'s codebase-consistency pass:
`VerifyPinCommand`/`CheckInCommand` (feature 008a) already refuse a caregiver's check-in at
a location they aren't eligible for (`RoomShiftFailure.NotEligible`). Without this check, a
director could plan a shift the system already knows is impossible — the caregiver's actual
check-in at that location would fail, producing a rota that looks complete but silently
can't be fulfilled. This is a data-integrity gap, not a reasonable v1 simplification, so it
was fixed rather than logged as follow-up debt (same standing rule as every other
checklist/analyze/converge finding in this project).

**Alternatives considered**: Leaving it unenforced and treating it as a training/process
issue (directors are expected to know who's eligible where). Rejected — feature 005
introduced `StaffLocationEligibility` specifically so features aren't left guessing based
on directors' memory, and 008a already relies on it as a hard boundary; a second feature
touching location assignment should reuse the same boundary, not quietly bypass it.
