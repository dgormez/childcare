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

**Correction made while implementing**: an earlier draft of this table also listed a
`ValidationFailed` per-child reason for a payload that fails `ChildEventPayloadValidator`. Writing
the actual handler surfaced that this can't happen per-child — the batch's payload is shared
across every selected child (one `EventType`/`Payload` for the whole request, not one per child),
so `ChildEventPayloadValidator` runs once, through the same `RuleFor(x =>
x).Custom(...)`/`ValidationBehavior` pipeline mechanism `RecordChildEventCommandValidator` already
uses (feature 009) — a failure there rejects the *whole* batch with `422` before any child is
processed, exactly like the `batch_too_large`/`batch_type_not_supported` checks, never as one
child's entry alongside others that succeeded. Removed rather than kept as a reason that could
never actually be returned.

## Request/response shapes

See `contracts/child-events-batch-api.md` for the full `POST /api/child-events/batch` contract.
No changes to any existing `ChildEvent`-related contract, endpoint, or entity. Each batch item
carries a client-generated `id` (mirrors `ChildEvent.Id`'s existing client-generated-on-create
convention, feature 009) so a retried offline-queue replay is idempotent per child, not just
per batch — see contracts/child-events-batch-api.md's `items` field.
