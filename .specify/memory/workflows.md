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

Cross-cutting: photo retention/cost-tiering/deletion policy on departure is governed by feature
031 (Photo Lifecycle & Governance), layered on top of this workflow's deactivation/reactivation
steps rather than a change to them.

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

Cross-cutting: QR-based contactless check-in/check-out is governed by feature 021 (QR Contactless
Check-In) — an additive, per-location opt-in entry point that triggers the same check-in/check-out
transition a manual tap does; no change to the attendance record shape, BKR calculation, or the
caregiver's physical handover responsibility.

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

Cross-cutting: photo storage-class tiering and RBAC parity across profile/group-activity/health
photos is governed by feature 031 (Photo Lifecycle & Governance) — no change to how these are
captured or displayed here.

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

Cross-cutting: original-resolution photo download for parents is governed by feature 031 (Photo
Lifecycle & Governance) — an addition to how parents access photos already surfaced here, not a
new communication channel.

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

Cross-cutting: GDPR deletion-cascade and storage-class tiering for health/vaccine attachments is
governed by feature 031 (Photo Lifecycle & Governance) — no change to how attachments are
recorded or clinically used here.

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

## Staff Management

No detail file yet (feature 028 — first feature in this workflow).

Change note (per governance rules): added because feature 028 (Staff HR Dossier & Time
Registration) is the first feature covering staff employment records and worked-hours tracking —
distinct from **Classroom Operations**, which covers room/ratio "Staffing" (008a's room-shift
register) but not HR documents or time. No existing implementation needs review, since no earlier
feature built anything here.

Manages the employment relationship between the organisation and its staff — HR record-keeping
and worked-hours tracking, as distinct from the day-to-day room/ratio assignment Classroom
Operations already covers.

Actors:

- **Director** — manages each staff member's HR dossier, unlocks/corrects time entries, downloads
  the medewerkersbeleid subsidy report.
- **Staff** — clocks in/out via staff-mobile (feature 027).

Includes:

- Staff HR dossier: employment contracts, amendments, qualification and training documents,
  each with optional validity dates (feature 028).
- Time registration: clock in/out per function, per location/group, with a director-configurable
  immutability lock period (feature 028).
- Contract-expiry alerts: a director dashboard block surfacing staff whose employment contract
  expires within 60 days (feature 028).
- Medewerkersbeleid subsidy reporting: child-hours-to-staff-hours ratio by function, per location
  and period, plus a raw hours CSV export for payroll handoff (feature 028).

Explicitly excludes: room/group ratio assignment and shift scheduling (Classroom Operations,
Billing & Payments' payroll calculation is out of scope entirely — this workflow only produces
the hours data payroll systems consume).

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
