# API Contract: Developmental Milestones

All routes require an authenticated caller (JWT or device token, per policy below). All responses
use existing i18n-key error conventions (`{ "errorKey": "errors.milestones.<reason>" }`) on
failure.

## `GET /api/developmental-domains`

Policy: any authenticated caller (`DeviceOrStaffOrDirector` union `ParentOnly` — i.e. no
restriction beyond "is someone," mirrors `GET /api/vaccine-types`' shared-catalog openness, scoped
down for parent access too since this catalog carries no sensitive data).

Returns the shared catalog: `[{ id, code, nameNl, nameFr, nameEn, sortOrder, milestones: [{ id,
ageFromMonths, ageToMonths, descriptionNl, descriptionFr, descriptionEn, sortOrder }] }]`, grouped
by domain, ordered by `sortOrder`.

## `POST /api/children/{childId}/milestone-observations`

Policy: `DeviceAuthenticated` (matches `POST /api/child-events`'s actual recording-route policy —
research.md R6 corrected during implementation: `ChildEventEndpoints`' record route uses
`DeviceAuthenticated`, not `DeviceOrStaffOrDirector`).

Body: `{ milestoneId, status, observedAt, notes? }`. `status` MUST be one of
`emerging`/`achieved`/`not_yet` — anything else returns 400
(`errors.milestones.invalid_status`). `observedBy` is derived server-side from the device/shift
claims, not client-supplied (matches `child_events`' `recorded_by` derivation).

Returns `201 Created` with the new observation. No corresponding `PUT`/`PATCH`/`DELETE` route
exists for this resource (research.md R3) — this is the only mutation endpoint.

## `GET /api/children/{childId}/milestone-portfolio`

Policy: `DeviceOrStaffOrDirector` (corrected during implementation from `StaffOrDirector`: the
caregiver tablet also needs read access here to show a confirmation/history view immediately
after recording — mirrors `ChildrenEndpoints`/`GroupsEndpoints`' own `DeviceOrStaffOrDirector`
precedent, research.md R2). Full per-milestone history included.

Returns the domain-grouped portfolio: each milestone's current status, `isCurrentFocus` (age-band
match), and full chronological observation history.

## `GET /api/parent/children/{childId}/milestone-portfolio`

Policy: `ParentOnly`. Handler resolves the caller's `Contact` via
`ICurrentParentContactResolver`, then verifies `ChildContacts.Any(cc => cc.ContactId ==
contact.Id && cc.ChildId == childId)` — returns 403 (`errors.milestones.forbidden`) if the caller
is not a linked contact of the child, mirroring `GetParentDailySummaryQuery`'s `Forbidden()`
pattern exactly.

Returns the same domain-grouped structure as the director endpoint, but each milestone exposes
only its current status and `isCurrentFocus` — not the full observation-by-observation history
(data-model.md's Derived View note).

## `GET /api/children/{childId}/milestone-portfolio/pdf`

Policy: `StaffOrDirector` for the director-triggered download (caregiver tablet does not export
PDFs — FR-008 pairs this only with the director/parent variants).

## `GET /api/parent/children/{childId}/milestone-portfolio/pdf`

Policy: `ParentOnly`, same ownership check as the parent portfolio query above.

Both PDF routes render on-demand (research.md R4) and stream `application/pdf` bytes directly in
the response — nothing is written to storage, no signed URL involved (unlike fiscal attestations).
