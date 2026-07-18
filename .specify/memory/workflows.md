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

Detail: `Workflows/billing.md` (feature 014 — monthly invoice generation and payment tracking;
the first feature in this workflow).

Manages financial relationship between families and the center.

Includes:

- Tuition.
- Invoices.
- Payments.
- Receipts.
- Discounts.

---

## Reporting & Management

Detail: `Workflows/reporting.md` (feature 018 — director dashboard: occupancy, BKR compliance,
monthly attendance summary, invoice status overview, data-completeness monitor; the first
feature in this workflow).

Change note (per governance rules): added because feature 018 is the first implementation in
this workflow, which previously had no detail file; no existing implementation needs review,
since no earlier feature built anything here.

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

---

## Government Reporting & Compliance

Detail: `Workflows/government-reporting.md` (added 2026-07-15 by the regulatory research pass;
first features: 033–038, 041 — none started yet).

Change note (per governance rules): added because the 2026-07-15 research pass surfaced a whole
category of legally mandated flows between a KDV and Flemish/federal authorities that no existing
workflow covered — affects features 015, 019, 033–038, 041 (and touches 024/034); no existing
implementation needs review, since none of these features has started.

Manages the legally mandated data flows between the organisation and government bodies
(Opgroeien / Kind & Gezin, FOD Financiën, Zorginspectie), plus the in-house compliance registers
those bodies require.

Includes:

- Monthly kinderopvangtoeslag attendance submission (AARON webservice — feature 033).
- Monthly IKT opvangprestaties submission (FO-SU-05 — feature 019).
- Annual jaarregistraties (FO-RE forms + medewerkers helper — feature 034).
- Fiscal attest 281.86 generation and Belcotax-on-web filing (features 015, 019).
- Risk-analysis register, incident logbook, sleep-position attests (feature 035).
- Crisis / grensoverschrijdend-gedrag mandatory reporting and verontrusting registration
  (feature 036).
- Attendance-register legal compliance: parent confirmation, retention, inspection export
  (feature 037).
- Data-retention lifecycle per sector terms (feature 038).
- Effective-dated regulatory rulesets (BKR 2027 — feature 041).

Source contracts: `docs/integrations/opgroeien/` (see its README.md).
