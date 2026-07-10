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
