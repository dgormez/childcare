# Quickstart: QR Contactless Check-In

Validation scenarios proving this feature works end-to-end. Run against local dev
(`docker compose up` PostgreSQL + API) unless noted.

## Prerequisites

- A tenant with at least one location, one director account, one paired caregiver-tablet device
  token (feature 008a) for that location, one parent account with a child enrolled at that
  location.
- A second, untouched location in the same tenant (for SC-002's isolation check).

## Scenario 1 — Setting defaults to disabled, director opt-in is isolated

1. `GET` the location settings screen for a location that has never touched this setting —
   expect the QR check-in toggle to show disabled (FR-002).
2. As a parent linked to that location's child, attempt `POST
   /api/parent/attendance/qr-code` — expect `400 errors.qrCheckIn.not_enabled`.
3. As director, `PUT /api/locations/{locationId}/qr-checkin-setting` with `{"enabled": true}` —
   expect `200`, and a structured log entry recording the director/location/old→new value
   (FR-016).
4. `GET` the second, untouched location's setting — still disabled (SC-002).

## Scenario 2 — Full scan check-in / check-out cycle

1. With the setting enabled (Scenario 1), as the parent, `POST
   /api/parent/attendance/qr-code` with the child's id — expect `200` with a `code` and
   `expiresAtUnix` ~30s out.
2. As the paired caregiver-tablet device, `POST /api/attendance/qr-code/verify` with that code —
   expect `200`, `"direction": "check-in"`, and an `AttendanceRecordResponse` with
   `status = present`.
3. Immediately repeat step 2 with the *same* code — expect `409
   errors.qrCheckIn.already_used` (FR-019 cooldown), not a duplicate check-out.
4. As the parent, request a fresh code (step 1) and verify it (step 2) — expect
   `"direction": "check-out"` and `status` reflecting checked-out (the toggle behavior, FR-009).

## Scenario 3 — Rejection paths

1. Wait 31+ seconds after issuing a code, then verify it — expect `410
   errors.qrCheckIn.code_expired` (FR-011), distinct from the wrong-location error below.
2. As a caregiver tablet paired to a *different* location, verify a valid code for a child
   enrolled elsewhere — expect `403 errors.qrCheckIn.wrong_location` (FR-010), no attendance
   record created.
3. Tamper with a valid code's payload (flip a byte) and verify — expect `401
   errors.qrCheckIn.invalid_code` (FR-007).

## Scenario 4 — BKR/reporting parity (FR-014/SC-004)

1. Check one child in via QR scan (Scenario 2) and a second child in via the existing manual tap
   endpoint (`POST /api/attendance/check-in`).
2. `GET /api/attendance/bkr` for the location — both children count identically toward the
   ratio; no field on either `AttendanceRecordResponse` distinguishes their origin.

## Scenario 5 — Manual fallback always available

1. With the setting enabled, `POST /api/attendance/check-in` (manual tap) directly — expect
   `201`/`200` exactly as it behaves today, unaffected by the setting (FR-004/SC-005).
2. With the setting disabled, confirm no QR-related screen is reachable in the parent app or
   caregiver tablet UI for that location (manual code review of the client's setting-gated
   navigation — not an API-testable assertion).
