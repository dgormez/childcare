# Data Model: Multi-Child Events

No new tables, no migration. This feature is a creation-time convenience over the existing
`ChildEvent` entity (feature 009, `specs/009-child-events/data-model.md`) — every row a batch
submission creates is a normal `ChildEvent` row, indistinguishable at rest from one created by the
single-child flow. Rows created together from the same batch share `EventType`, `OccurredAt`, and
`Payload` but carry no link to each other or to a "batch" concept in the data model.

## ChildEventBatchFailureReason (new, in-memory only — not persisted)

The per-child failure reason returned in a batch response. Not a database enum/column; exists only
in `ChildEventBatchResult` (Application layer) and the API response shape.

| Value | Meaning |
|---|---|
| `ChildNotFound` | `child_id` doesn't resolve within this tenant (mirrors `ChildEventFailure.ChildNotFound`). |
| `NotPresent` | The child has no `AttendanceRecord` (feature 010) for today at this device's `LocationId` with `Status = Present` and `CheckOutAt == null` at the moment this child's row was attempted (research.md R4). |
| `ValidationFailed` | The shared payload fails `ChildEventPayloadValidator` for the batch's `EventType` — in practice this fails identically for every child in the batch (the payload is shared), but is still reported per-child for a uniform response shape. |

## Request/response shapes

See `contracts/child-events-batch-api.md` for the full `POST /api/child-events/batch` contract.
No changes to any existing `ChildEvent`-related contract, endpoint, or entity.
