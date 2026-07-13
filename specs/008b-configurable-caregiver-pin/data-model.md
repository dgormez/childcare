# Data Model: Configurable Caregiver PIN

## Location (extended, no new table)

Adds one column to the existing `Location` entity (`backend/ChildCare.Domain/Entities/Location.cs`).

| Field                    | Type   | Default | Notes                                                                 |
|--------------------------|--------|---------|------------------------------------------------------------------------|
| `RequiresCaregiverPin`   | `bool` | `true`  | Whether check-in/check-out at this location requires PIN verification. |

No validation rules beyond the type itself (a bool has no invalid state). No relationships
change. No state-transition machinery is needed — flipping the value takes effect on the next
check-in/check-out attempt (existing open `RoomShift` rows are never touched, per FR-010).

## Room Shift (unchanged)

No schema change. The record produced by check-in/check-out is identical whether or not PIN
verification occurred — this is the mechanism (already true before this feature) that keeps
downstream BKR ratio and event/incident attribution unaffected (FR-005, FR-011, FR-012).

## Staff Profile (unchanged)

`PinHash`, `PinFailedAttempts`, `PinFirstFailedAttemptAt`, `PinLockedUntil` are untouched by this
feature — turning a location's requirement off/on never reads, clears, or resets any of these
fields (FR-009).
