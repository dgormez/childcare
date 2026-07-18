# Quickstart: Management Reporting

## Prerequisites

- Local backend running against Docker PostgreSQL (`docker-compose up`), migrations applied
  (including this feature's `Group.Capacity` tenant migration).
- A seeded tenant with two locations, at least one group with `Capacity` set, some children
  checked in today (some over that group's capacity), and staffing that both satisfies and
  breaches BKR for at least one group.
- A director JWT for that tenant.

## Validate: today's occupancy is colour-coded correctly

```
GET /api/reports/occupancy
```

Expect each group with a `Capacity` set to show `green`/`amber`/`red` matching its actual
present-vs-capacity count; a group with no `Capacity` shows a present count with `capacity: null`,
`status: null` — no error, no divide-by-zero.

## Validate: closure day shows 0/capacity cleanly

Seed a `KdvClosureDay` for one location today, then repeat the occupancy request scoped to that
`locationId`. Expect `presentCount: 0` against the location's real capacity, not an error or an
empty response.

## Validate: live per-group BKR ratio

```
GET /api/reports/bkr
```

Expect the seeded breaching group to show `status: "red"` with its actual `presentCount`/
`qualifiedStaffCount`; a compliant group shows `"green"`.

## Validate: BKR breach history

```
GET /api/reports/bkr/breaches?from=2026-06-18&to=2026-07-18
```

Expect the seeded historical breach window to appear with a `startedAt`/`endedAt` matching when
the ratio was actually exceeded. Repeat with a range containing no breaches — expect an empty
`breaches` array (renders the empty state client-side).

## Validate: monthly attendance summary + export parity

```
GET /api/reports/attendance-summary?month=2026-06-01
GET /api/reports/attendance-summary/export?month=2026-06-01&format=csv
GET /api/reports/attendance-summary/export?month=2026-06-01&format=pdf
```

Expect all three to agree on every child's present/absent-justified/absent-unjustified/closure
totals. Seed one child with a mid-month location change and confirm their days split correctly
across both locations with no day dropped or double-counted.

## Validate: invoice status overview

```
GET /api/reports/invoices
```

Expect correct paid/outstanding/overdue counts and totals for the current month, with the overdue
list showing accurate `daysOverdue` per invoice.

## Validate: data-completeness monitor

```
GET /api/reports/data-completeness
```

Seed: a child with no `CanPickup` contact, a child with an overdue `VaccineRecord`, a staff member
missing `QualificationLevel`, a staff member with no `PinHash`. Expect all four flagged with the
correct `type` and `subjectId`. Remove all four conditions and repeat — expect an empty `flags`
array.

## Validate: location filter and tenant isolation

Repeat any of the above with `?locationId=<one of the tenant's locations>` — expect every section
to narrow to that location only. Attempt the same requests with a JWT from a different tenant —
expect no data from the first tenant to appear (or a 403/404, depending on the route).

## Out of scope for this quickstart

Director-web UI walkthrough (dashboard sections, filters, export buttons) — covered by
`web/`'s own component/E2E tests during `/speckit-implement`.
