# API Contract: Reservation Settings

Extends `LocationEndpoints.cs` (feature 004) and `DayReservationEndpoints.cs` (feature 013a). No
new endpoint groups — both existing groups gain routes/fields.

## `GET /api/locations` / `GET /api/locations/{id}` (extended, `DirectorOnly`)

`LocationResponse` gains four fields (all always present, never null — column defaults per
data-model.md):

```jsonc
{
  // ...existing fields unchanged...
  "reservationAbsencesMode": "approval",   // "disabled" | "informational" | "approval"
  "reservationExtrasMode": "approval",
  "reservationSwapsMode": "disabled",
  "reservationNoticeHours": 0
}
```

## `PUT /api/locations/{id}/reservation-settings` (new, `DirectorOnly`)

Request:

```jsonc
{
  "absencesMode": "informational",
  "extrasMode": "approval",
  "swapsMode": "disabled",
  "noticeHours": 24,
  "confirmDespitePending": false   // FR-014 — set true to proceed after seeing the warning
}
```

Response `200 OK` — updated `LocationResponse`.

Failure — `404 Not Found`, `errors.location.not_found` (unknown location, mirrors existing
`LocationEndpoints` pattern).

Failure — `422`, standard `ValidationBehavior` `fieldErrors` shape — invalid mode string, or
`noticeHours` outside 0–8760 (FR-011).

Failure — `409 Conflict`, `errors.location.reservation_settings.pending_requests_warning` (FR-014)
— returned only when `confirmDespitePending = false` **and** at least one mode is changing away
from `approval` for a type that currently has one or more `pending` requests at this location.
Mirrors `PublishClosureDayCommand`'s `ConfirmExistingAttendance` pattern (feature 011) exactly.
Body:

```jsonc
{
  "errorKey": "errors.location.reservation_settings.pending_requests_warning",
  // Keys are DayReservationType wire strings (DayReservationMapper.ToWire, 013a) — "exchange",
  // not "swap". The settings column is named ReservationSwapsMode (matches the original brief's
  // "swaps" terminology and the web UI's "Dagwissel" label), but it governs DayReservationType.Exchange,
  // so every API payload uses "exchange" for consistency with every other day-reservation endpoint.
  "pendingCounts": { "absence": 3, "extra": 0, "exchange": 1 }   // only types actually changing away from approval
}
```

Resubmitting the identical request with `confirmDespitePending: true` applies the change; existing
pending requests remain untouched (FR-005).

## `POST /api/day-reservations` (existing, `ParentOnly` — behavior extended)

No request/response shape change. New failure modes:

Failure — `403 Forbidden`, `errors.day_reservations.request_type_disabled` (FR-007) — the
resolved candidate location(s)' mode for this request's type is `disabled` (research.md R3's
"any candidate disabled wins" rule).

Failure — `400 Bad Request`, `errors.day_reservations.notice_period_required` (FR-012) — the
requested date falls inside the resolved notice-hours window. A flat `errorKey`, not
`ValidationBehavior`'s `fieldErrors` shape — corrected during implementation to match this
codebase's actual convention: DB-dependent checks (`NotContractedDay`, `ClosureDay`) are
handler-level failures with a flat errorKey, since `ValidationBehavior`/`fieldErrors` is reserved
for synchronous FluentValidation rules with no DB dependency (constitution Principle III).

Success — when the resolved policy is `informational` (research.md R3), the created
`DayReservationResponse` is returned already `approved`, with `decidedBy: null` and `decidedAt`
set — same response shape as an `approval`-mode decision, just synchronous with submission
(FR-008).

## `GET /api/day-reservations` (existing, `DirectorOnly` — unchanged shape, new usage)

No change. `?status=approved` (or `all`) now also returns system-auto-approved rows
(`decidedBy: null`) alongside director-decided ones — the web queue screen distinguishes them by
this field (research.md R2), not a new endpoint.

## `GET /api/parent/children/{childId}/reservation-availability` (new, `ParentOnly`)

No parent-accessible read exposes location/mode data today (`ParentChildResponse`, feature 013,
carries no location reference at all). Lets the parent-mobile app decide which entry points to
show/block without duplicating `ReservationPolicyResolver`'s logic client-side.

Response `200 OK`:

```jsonc
{
  "absence": "approval",      // resolved ReservationPolicy.Mode, per type, same rules as submission-time (research.md R3)
  "extra": "disabled",
  "exchange": "informational", // DayReservationType wire string — see the pendingCounts note above
  "noticeHours": 24            // the resolved (most-restrictive) notice-hours value
}
```

Failure — `403 Forbidden`, `errors.day_reservations.child_not_linked` — same check
`SubmitDayReservationCommand` already performs, reused.
