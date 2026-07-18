# API Contract: Management Reporting

All routes require `DirectorOnly` (FR-012/FR-018 constitution Principle I — no caregiver or
parent access to any report in this feature). All responses use existing i18n-key error
conventions (`{ "errorKey": "errors.reporting.<reason>" }`) on failure. Every route is tenant-scoped
via `ITenantDbContext`; an optional `locationId` query parameter narrows any route to one
location — omitted, the response aggregates across every location the director's tenant has.

## `GET /api/reports/occupancy`

Query: `locationId?`.

Returns:

```json
{
  "asOf": "2026-07-18",
  "locations": [
    {
      "locationId": "...",
      "locationName": "...",
      "presentCount": 8,
      "capacity": 20,
      "status": "green",
      "groups": [
        { "groupId": "...", "groupName": "...", "presentCount": 5, "capacity": 8, "status": "green" }
      ],
      "weekAhead": [
        { "date": "2026-07-19", "freeCapacity": 12, "closed": false }
      ]
    }
  ]
}
```

`groups[].capacity`/`status` are `null` for a group with no `Capacity` set (FR-001, Edge Cases).
`weekAhead` reuses `GetOccupancyQuery`'s existing shape (feature 012a) unmodified.

## `GET /api/reports/bkr`

Query: `locationId?`.

Returns the live per-group ratio (FR-004):

```json
{
  "asOf": "2026-07-18T14:32:00Z",
  "groups": [
    { "groupId": "...", "locationId": "...", "presentCount": 9, "qualifiedStaffCount": 1, "isNapTime": false, "threshold": 8, "status": "red" }
  ]
}
```

## `GET /api/reports/bkr/breaches`

Query: `locationId?`, `from?` (date, default: 30 days before `to`), `to?` (date, default: today).
Range MUST NOT exceed 366 days (matches `GetOccupancyQuery`'s existing range-validation
precedent) — 422 `errors.validation` otherwise (this codebase's standard FluentValidation
pipeline status code, not 400).

Returns (FR-005):

```json
{
  "from": "2026-06-18",
  "to": "2026-07-18",
  "breaches": [
    { "groupId": "...", "locationId": "...", "startedAt": "2026-07-10T13:05:00Z", "endedAt": "2026-07-10T13:40:00Z" }
  ]
}
```

Empty `breaches` array renders the "no breaches in this period" empty state client-side (FR-017).

## `GET /api/reports/attendance-summary`

Query: `locationId?`, `month` (required, `YYYY-MM-01`).

Returns (FR-006):

```json
{
  "month": "2026-06-01",
  "children": [
    { "childId": "...", "childName": "...", "groupId": "...", "locationId": "...", "presentDays": 18, "absentJustifiedDays": 1, "absentUnjustifiedDays": 0, "closureDays": 2 }
  ],
  "groupTotals": [ { "groupId": "...", "presentDays": 90, "absentJustifiedDays": 3, "absentUnjustifiedDays": 1, "closureDays": 10 } ],
  "locationTotals": [ { "locationId": "...", "presentDays": 180, "absentJustifiedDays": 5, "absentUnjustifiedDays": 2, "closureDays": 20 } ]
}
```

A child with a mid-month location/group change appears once per `(locationId, groupId)` pair
their attendance actually spans that month (data-model.md's Edge Case), not once per child.

## `GET /api/reports/attendance-summary/export`

Query: same as above, plus `format` (`csv` | `pdf`, required).

`format=csv`: `200 text/csv`, `Content-Disposition: attachment; filename="attendance-summary-{month}.csv"`,
UTF-8 with BOM (research.md R8). Columns match the on-screen `children` rows.

`format=pdf`: `200 application/pdf`, rendered on-demand via QuestPDF (not stored — matches
`GenerateInvoicePdfQuery`'s on-demand pattern, not `FiscalAttestation`'s stored-record pattern).

Both MUST produce totals identical to the on-screen `GET /api/reports/attendance-summary`
response for the same parameters (FR-007/FR-008, SC-002).

## `GET /api/reports/invoices`

Query: `locationId?` (implicitly scoped to the current calendar month — FR-009; no `month`
parameter, since this is a current-status overview, not a historical report).

Returns:

```json
{
  "month": "2026-07-01",
  "paidCount": 40, "paidTotalCents": 2400000,
  "outstandingCount": 5, "outstandingTotalCents": 300000,
  "overdueCount": 2, "overdueTotalCents": 120000,
  "totalInvoicedCents": 2820000,
  "overdueInvoices": [
    { "invoiceId": "...", "childName": "...", "dueDate": "2026-07-01", "daysOverdue": 17, "totalCents": 60000 }
  ]
}
```

## `GET /api/reports/data-completeness`

Query: `locationId?`.

Returns (FR-011):

```json
{
  "flags": [
    { "type": "missing_pickup_contact", "subjectType": "child", "subjectId": "...", "subjectName": "...", "detail": null },
    { "type": "overdue_vaccine", "subjectType": "child", "subjectId": "...", "subjectName": "...", "detail": "DTP (due 2026-05-01)" },
    { "type": "missing_qualification", "subjectType": "staff", "subjectId": "...", "subjectName": "...", "detail": null },
    { "type": "missing_pin", "subjectType": "staff", "subjectId": "...", "subjectName": "...", "detail": null }
  ]
}
```

Empty `flags` array renders the "nothing to flag" empty state client-side (FR-017).
