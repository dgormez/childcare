# Research: Daily Attendance Registration

All items below were resolved by reading existing shipped code rather than external research —
this feature builds directly on infrastructure features 002/006/007/008/008a/009 already
established. No NEEDS CLARIFICATION markers remained in Technical Context.

## R1: `recorded_by` attribution

**Decision**: Reuse `IShiftAttributionService.ResolveRecordedByAsync(locationId, groupId,
occurredAtUtc)` (`backend/ChildCare.Application/RoomShifts/IShiftAttributionService.cs`) to
populate `AttendanceRecord.RecordedBy` (a `Guid[]`/`uuid[]` column, mirroring
`ChildEvent.RecordedBy`'s existing mapping) at check-in/check-out/absence-write time. No new
attribution mechanism is built.

**Rationale**: This service's own doc comment already names feature 010 as an intended caller
(feature 009's research.md R2 notes the same). Building a second attribution query for attendance
would duplicate already-tested code for no reason — the underlying question ("who was checked in
at this location/group at this moment") is identical for both features.

**Alternatives considered**: A single nullable `RecordedBy: UUID` column matching the originating
backlog's literal schema — rejected per spec.md's Clarifications session; the same
no-individual-identity constraint that drove feature 009 to an array applies identically here.

## R2: BKR ratio computation and "nap time" inference

**Decision**: `GetBkrRatioQuery` computes, for a given `locationId`:

1. **Present count** — `AttendanceRecords.Count(r => r.LocationId == locationId && r.Date ==
   today && r.Status == Present && r.CheckOutAt == null)` (checked in, not yet checked out).
2. **Qualified on-duty staff** — reuses `GetRoomRosterQuery`'s existing pattern
   (`backend/ChildCare.Application/RoomShifts/GetRoomRosterQuery.cs`): open `RoomShift`s at the
   location, joined to `StaffProfile.QualificationLevel`, excluding `StudentVolunteer` (spec
   FR-007a). Reuses `CloseStaleShiftsHelper` first, exactly as `GetRoomRosterQuery` does, so a
   forgotten check-out from yesterday never inflates today's on-duty count.
3. **Nap-time inference** — a location is "in nap time" when at least half of its present
   children (from step 1) have an open (`EndedAt == null`) `sleep`-type `ChildEvent` right now.
   This is a direct read against `ChildEvents` (feature 009), no new table.
4. **Threshold selection** — 0 qualified staff + ≥1 present child → breached (FR-007b). 1
   qualified staff → cap 8 (14 if nap time). 2+ qualified staff → cap `9 × staffCount` (`14 ×
   staffCount` if nap time).

**Rationale**: Every input already exists in a shipped feature (008a's roster, 009's sleep
events, 005's qualification field) — this query composes them rather than introducing new state.
Inferring nap time automatically (vs. a manual toggle or fixed schedule) needs no caregiver action
and can't drift out of sync with reality, per spec.md's Clarifications session.

**Alternatives considered**: A manual per-room "nap mode" toggle — rejected: adds a UI element and
a forgettable manual step for a signal the system can already derive from existing data. A fixed
daily time window — rejected: factually wrong on days with an off-schedule or skipped nap, and
this codebase already has a working feature-009 sleep-event signal to use instead.

## R3: Leefgroep (18-cap) BKR regime — out of scope, constitution amendment

**Decision**: Not implemented by this feature. `constitution.md` v1.3.0 adds a documented
carve-out to Principle II (Regulatory Compliance by Design) rather than leaving this as an
undocumented plan-level deviation — see the constitution's Sync Impact Report for the full
amendment text.

**Rationale**: No feature to date gives `Group`/`Location` any way to be flagged as a leefgroep
versus a standard room, so there is no data for a leefgroep-specific cap to branch on. Per the
precedent set by the 1.1.0/1.2.0 constitution amendments (codify an exception formally rather than
bend a NON-NEGOTIABLE-adjacent principle via ad hoc plan justification), this is recorded as a
versioned constitution change, not silently absorbed into this plan.

**Alternatives considered**: Add a `Group.IsLeefgroep` flag now to close the gap — rejected as
speculative scope beyond what feature 010's own spec calls for (BACKLOG.md's 010 prompt doesn't
mention a group-type field); a future feature that actually needs the distinction can add it then.

## R4: Offline conflict policy — server-wins, distinct from feature 009

**Decision**: The `attendance_record` sync-engine handler (`mobile/services/attendance.ts`)
registers a conflict handler that, on a `409` response (duplicate check-in for the same
child/location/date), marks the queued row as synced with a conflict note (`sync_error =
"conflict: already recorded"`) rather than retrying — the server's existing record is
authoritative. This differs from feature 009's `child_event` handler, whose `onConflict: () =>
"discard"` policy exists because child events are independent, always-append-only records with no
uniqueness constraint to violate.

**Rationale**: Attendance has a real uniqueness constraint (one record per child/location/day)
that child events deliberately don't — two check-ins for the same child/day are not two valid
independent facts the way two diaper-change events are; one of them is stale/duplicate
information about the same real-world event (a child's arrival), so server-wins is the correct
resolution per spec.md's Clarifications and Key Constraints.

**Alternatives considered**: Client-wins (overwrite server state with the queued write) —
rejected: risks clobbering a legitimate check-out recorded by a different, better-connected tablet
while the original tablet was still offline. "All writes preserved" (feature 009's policy) —
rejected: doesn't apply here since there's exactly one valid record per child/day by design, not
an append-only log.

## R5: Same-day / any-day correction authorization

**Decision**: Reuse feature 009's resolved `ChildEventEditWindowPolicy` pattern directly: a
caregiver (device token) may correct only a same-day record, and only when the requesting
device's own `LocationId` claim matches the record's `LocationId`; a director (user JWT) may
correct any record regardless of age. Implemented as `AttendanceEditWindowPolicy`, structurally
identical to `ChildEventEditWindowPolicy`.

**Rationale**: Same underlying auth model, same conclusion feature 009 already arrived at
(research.md R4 there): device-token routes carry no individual caregiver identity, so
location-match is the only caregiver-facing eligibility signal actually available. No reason to
re-derive this from scratch.

**Alternatives considered**: None seriously considered — re-litigating an already-resolved,
identical constraint would be pure repetition of feature 009's own reasoning.

## R6: `planned_duration_minutes` derivation

**Decision**: At check-in time, `PlannedDurationCalculator` looks up the child's single active
`Contract` at that specific `LocationId` (per feature 007's split-location rule: at most one
active contract per child per location), finds the `ContractedDay` entry (if any) matching the
record's weekday, and computes `(EndTime - StartTime)` in minutes. If no `ContractedDay` entry
exists for that weekday (an "extra day"), `PlannedDurationMinutes` is stored as `null` (spec.md
FR-006a) rather than 0 or a fabricated value.

**Rationale**: `ContractedDay` already models "at most one entry per weekday" (feature 007); this
is a direct, no-ambiguity lookup. Storing `null` rather than `0` for the extra-day case preserves
the distinction between "contracted for 0 minutes" (which can't actually happen — a
`ContractedDay` either doesn't exist for a weekday or has a positive duration) and "no contracted
schedule to derive from at all."

**Alternatives considered**: Deriving a fallback duration from `check_in_at`/`check_out_at` for
the extra-day case — rejected as out of scope per spec.md's Assumptions; a future feature can add
this if Phase 3 reporting needs it.

## R7: EF Core migration approach

**Decision**: A standard EF Core migration adding `attendance_records` to the tenant migration set
(`ChildCare.Infrastructure/Persistence/Migrations/Tenant/`), applied via the existing per-tenant
migration-runner mechanism (feature 002) — no new migration mechanism needed.

**Rationale**: New table, not a schema-strategy change; no deviation from established practice.

## R8: Pagination

**Decision**: `ListAttendanceQuery` (director web's history/correction view) is paginated the same
way `ListChildEventsQuery` is (cursor-style, ordered by `(date DESC, id)`), reusing the same
pattern rather than a new one.

**Rationale**: Consistency with the one other paginated list in this codebase; attendance data
also grows one row per child per location per day indefinitely, the same "grows fast" profile
`child_events` has.

**Alternatives considered**: Offset/page-number pagination — rejected for the same reasons feature
009's research.md R6 already rejected it (degrades on large offsets, shifts under concurrent
inserts).

## R9: OpenAPI schema-id collision on `CheckInRequest`/`CheckOutRequest` (found during implementation)

**Decision**: The request DTOs are named `AttendanceCheckInRequest`/`AttendanceCheckOutRequest`,
not the shorter `CheckInRequest`/`CheckOutRequest` the contract originally used.

**Rationale**: `RoomShiftEndpoints.cs` (feature 008a) already declares its own
`CheckInRequest(Guid StaffId, string Pin)`/`CheckOutRequest(Guid StaffId, string Pin)` in the same
`ChildCare.Api.Endpoints` namespace. ASP.NET Core's built-in OpenAPI generator keys schema
components by the type's short name by default — two unrelated C# types sharing one name silently
collide, with one clobbering the other in `components.schemas`. This was caught only because
`openapi-typescript`'s generated mobile client ended up typed against the *wrong* shape entirely
(`{ staffId, pin }` instead of `{ childId, date }`) for `/api/attendance/check-in` — a `tsc
--noEmit` compile error surfaced it, not a runtime failure, since both routes shared one schema
silently. Renaming avoids the collision outright without touching feature 008a's existing types.

**Also discovered**: process hygiene for locally-run ad hoc backend instances (used to regenerate
`api-types.ts`) — `pkill -f "ChildCare.Api.dll"` does not match the process, since `dotnet run`
launches the native apphost executable named `ChildCare.Api` (no `.dll` suffix in the process
list). A stale prior instance kept serving the old (pre-rename) schema on the same port after a
supposedly-successful "kill and restart," which is what actually produced the wrong-schema
symptom above on the first attempt. `pkill -f "ChildCare.Api"` (no `.dll`) is required to
reliably stop it.

**Alternatives considered**: A custom schema-id transformer (`options.AddSchemaTransformer` /
a `CreateSchemaReferenceId` override) using the full namespace-qualified name — rejected as
disproportionate infrastructure change for a two-type naming clash; a rename is the minimal fix
and matches how other accidental short-name collisions in a growing codebase are normally
resolved (rename the newer one).
