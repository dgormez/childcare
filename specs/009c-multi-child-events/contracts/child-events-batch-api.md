# Contract: Child Events Batch API

`POST /api/child-events/batch` is device-token authenticated only (`RequireAuthorization
("DeviceAuthenticated")`, feature 008a's `DeviceTokenRotationFilter` applied) — same policy as
`POST /api/child-events` (research.md R3). `LocationId`/`GroupId` for every created row come from
the device token's own claims, exactly like the single-child endpoint; they are never per-`child_id`
values.

This also documents a prerequisite auth fix bundled into this feature (research.md R2):
`GET /api/children` and `GET /api/groups` are extended from `StaffOrDirector`-only to a
`DeviceOrStaffOrDirector`-style composite policy also accepting the `DeviceToken` scheme, mirroring
the existing `DeviceOrDirector` policy `PATCH`/`DELETE /api/child-events/{id}` already use. No
change to those two routes' response shape or their existing `StaffOrDirector` location-scoping
behavior for staff/director callers.

## `POST /api/child-events/batch`

Request:
```json
{
  "items": [
    { "childId": "guid", "id": "guid" },
    { "childId": "guid", "id": "guid" },
    { "childId": "guid", "id": "guid" }
  ],
  "eventType": "sleep",
  "occurredAt": "2026-07-11T13:00:00Z",
  "endedAt": null,
  "payload": { "quality": null },
  "visibleToParent": true
}
```

- `items`: 1–30 entries, deduplicated by `childId` server-side. `> 30` → `422
  { errorKey: "errors.validation", fieldErrors: { "Items.Count": "errors.child_events.batch_too_large" } }`
  before any child is processed — the standard `ValidationBehavior` pipeline response feature 009's
  own contract already established (`contracts/child-events-api.md`: "not a bespoke shape"), not a
  top-level `errorKey` of its own (**correction made during `/speckit-converge`**: an earlier draft
  of this doc claimed the latter, which doesn't match `Program.cs`'s `ValidationException` handler).
  Each
  entry's `id` is a client-generated id for that child's future `ChildEvent` row — mirrors
  `POST /api/child-events`'s existing `id` field and its idempotency-by-id behavior
  (FR-013a, feature 009): if a retried replay (e.g. an ambiguous network failure after the
  server already committed a child's row but before the client received the response) resends
  the same `id` for a child that already succeeded, that child is reported as `created` again
  (using the existing row) rather than creating a duplicate `ChildEvent`. This is what makes
  the offline-queue replay in research.md R6 safe to retry as a whole after a partial or
  ambiguous failure, without a separate per-batch idempotency mechanism.
- `eventType`: MUST be one of the eight multi-select-eligible types (`sleep`, `diaper`,
  `feeding_bottle`, `feeding_solid`, `mood`, `activity`, `note`, `custom`). Any other value
  (including `temperature`, `medication`, `weight`, `growth_check`) → the same standard
  `errors.validation`/`fieldErrors` shape as above (`fieldErrors.EventType =
  "errors.child_events.batch_type_not_supported"`) before any child is processed —
  these individual-only types have no `administeredByStaffId` field in this request shape at all,
  since the PIN-confirmation flow they require doesn't fit a batch.
- `payload`/`endedAt`/`visibleToParent`: same shape and validation rules as
  `POST /api/child-events` (feature 009), applied once and shared across every child in the batch.

Response `200` (always 200, whether every child succeeded or some failed — never 207, to keep the
mobile client's `response.ok` handling uniform, research.md R6):
```json
{
  "created": [
    { "childId": "guid", "eventId": "guid" }
  ],
  "errors": [
    { "childId": "guid", "reason": "not_present" }
  ]
}
```

- `created`: one entry per successfully created (or, on an idempotent-retry match, already-existing)
  `ChildEvent`, in the same order as the request's `items` (skipping failures).
- `errors`: one entry per failed `child_id`, `reason` one of `child_not_found`, `not_present`
  (data-model.md's `ChildEventBatchFailureReason`, snake_cased for the wire the same way
  `ChildEventTypeExtensions` already snake_cases multi-word event types). A shared-payload
  validation failure is not a per-child reason — it rejects the whole batch with `422` before any
  child is processed (data-model.md's "Correction made while implementing"), the same as
  `batch_too_large`/`batch_type_not_supported` above.
- A `child_id` never appears in both arrays.
- Every succeeding child's `ChildEvent` is committed independently (research.md R5) — a failure
  anywhere in the batch never rolls back an earlier success.

## Offline queue mapping (mobile, `entity_type = 'child_event_batch'`)

| Local action | `operation` | `endpoint` | Notes |
|---|---|---|---|
| Submit multi-child event | `create` | `POST /api/child-events/batch` | One queue row per batch submission, regardless of how many children are selected (research.md R6) — never exploded into per-child rows. |

Sync handling (`syncEngine.ts`): a `2xx` response with a non-empty `errors` array is marked via
`markSyncError(row.id, "partial: " + JSON.stringify(errors))` (counted as `failed`, reusing the
`"rejected: "`-prefix "needs review" convention feature 009 established, with a distinct `"partial:
"` prefix since some children did succeed) rather than `markSynced` — research.md R6. A fully
successful batch (`errors: []`) is marked synced exactly like any other queued row.
