# Contract: Child Events API

`POST`/`GET` (create, list, daily summary) are device-token authenticated only
(`RequireAuthorization("DeviceAuthenticated")`, feature 008a's `DeviceTokenRotationFilter`
applied), matching `RoomShiftEndpoints.cs`'s existing group pattern — no per-caregiver HTTP auth,
consistent with constitution's Technology Stack Constraints (device token is the tablet's
security boundary; PIN/attribution is accountability, not authentication).

`PATCH`/`DELETE` need to accept *either* a device token (caregiver, same-day-and-same-location
only) *or* a director's user JWT (any event, any day) on the same route. **Correction made during
implementation**: no existing endpoint in this codebase actually does this —
`RoomShiftEndpoints.cs`'s own `/api/room-shifts/{id}` correction route is `DirectorOnly` only,
not dual-auth as an earlier draft of this contract claimed. A new composite authorization policy,
`DeviceOrDirector` (`policy.AddAuthenticationSchemes("DeviceToken",
JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser()`), is added in `Program.cs`
specifically for these two routes — ASP.NET Core tries each listed scheme and succeeds if either
authenticates. Inside the handler, the code branches explicitly: a device-token claim present →
device path (`ChildEventEditWindowPolicy` same-day-and-location check); `director` role present →
always allowed; neither (e.g. a caregiver's own personal session JWT, which the auth model
doesn't expect to be used for this) → `403`.

## `POST /api/child-events`

Request:
```json
{
  "childId": "guid",
  "eventType": "diaper",
  "occurredAt": "2026-07-08T09:15:00Z",
  "endedAt": null,
  "payload": { "type": "wet", "notes": null },
  "visibleToParent": true,
  "administeredByStaffId": null
}
```

- `administeredByStaffId` is set by the client only after a successful
  `POST /api/room-shifts/confirm-administrator` call (reused from feature 008a, research.md R2);
  null/omitted means skipped or not applicable to this event type.
- Response `201`: full `ChildEventResponse` (below), with `recordedBy` populated server-side from
  `IShiftAttributionService`.
- Idempotent by `id` (FR-013a): if `id` is supplied and already exists, returns the existing
  record (`200`) instead of creating a duplicate or erroring.
- `422 { errorKey: "errors.validation", fieldErrors }` — payload doesn't match `eventType`'s
  shape, is missing a required field, has an empty `measurement` payload, or has a numeric field
  outside its FR-002a range (data-model.md) — the standard `ValidationBehavior` pipeline
  response, not a bespoke shape.
- `404 errors.children.not_found` — `childId` doesn't resolve within this tenant.

## `PATCH /api/child-events/{id}`

Request: any subset of `{ endedAt, payload, visibleToParent, administeredByStaffId }`.

- `200`: updated `ChildEventResponse`.
- `403 errors.child_events.edit_window_expired` — request is device-token authenticated (not a
  director JWT), and either `occurredAt` is not today (`Europe/Brussels`, research.md R8) or the
  requesting device's own `LocationId` claim doesn't match the event's `LocationId` (research.md
  R4 — corrected during implementation from a per-caregiver check to a device-location check,
  since no individual caregiver identity exists on this auth path).
- `404 errors.child_events.not_found` — unknown id or already soft-deleted.
- `422 { errorKey: "errors.validation", fieldErrors }` — merged payload fails type-specific
  validation.

## `DELETE /api/child-events/{id}`

- `204` on success (soft-delete; `DeletedAt` set).
- Same `403`/`404` semantics as `PATCH`.

## `GET /api/child-events?childId={id}&before={cursor}&limit={n}`

- Returns up to `limit` (default 20, max 100) events for `childId`, `DeletedAt IS NULL`, ordered
  `occurredAt DESC`, strictly before the opaque `before` cursor if supplied (research.md R6).
- Response includes `nextCursor` (null when no more pages).
- No `visibleToParent` filtering here — this is the caregiver-tablet-facing endpoint (full
  timeline, per spec.md's caregiver stories). A future parent-facing endpoint (built when the
  parent app exists) is expected to add its own `visibleToParent = true` filter rather than reuse
  this route unfiltered.

## `GET /api/child-events/daily-summary?childId={id}&date={yyyy-MM-dd}`

- Returns the computed Daily Summary (data-model.md) for that child/date, where `date` is
  interpreted as an `Europe/Brussels` calendar day (FR-018a, research.md R8), the same boundary
  `PATCH`/`DELETE`'s edit-window check uses.
- Always filters `DeletedAt IS NULL AND visibleToParent = true` (FR-018) uniformly across every
  field, including latest-value fields — this endpoint's whole purpose is to be
  parent-consumption-shaped, regardless of which client calls it today.
- `200` with all-zero/null fields when no events exist (never a `404`).

## Response shape (`ChildEventResponse`)

```json
{
  "id": "guid",
  "childId": "guid",
  "eventType": "sleep",
  "occurredAt": "2026-07-08T13:00:00Z",
  "endedAt": "2026-07-08T14:30:00Z",
  "payload": { "quality": "good", "durationMinutes": 90 },
  "visibleToParent": true,
  "recordedBy": ["guid", "guid"],
  "administeredBy": null,
  "createdAt": "2026-07-08T13:00:05Z",
  "updatedAt": "2026-07-08T14:30:10Z"
}
```

## Offline queue mapping (mobile, `entity_type = 'child_event'`)

| Local action | `operation` | `endpoint` | Notes |
|---|---|---|---|
| Create event | `create` | `POST /api/child-events` | Client-generated `id` included in payload so the server uses it as the PK (no id-swap needed on sync, simplifying research.md R3's merge). |
| End sleep (create still queued) | — | — | Merged into the queued create's payload client-side (research.md R3); no separate queue row. |
| End sleep (create already synced) | `update` | `PATCH /api/child-events/{id}` | Normal queued PATCH. |
| Edit event | `update` | `PATCH /api/child-events/{id}` | |
| Delete event | `delete` | `DELETE /api/child-events/{id}` | |

Conflict policy (`onConflict` in the new sync handler): default `"discard"` (server wins) for
every case except none is expected to actually 409 in practice — child events are append-only by
design (spec.md: "ALL WRITES PRESERVED"); the one true conflict case (two tablets ending the same
sleep event) is resolved server-side deterministically (data model: last write by `UpdatedAt`
wins, not a 409) rather than surfaced to the client as a conflict at all, so `onConflict` exists
only as the sync engine's required interface shape, not because this feature expects to hit it.
