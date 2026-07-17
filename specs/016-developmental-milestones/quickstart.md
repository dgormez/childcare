# Quickstart: Developmental Milestones

## Prerequisites

- Local backend running against Docker PostgreSQL (`docker-compose up`), migrations applied
  (public + tenant).
- A seeded tenant with at least one child whose `DateOfBirth` places them mid-way through a known
  milestone age band (e.g. 18 months, to exercise the 15–21 month band).
- A caregiver device token (kiosk pairing) and a director JWT for that tenant; a parent JWT linked
  to the same child via `ChildContacts`.

## Validate: catalog is available

```
GET /api/developmental-domains
```

Expect 7 domains (`motor_gross`, `motor_fine`, `language`, `cognitive`, `social`, `emotional`,
`self_care`), each with milestones spanning 0–36 months, in NL/FR/EN.

## Validate: caregiver records an observation

```
POST /api/children/{childId}/milestone-observations
{ "milestoneId": "<a milestone id from the catalog>", "status": "achieved", "observedAt": "2026-07-16" }
```

Expect `201 Created`. Repeat with `status: "not_yet"` for the same `milestoneId` — expect a
second, independent row; the first is untouched.

## Validate: director portfolio view

```
GET /api/children/{childId}/milestone-portfolio
```

Expect milestones grouped by domain; the milestone from the previous step shows `not_yet` as
current status with both observations present in its history; milestones in the child's current
age band show `isCurrentFocus: true`.

## Validate: parent portfolio view + access control

```
GET /api/parent/children/{childId}/milestone-portfolio   (as the linked parent) → 200, same grouped structure, no per-observation history
GET /api/parent/children/{otherChildId}/milestone-portfolio (as a parent not linked to otherChildId) → 403
```

## Validate: PDF export

```
GET /api/children/{childId}/milestone-portfolio/pdf
```

Expect a valid PDF stream reflecting the current portfolio state (re-request after recording a
new observation — the PDF must reflect it immediately, since it is never cached/stored).

## Out of scope for this quickstart

Reference-catalog editing (read-only in this feature, per spec.md's Assumptions) and the
caregiver-tablet/parent-mobile/director-web UI walkthroughs — covered by each platform's own test
suite during `/speckit-implement`.
