# Phase 0 Research: Waiting List Management

## R1: Occupancy projection source — attendance vs. contracts

**Decision**: The occupancy view is computed entirely from `Contract` (007) and
`Location.MaxCapacity` (004), never from `AttendanceRecord` (010). For a location and a
target date: `free = Location.MaxCapacity - activeContractCountCoveringThatWeekdayAndDate`,
where an active contract counts if `Status == Active`, `StartDate <= date`, and
(`EndDate == null || EndDate >= date`), and `ContractedDays` contains an entry whose
`Weekday == date.DayOfWeek`. Any date with a `KdvClosureDay` (`Status == Published`) for that
location is returned as `Closed`, overriding the numeric count entirely.

**Rationale**: BACKLOG.md's original prompt states the occupancy view "reads from attendance
(010) + contracts (007)". Reading feature 010's actual shipped implementation
(`AttendanceRecord`) shows it is a same-day/historical check-in log — rows only exist for
dates that have already happened (or are happening today). A waiting-list occupancy check is
inherently forward-looking (a family's `requested_start_date` is typically weeks or months
out), so attendance simply has no data for the dates that matter here. This mirrors feature
012's own precedent of correcting a BACKLOG premise against what an earlier feature actually
shipped, documented in spec.md's Clarifications rather than implemented as literally written.

**Alternatives considered**:
- *Use attendance for past/today and contracts for the future, blending the two.* Rejected —
  adds real complexity (two code paths, a boundary date to reconcile) for a view whose entire
  purpose is forward planning; a director checking "can I offer March 3rd" has no use for
  today's actual attendance count.
- *Wire in feature 010's `GetBkrRatioQuery` or `AttendanceRecord` directly as originally
  worded.* Rejected — technically infeasible for future dates (no rows exist), and would
  silently return misleading data (e.g., always reading as "fully free" for any date with no
  attendance rows yet, regardless of actual contracted load).

## R2: Priority reorder concurrency

**Decision**: No advisory-lock or optimistic-concurrency mechanism is introduced for
`ReorderWaitingListEntryCommand`. Two concurrent reorders on the same location's queue simply
apply last-write-wins.

**Rationale**: Resolved in spec.md's Edge Cases — this is a low-frequency, low-stakes
administrative action (a director adjusting queue position), unlike feature 007's contract
day-overlap validator (a legal/compliance-significant check requiring `IAdvisoryLockService`).
Introducing locking here would add complexity with no corresponding risk reduction — worst
case, one director's reorder is overwritten by another's a few seconds later, self-correctable
by reordering again.

**Alternatives considered**: Reusing feature 007's `IAdvisoryLockService`, keyed on
`(locationId)`. Rejected — that pattern exists specifically for cases where a race condition
could produce a silently-invalid business state (e.g., two overlapping active contracts).
Here, the worst outcome of a race is a slightly stale display order, not a data-integrity
violation.

## R3: Email notification mechanism

**Decision**: Add `Task SendWaitingListOfferedAsync(string toEmail, string contactName, string
childName, string locationName)` to the existing `IEmailSender` port
(`backend/ChildCare.Application/Common/IEmailSender.cs`) and its concrete MailKit-based
`EmailService` (`backend/ChildCare.Api/Services/EmailService.cs`), following the same
English-only raw-HTML body pattern already used by `SendStaffInvitationAsync` (feature 005).
`TransitionWaitingListStatusCommandHandler` calls it only on the `waiting → offered`
transition, and only when `ContactEmail` is non-empty; a missing email is logged, not treated
as an error.

**Rationale**: Feature 020 (the eventual dedicated `EmailService`/templating rework for parent
communications) is not shipped yet — the original BACKLOG prompt explicitly anticipates this
("uses the EmailService from 020 if already shipped, otherwise a simple MailKit send inline").
`IEmailSender` already is that "simple MailKit send inline" mechanism (built in feature 001,
extended by 003 and 005) — reusing it is the smaller, consistent change; building a second,
parallel email mechanism for one new email type would violate the "no reinventing something
that already exists" principle this codebase applies elsewhere (e.g., `IProfilePhotoStorage`,
`IAdvisoryLockService`).

