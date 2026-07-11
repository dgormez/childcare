# Research: Day Reservations

No `NEEDS CLARIFICATION` markers remain in plan.md's Technical Context — this feature reuses
established patterns end-to-end. Findings below document why each reuse is safe and where the
prior art lives.

## R1: Absence approval reuses `AttendanceRecord`/`IClosureCalendarReader`, not a new mechanism

**Decision**: Approving an `absence`-type `DayReservation` creates an `AttendanceRecord` with
`Status = Absent` via the same validated path `MarkAbsentCommandHandler` already implements
(`backend/ChildCare.Application/Attendance/MarkAbsentCommand.cs`): child-exists check,
`IClosureCalendarReader.IsPublishedClosureDateAsync` guard, unique-constraint race handling. The
approval command handler either calls the existing `MarkAbsentCommand` via `IMediator.Send`
in-process, or duplicates its guarded-insert logic directly against `db.AttendanceRecords` — the
tasks phase should prefer sending the existing command (avoids duplicating the race-condition
handling) unless its `RecordedBy`/`GroupId` parameters don't fit a director-approval caller,
in which case a director-authored equivalent (`DirectorTenantUserId` path, already supported by
`MarkAbsentCommand`) is used directly.

**Rationale**: `MarkAbsentCommand` already encodes the exact business rule spec.md FR-011 needs
(closure day blocks the write) and the exact concurrency handling FR-016 needs (unique
constraint on `(ChildId, LocationId, Date)` — losing racer gets a clean failure, not a corrupt
double-write). Reimplementing this guard a second time risks the two copies drifting.

**Alternatives considered**: A parallel `IClosureCalendarReader`-only check without touching
`AttendanceRecord` directly (i.e., day reservations stays purely descriptive, and 010's UI reads
`day_reservations` at check-in time to decide justified/unjustified) — rejected because it
would require attendance's caregiver-tablet check-in flow to join against a new table it has no
reason to know about today, whereas writing the `AttendanceRecord` up front means 010's existing
absence-handling UI (director web attendance history, feature 010) needs zero changes to display
a pre-registered absence — it already renders `Status = Absent` rows.

## R2: `extra`/`exchange` approval does not touch `AttendanceRecord`

**Decision**: Approving `extra` or `exchange` only transitions the `DayReservation`'s own status
— no `AttendanceRecord` is created or modified.

**Rationale**: Feature 010's shipped notes confirm check-in already supports a day with no
matching `ContractedDay` (`PlannedDurationMinutes = null`, "extra-day manual check-in"). A child
showing up on an approved extra/exchange day checks in exactly like today's existing extra-day
path — nothing new needed. Pre-creating an `AttendanceRecord` before the child actually arrives
would be wrong anyway (attendance records presence, not intent).

**Alternatives considered**: Writing a "planned" attendance row at approval time — rejected,
attendance's whole model (010) is "record what happened," not "record what's planned"; a planned
row would need a new status/lifecycle 010 doesn't have and doesn't need for this to work.

## R3: Parent-child authorization reuses `ICurrentParentContactResolver`

**Decision**: `SubmitDayReservationCommand`/`CancelDayReservationCommand` resolve the calling
parent's `Contact` via `ICurrentParentContactResolver.ResolveAsync(tenantUserId)` (feature 013)
and verify the target `ChildId` is linked to that `Contact`, rejecting otherwise (FR-005).

**Rationale**: This is the exact primitive feature 013 built for "which family is this request
for," documented on the interface itself as the shared authorization primitive for every
parent-facing handler. No new linkage mechanism needed.

**Alternatives considered**: none seriously considered — this is a direct, documented precedent.

## R4: Push notifications reuse `IExpoPushSender`

**Decision**: Approval/rejection status changes call `IExpoPushSender` (feature 009/013's Expo
Push Notification Service port) the same way `ClosureNotificationService` and
`TemperatureAlertService` already do — fire-and-forget from the command handler after the status
transition commits, logging (not throwing) if the parent has no registered push token.

**Rationale**: Established port; no new notification channel needed.

**Alternatives considered**: none — direct reuse of an existing constitution-mandated tech
stack choice (Expo Push Notification Service).

## R5: Extra-day capacity warning reuses feature 012a's occupancy computation

**Decision**: The director-web queue's capacity warning for `extra` requests is computed the same
way feature 012a's `GetOccupancyQuery` computes forward-looking occupancy: active contracts
against `Location.MaxCapacity` for the requested date, honoring the closure calendar. Not wired
through attendance (010), consistent with 012a's own research finding that attendance data
doesn't exist for future dates.

**Rationale**: Direct precedent avoids re-deriving the same "how do I know if a future date is
full" logic a second, possibly-inconsistent way.

**Alternatives considered**: A new capacity query scoped to day reservations — rejected as
unnecessary duplication when 012a's query already answers "is location X at/over capacity on
date Y."

## R7 (found during implementation): absence approval resolves `LocationId` from the child's active contract

**Decision**: `DayReservation` intentionally has no `LocationId` column (a parent doesn't pick a
location when reporting illness — they just say their child won't be there). But
`AttendanceRecord` requires `LocationId` (feature 010's schema). `ApproveDayReservationCommand`'s
absence branch resolves it at approval time: the child's active `Contract` whose
`ContractedDays` includes `RequestedDate.DayOfWeek`, mirroring
`PlannedDurationCalculator`'s/`ClosureParentRecipientResolver`'s existing weekday-match query
shape (just without a known `LocationId` to filter by first, since resolving it *is* the point).
If no active contract covers that weekday (a genuinely edge-case "sick day" request for a day the
child isn't normally contracted), approval fails cleanly with `NoContractedLocation` rather than
guessing a location — the director sees this and can still resolve it manually through the
existing attendance UI if truly needed.

**Rationale**: A child can hold two simultaneous contracts at different locations (feature 007
split-location rule), so "the child's location" isn't a single fact — it's a function of which
day is being asked about. Every weekday maps to at most one location by construction (the
day-overlap validator, constitution Principle II, rejects two contracts claiming the same
weekday), so this resolution is deterministic when a match exists.

**Alternatives considered**: Adding a `LocationId` column to `DayReservation`, set at
submission time — rejected because it would force the parent-facing submission form to ask
"which location," a question with an obvious answer 95%+ of the time (derivable from the date)
and a UX cost (extra form field) for a case the system can already resolve itself. Also
considered defaulting to the child's *first* active contract regardless of weekday match —
rejected as silently wrong: writing an absence against the wrong location would corrupt that
location's attendance/BKR data for no benefit.

## R6: Contracted-day validation for `exchange` reuses `Contract.ContractedDays`

**Decision**: `SubmitDayReservationCommand`'s validator for `type = exchange` checks that
`exchange_for_date`'s day-of-week appears in an active `Contract.ContractedDays` for the child at
the relevant location (FR-003), by querying `ChildCare.Domain.Entities.Contract` /
`ChildCare.Domain.ValueObjects.ContractedDay` directly (read-only, no new port needed — contracts
are already tenant-schema entities read by other Application-layer handlers, e.g. 012a's
occupancy query).

**Rationale**: `ContractedDay.Weekday` (a `DayOfWeek`) is exactly the field needed; no
abstraction gap to bridge.

**Alternatives considered**: none — direct field reuse.
