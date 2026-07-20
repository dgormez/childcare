# Phase 1 Data Model: QR Contactless Check-In

## Location (existing entity, extended)

New column:

| Field | Type | Default | Notes |
|---|---|---|---|
| `QrCheckInEnabled` | `bool` | `false` | FR-002 — every existing row defaults to `false` on migration; no location's behavior changes until a director explicitly opts in. |

No other changes to `Location`. Mirrors the existing per-location settings pattern (e.g.
`RequiresCaregiverPin` from feature 008b, `ReservationAbsencesMode` from feature 013f) — a plain
column, no new table, updated via a dedicated MediatR command.

## Check-In Code (ephemeral, not persisted)

Not a database entity — see research.md R1. Conceptually:

| Field | Type | Notes |
|---|---|---|
| `ChildId` | `Guid` | The child this code identifies. |
| `IssuedAtUnix` | `long` | Seconds since epoch; verification computes `now - IssuedAtUnix > 30` to reject expired codes (FR-006). |
| `Nonce` | `Guid` | Uniquely identifies this issuance; doubles as the FR-019 cooldown key after a successful scan. |
| *(signature)* | `byte[]` (HMAC-SHA256) | Computed over `{ChildId, IssuedAtUnix, Nonce}` with a server-only signing key; verification recomputes and compares (FR-007). |

Wire encoding: `base64url({ChildId}|{IssuedAtUnix}|{Nonce})` + `.` +
`base64url(signature)` — the literal string encoded into the QR code.

**Consumed-nonce cooldown set**: a short-lived server-side record (in-memory cache, or a
lightweight table/row keyed by `Nonce` with an expiry slightly past the code's own 30s window)
recording nonces that have already produced a successful check-in/check-out, so a second scan of
the same still-visible code within the cooldown window is rejected (FR-019) without needing a
second full persisted entity per code issued.

## Attendance Record (existing entity, feature 010 — unchanged)

No new field. A successful QR verification calls the exact same `CheckInCommand`/
`CheckOutCommand` path a manual tap uses (research.md R5), so the produced `AttendanceRecord` is
indistinguishable from one created by a tap (FR-008) — this feature adds no "origin" column,
since FR-014 explicitly requires downstream readers (BKR calculation, reporting) to treat both
origins identically, and no requirement in spec.md asks for the origin to be *displayed*
anywhere.

## State Transitions

Unchanged from feature 010's existing attendance state machine — see `Workflows/attendance.md`.
A QR scan is a new *trigger* into that machine, not a new state or transition.