**Alternatives considered**: Building a standalone `WaitingListEmailService`. Rejected — no
reason exists to duplicate `EmailService`'s SMTP-config/fallback-logging plumbing for one
additional email type.

## R4: Status transition enforcement

**Decision**: `TransitionWaitingListStatusCommandHandler` enforces the FR-007 allow-list
(`waiting→offered`, `waiting→withdrawn`, `offered→enrolled`, `offered→withdrawn`,
`offered→waiting`) via an explicit lookup table checked in the handler before persisting,
returning a typed `WaitingListFailure.InvalidStatusTransition` (mapped to `409
errors.waiting_list.invalid_status_transition`) for anything outside it — mirroring
`ContractStatus`'s handler-level transition checks (feature 007) rather than a database CHECK
constraint, since the allow-list depends on the *current* status (a transition rule), not a
single-column value constraint a CHECK could express.

**Rationale**: Consistent with how every other stateful entity in this codebase
(`Contract.Status`, `KdvClosureDay.Status`) enforces its lifecycle — in the
Application-layer command handler, testable directly, not scattered between client-side and
database-level checks (Constitution Principle II's "never only client-side" spirit applied
here even though this isn't a regulatory rule).

**Alternatives considered**: A PostgreSQL trigger enforcing valid transitions. Rejected — no
other entity in this codebase uses a DB trigger for business rules; the Application-layer
handler pattern is the established, testable convention.

## R5: Child-link and child-creation flow

**Decision**: `LinkChildToWaitingListEntryCommand` accepts either an existing `ChildId` (sets
`WaitingListEntry.ChildId` directly, validated to exist and belong to the tenant) or a
`CreateNewChild: true` flag, in which case the handler internally issues feature 006's
existing `CreateChildCommand` (`FirstName`/`LastName`/`DateOfBirth` pre-filled from the entry,
all other fields null/default) via `IMediator.Send`, then links the resulting `ChildId`. No
new child-creation logic is written — the existing command is reused as-is.

**Rationale**: FR-011 requires pre-filling name/DOB into a "create child record now?" flow.
Reusing `CreateChildCommand` directly (rather than duplicating child-creation validation)
follows the same “reuse an existing command from another module” pattern feature 012 used for
`IAdvisoryLockService`, and feature 009a used for its backfill command referencing existing
patterns — no feature to date has needed to fork child-creation logic, and this doesn't either.

**Alternatives considered**: A dedicated `WaitingListEntry`→`Child` conversion endpoint that
duplicates child-record field validation. Rejected — pure duplication of `CreateChildCommand`
for no behavioral difference.

## R6: Web waiting-list UI pattern

**Decision**: A table + side panel page at `web/app/(app)/waiting-list/page.tsx` — a
sortable/filterable table (per-location, defaulting to `waiting` status) on the left/main
area, an `OccupancyPanel` alongside it showing free-capacity-or-Closed per date for the
selected location, following the same high-density, full-row-action convention established by
`web/app/(app)/staff/page.tsx` and `web/app/(app)/closures/page.tsx` (features 007a/011), per
`platform-rules.md`'s Director Web density rules. Priority reorder uses explicit up/down
icon-buttons per row (not drag-and-drop) so the action is keyboard-operable without a second,
separate keyboard-only code path — satisfying spec.md's accessibility requirement with one
implementation rather than two.

**Rationale**: Matches every director-web feature shipped since 007a; introducing a
drag-and-drop library for reordering would need a separate keyboard fallback anyway (per
`platform-rules.md`'s keyboard-navigation requirement), so a single up/down-button
implementation is strictly simpler and already fully accessible.

**Alternatives considered**: A drag-and-drop reorder (e.g. `dnd-kit`). Rejected — no such
dependency exists in `web/` yet, and it would still require a keyboard-operable alternative
per spec.md's accessibility requirement, making up/down buttons necessary either way; adding
drag-and-drop on top would be pure extra surface area for zero additional capability.
