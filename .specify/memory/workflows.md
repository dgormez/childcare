# ChildCare Domain Workflows

## Purpose

This document defines the business workflows of the childcare platform.

It describes:

- How childcare operations happen.
- Which users participate.
- How information flows between caregiver tablet, parent mobile, director web, and backend
  systems.

Features should map to one or more workflows.

This document is a living domain model.

---

# Governance Rules

## Claude may

- Add new workflows when new business capabilities are discovered.
- Add missing steps to existing workflows.
- Split workflows when they become too broad.
- Add actors when new roles are discovered.
- Improve workflow descriptions based on implementation discoveries.

## Claude must not

- Remove existing business meaning.
- Change responsibilities between users silently.
- Merge unrelated workflows for implementation convenience.
- Change workflow outcomes without documenting the reason.

## Workflow changes require documenting

- What changed.
- Why it changed.
- Which features are affected.
- Whether existing implementations need review.

---

# Workflow Map

Each entry below names its detail file in `Workflows/` if one exists yet. Not every workflow
has one — per the governance rules above, add one when a feature actually needs the detail,
rather than stubbing all of them out up front.

## Child Lifecycle

Detail: `Workflows/child-lifecycle.md` (feature 012a — pre-enrollment waiting list; room
assignment/transfers/departure content is still unwritten, added when a feature needs it).

Manages the relationship between a child and the childcare organization.

Includes:

- Enrollment.
- Child profile.
- Parent onboarding.
- Room assignment.
- Transfers.
- Departure.

---

## Attendance & Presence

Detail: `Workflows/attendance.md`.

Manages knowing who is present and responsible for children.

Includes:

- Check-in.
- Check-out.
- Attendance corrections.
- Absences.
- Late arrivals.
- Capacity tracking.
- KDV closure days that mark enrolled children as closed for a location/date.

---

## Daily Child Care

Detail: `Workflows/dailycare.md`.

Manages activities and care events during the child's day.

Includes:

- Meals.
- Sleep.
- Diapering.
- Toileting.
- Activities.
- Learning observations.
- Photos.
- Daily reports.

---

## Parent Communication

Detail: `Workflows/communication.md`.

Manages communication and trust between childcare staff and families.

Includes:

- Messages.
- Daily updates.
- Announcements.
- Closure notifications and cancellation notices.
- Photos.
- Notifications.
- Parent feedback.

---

## Health & Safety

Detail: `Workflows/health-safety.md`.

Manages safety-critical childcare information — both the ongoing medical/emergency profile and
one-off safety events.

Includes:

- Allergies.
- Medication.
- Incidents.
- Injuries.
- Emergency information.
- Health alerts.

---

## Classroom Operations

Detail: `Workflows/classroom-operations.md` (feature 008a — room shift register; the
schedule/activities/ratios content below is still unwritten, added when a feature needs it).

Manages how caregivers run childcare rooms.

Includes:

- Classroom schedule.
- Activities.
- Staffing.
- Child groups.
- Ratios.
- Daily routines.

---

## Billing & Payments

No detail file yet.

Manages financial relationship between families and the center.

Includes:

- Tuition.
- Invoices.
- Payments.
- Receipts.
- Discounts.

---

## Reporting & Management

No detail file yet.

Supports director decision making.

Includes:

- Attendance reports.
- Enrollment reports.
- Revenue.
- Classroom utilization.
- Compliance reporting.

---

## Platform Administration

No detail file yet (feature 013h — first feature in this workflow).

Manages platform-wide, cross-tenant capabilities that sit above any single KDV — distinct from
every other workflow above, which is scoped to one tenant's operations. A **Platform Admin** is
not a new kind of account: it is an existing director account (`TenantUser`, still scoped to its
own tenant like any other director) with an additional flag granting extra authority over
platform-wide reference data that lives outside any tenant schema.

Actors:

- **Platform Admin** — an existing director account flagged for this extra authority. Granted
  out-of-band (direct data change), not through any in-app flow, mirroring how the reference data
  itself is maintained.

Includes:

- Managing the shared vaccine catalog (feature 013g's `vaccine_types` reference table — create,
  rename, reorder, deactivate entries; feature 013h).
- Any future platform-wide reference data or cross-tenant administrative capability — this
  workflow is the intended home for it, reusing the same `IsPlatformAdmin` flag/authorization
  policy rather than each feature inventing its own cross-tenant admin mechanism.

Explicitly excludes: anything scoped to a single tenant's own data (that's every other workflow
above, gated by the existing `DirectorOnly`/`StaffOrDirector`/tenant-scoping model) and
platform-operator actions performed outside the app entirely (e.g. granting the
`IsPlatformAdmin` flag itself, or infrastructure/deployment operations) — those remain direct
data changes or ops tooling, not a UI workflow.
