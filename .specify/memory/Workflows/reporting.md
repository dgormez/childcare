# Reporting & Management Workflow

## Purpose

Give a director a single, glanceable place to check operational health — occupancy, legal BKR
compliance, attendance, invoicing, and data completeness — without compiling that picture by
hand from separate screens or spreadsheets.

### Trigger

A director opens the dashboard (routinely, to check "is everything OK today"), or needs a
specific report (a monthly attendance summary to export, an investigation into a BKR breach, a
data-completeness sweep).

### Actors

- Director (views every report section, filters by location, exports the monthly attendance
  summary as CSV/PDF)
- System (aggregates read-only from existing operational tables on every view; introduces no new
  write-side event of its own)

### Flow — director dashboard (feature 018)

1. Director opens the dashboard. Every section loads independently against their own tenant's
   data only.
2. **Occupancy**: today's actual present-vs-capacity per group and per location (colour-coded
   green/amber/red), plus a week-ahead projection per location computed from active contracts
   (reusing feature 012a's forward-occupancy projection) — not from attendance, which doesn't
   exist yet for future dates.
3. **BKR compliance**: live present-children-vs-qualified-staff ratio per group, extending
   feature 010's existing location-scoped live-ratio computation to group scope; plus, for a
   director-chosen date range, the history of breach windows (start/end/group), reconstructed
   from existing attendance/room-shift check-in/out timestamps rather than a new persisted
   event log.
4. **Monthly attendance summary**: for a director-chosen month, present/absent
   (justified/unjustified split)/closure day totals per child, rolled up per group and per
   location — exportable as CSV or PDF.
5. **Invoice status overview**: current month's invoices bucketed paid/outstanding/overdue (the
   existing `status = sent AND due_date < today` convention from the Billing workflow), with
   revenue collected vs invoiced and a days-overdue list.
6. **Data-completeness monitor**: a flat list of gaps — a child with no authorised-pickup
   contact, a child with an overdue vaccine, a staff member missing a qualification level or a
   check-in PIN — each linking to that child's or staff member's existing detail screen to fix
   it.
7. A multi-location director can narrow every section to one location via a shared filter; the
   unfiltered default is the aggregate across all their locations.
8. Clicking any flagged item (an over-capacity group, a breach, an overdue invoice, a
   completeness gap) navigates to that record's own existing detail screen — this workflow adds
   no new detail/edit screens of its own, only the aggregate view and the drill-in link.

### Applications

Director Web:

- The dashboard (extends the existing `dashboard` screen) — occupancy, BKR, attendance summary,
  invoice overview, data-completeness monitor, all filterable by location.
- Monthly attendance summary export (CSV, PDF).

Parent Mobile: no interaction — this workflow is director-facing only.

Caregiver Tablet: no interaction — this workflow is director-facing only.

### Principles

- Read-only: this workflow never writes a new operational record. It aggregates what
  Attendance & Presence, Billing & Payments, Classroom Operations, and Child Lifecycle already
  produce.
- No separate reporting schema or data warehouse — every report queries existing tenant tables
  directly (with indexes added as needed), reusing established computed-status conventions
  (BKR's green/amber/red, invoicing's derived "overdue") rather than inventing new ones.
- "Today" is always the unambiguous Belgian calendar day, never a rolling 24-hour window, so the
  dashboard's meaning doesn't shift mid-shift at midnight.
- A colour-coded status is never colour alone — every occupancy/BKR state pairs with an icon.
- Every report and the completeness monitor is strictly tenant-scoped; a multi-location
  director's location filter narrows within their own tenant only, never across tenants.
