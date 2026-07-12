# Data Model: Reservation Settings

## `Location` (extended — feature 004, tenant schema)

Four new columns, following 004's own precedent of nullable/defaulted settings fields living
directly on `Location` rather than a separate table (research.md not needed for this choice — it
matches 004's `NaamLocatie`/`Dossiernummer`/etc. exactly).

| Column | Type | Default | Notes |
|---|---|---|---|
| `ReservationAbsencesMode` | `ReservationRequestMode` (enum, stored as text) | `Approval` | FR-001/FR-002 |
| `ReservationExtrasMode` | `ReservationRequestMode` | `Approval` | FR-001/FR-002 |
| `ReservationSwapsMode` | `ReservationRequestMode` | `Disabled` | FR-001/FR-002 — most KDVs disallow swaps by default (per original brief) |
| `ReservationNoticeHours` | `int` | `0` | FR-003, validated 0–8760 (FR-011) |

New enum, `ChildCare.Domain.Enums.ReservationRequestMode`:

```csharp
public enum ReservationRequestMode { Disabled, Informational, Approval }
```

Migration back-fills existing rows with the column defaults above (FR-002) — every location
created before this feature shipped behaves exactly as it does today (`approval`/`approval`/
`disabled`/`0`).

## `DayReservation` (unchanged shape — feature 013a)

No new columns. This feature changes *how* a row reaches `Approved` (system- vs. director-decided)
and *whether* submission is permitted, not the entity's shape:

- `DecidedBy = null` on an `Approved` row now means "auto-approved under `informational` mode"
  (research.md R1) — previously impossible, since every prior approval path always set a real
  director id.
- `AbsenceJustified` is set to `true` for a system auto-approval of an `absence` request (no
  director judgment is being applied under `informational` mode — the parent's report is taken at
  face value, consistent with the mode's own definition).

## New value type: `ReservationPolicy` (Application layer, not persisted)

```csharp
public record ReservationPolicy(ReservationRequestMode Mode, int NoticeHours);
```

Computed per-submission by `ReservationPolicyResolver` (research.md R3) — never stored; always
derived fresh from the current `Location` settings and the child's current active contracts, so a
mode change takes effect immediately for the very next submission (FR-004/SC-001).
