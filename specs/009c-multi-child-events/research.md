# Research: Multi-Child Events

## R1 — Entry point: room roster multi-select, not a "toggle before the child picker"

**Decision**: Multi-select is a mode on the existing room roster screen (`mobile/app/(app)/index.tsx`,
the `GroupViewScreen`'s "children" tab), not a toggle inserted into `QuickActionSheet`.

**Rationale**: The BACKLOG prompt assumed a "child picker" step exists in the event-logging flow
for a toggle to sit "before." Reading `QuickActionSheet.tsx` and `child/[id].tsx` shows the actual
flow is child-first: the caregiver taps a specific child's card on the roster, navigates to that
child's detail screen, and only then picks an event type. There is no intermediate "pick an event
type, then pick a child" screen anywhere in the shipped 009/009a UI. Introducing multi-select as a
roster-level mode (matches the BACKLOG prompt's own literal framing — "caregiver selects multiple
children before logging an event") lets it reuse the roster's already-present children, photos,
and attendance state (`attendanceByChildId`) rather than building a second, parallel child-listing
component.

**Alternatives considered**: A dedicated "start batch event" screen reachable from a new button —
rejected as an unnecessary extra navigation hop and a second implementation of "list the room's
present children," when the roster screen already is that.

## R2 — Prerequisite fix: `GET /api/children` and `GET /api/groups` must accept device tokens

**Decision**: Add `DeviceToken` as an accepted authentication scheme (alongside the existing
`staff`/`director` role JWT) on `GET /api/children` and `GET /api/groups`, mirroring the
`DeviceOrDirector` composite policy feature 009 already added for `PATCH`/`DELETE
/api/child-events/{id}`.

**Rationale**: `mobile/services/apiClient.ts`'s request interceptor always prefers the device
token over any personal session token once one is stored (`deviceToken ?? auth?.accessToken`) —
true for every `apiClient` call, unconditionally. `index.tsx`'s `fetchChildren()` (the room
roster's own data source, which 009c's multi-select grid must reuse) calls exactly `GET
/api/groups` and `GET /api/children`, both currently gated by the `StaffOrDirector` role policy
(`RequireRole("staff", "director")`, feature 008 — predates 008a's kiosk/device-token model). A
paired kiosk tablet's device token carries `TenantId`/`DeviceId`/`LocationId`/`GroupId`/
`TokenVersion` claims only (`DeviceTokenService.GenerateDeviceToken`) — no role claim — so
`RequireRole` fails it with a 403. This is a real, pre-existing gap in already-merged features
(008a/009/009b/010/012 all render this same roster screen), not something 009c introduces, but
009c's own new UI cannot be meaningfully built or tested on top of a data path that 403s for the
exact session type (kiosk/device-token) this whole screen exists to serve. Confirmed by reading
`GroupsEndpoints.cs`/`ChildrenEndpoints.cs` (both `StaffOrDirector`-only) and
`CaregiverReadScopingTests.cs` (its own coverage of these two routes authenticates via
`CreateAndLoginCaregiverAsync` — a real per-caregiver email/password login, i.e. feature 008's
pre-kiosk model — never a device token). Confirmed with the user before fixing, per this project's
standing rule to pause on a genuinely new, high-impact, no-precedent finding rather than either
silently expanding scope or silently building on top of a suspected-broken foundation.

**Scope of the fix**: Authentication only — add `DeviceToken` to the accepted schemes for these
two `GET` routes, exactly as `DeviceOrDirector` already does elsewhere. `ListChildrenQuery`'s
existing `CallerIdentity`-based location-scoping (`role`/`tenantUserId`, feature 008 research.md
R6) is left untouched for staff/director callers; a device-token caller passes `role = null`,
`tenantUserId = null` through unchanged — the query already tolerates this shape (director callers
already pass through the same scoping branch unused). No change to write routes, no change to any
other endpoint.

**Alternatives considered**: A claims transformer that synthesizes a `"staff"` role claim for any
`DeviceToken`-authenticated principal — rejected as broader and less explicit than adding the one
already-precedented composite-scheme policy; it would also implicitly grant device tokens access
to every other `StaffOrDirector` route without a per-route decision, which is a larger blast radius
than this feature needs to reason about or test.

## R3 — Batch endpoint scope model matches the single-child endpoint

**Decision**: `POST /api/child-events/batch` is `DeviceAuthenticated` only (same policy as `POST
/api/child-events`). Every `child_event` row the batch creates uses the submitting device token's
own `LocationId`/`GroupId` claims — exactly like `RecordChildEventCommandHandler` already does for
the single-child endpoint. This is not a per-`child_id` value.

**Rationale**: `RecordChildEventCommandHandler` (feature 009) takes `LocationId`/`GroupId` from
`DeviceClaimsOf(ctx)` at the endpoint layer, never from the request body — the endpoint's own
comment states this explicitly ("come from the recording device's own JWT claims... never
client-supplied"). The batch endpoint should not invent a different, stricter model (a per-child
group-scope check) than the one the single-child endpoint already established; doing so would be
new, uncalled-for enforcement rather than consistent behavior. Because the multi-select grid is
itself built only from the device's own room roster (R1/R2), an out-of-scope `child_id` is not
reachable through the UI in the first place.

## R4 — Presence validation is new, batch-specific — not a gap being closed elsewhere

**Decision**: The batch endpoint validates, per `child_id`, that the child has an `AttendanceRecord`
(feature 010) for today at the device's `LocationId` with `Status = Present` and `CheckOutAt ==
null`, at the moment that child's row is written. A child failing this check is reported as a
per-child failure (`ChildEventBatchFailureReason.NotPresent`), not a whole-batch failure.

**Rationale**: `RecordChildEventCommandHandler` performs no presence/attendance check today for
the single-child endpoint — it only checks the child record exists at all
(`ChildEventFailure.ChildNotFound`). This feature is not "restoring" a check the single-child flow
already has; there isn't one. The reason to add it specifically here: a batch's selection window
(caregiver selects up to 30 children, then still has to fill in the event form) is materially
longer than the single-child flow's few-second tap-to-submit, making a "checked out mid-selection"
race realistic in a way it isn't for the single-child path — and the feature's own spec (edge
cases, User Story 2) explicitly calls for this behavior. `AttendanceRecord` (not `ChildEvent`) is
the correct source of truth for presence, since it's the entity feature 010 built specifically to
own that concept.

## R5 — Per-child transactional isolation and idempotency

**Decision**: The batch handler iterates the (already deduplicated, capped-at-30) request items in
a loop, calling `db.SaveChangesAsync()` after each individual child's checks/write, inside a
try/catch per iteration. A failure on one child's iteration is caught, recorded as that child's
failure reason, and the loop continues to the next child — it does not roll back or abort
previously-succeeded children. Each item carries a client-generated `id`
(`contracts/child-events-batch-api.md`); before creating a child's row, the handler checks whether
that `id` already exists (reusing `RecordChildEventCommandHandler`'s existing idempotency-by-id
check, FR-013a) and, if so, reports that child as `created` using the existing row instead of
inserting a duplicate.

**Idempotency rationale (checklist finding CHK017)**: without a per-child id, a batch retried after
an ambiguous failure (e.g. the server commits several children's rows, then the connection drops
before the client receives the response) would have no way to distinguish "this child already
succeeded" from "this child was never attempted" on the next replay attempt — a naive retry would
double-create events for the children that already succeeded. A per-child client-generated id,
mirroring the single-child endpoint's own established idempotency mechanism, closes this without
inventing a new pattern.

**Rationale**: EF Core's `SaveChangesAsync()` wraps only the currently-tracked pending changes in
one transaction; calling it once per child after that child's `Add` (rather than once at the end
for the whole batch) is what makes per-child isolation possible with the existing `ITenantDbContext`
pattern, with no new infrastructure. At a 30-child cap this is at most 30 round-trips — acceptable
per the spec's own priority ordering ("per-child transactional isolation and partial-success
correctness take priority over raw throughput at this scale"). A single N-row bulk insert wrapped
in one transaction was considered and rejected: it cannot express partial success (one bad row
would either fail the whole batch or require complex manual savepoint management for a 30-row
upper bound that doesn't justify the complexity).

## R6 — Offline queue: one entry, and partial-failure results must survive replay

**Decision**: The mobile batch submission is queued via the existing generic `enqueue()`
(`entity_type: "child_event_batch"`, `operation: "create"`, `endpoint: "/api/child-events/batch"`,
`httpMethod: "POST"`) — one row per batch submission, replayed by the existing `syncEngine.ts`
`replay()` exactly like any other queued row. `syncEngine.ts`'s `response.ok` branch is extended
with one additional, narrow check: for `entity_type === "child_event_batch"`, after a 2xx
response, the body is parsed and if its `errors` array is non-empty, the row is marked via
`markSyncError` with a `"partial: "`-prefixed message (counted as `failed`, not `succeeded`)
instead of `markSynced` — reusing the same "needs review" convention feature 009's `"rejected: "`
prefix already established for `EventTimeline`, rather than inventing a second mechanism. A fully
successful batch (empty `errors`) is marked synced exactly as before.

**Rationale**: Without this, a partial failure discovered only during background sync (the
caregiver who submitted while offline is not watching a retry UI at that moment) would be silently
absorbed as "succeeded" by the existing generic `response.ok` → `markSynced` path, since a 2xx
with a non-empty `errors` array is still `response.ok`. The spec's SC-004 explicitly requires the
offline-replayed result to match the online one — this small, precedented extension is the
narrowest way to satisfy that without building a new review surface. The caregiver app's existing
"needs review" UI convention (`EventTimeline` reading a `"rejected: "` prefix) is reused for a
`"partial: "` prefix rather than duplicated.

**Retry is manual, not automatic (checklist findings CHK015/CHK016)**: a sync-time partial failure
is not auto-retried on the next sync cycle — it surfaces as a "needs review" item exactly like a
`"rejected: "` one already does, and the caregiver resolves it the same way they already resolve
any other item in that state (open the child's timeline, see the flagged event, decide what to
do), rather than this feature inventing a second, batch-specific review flow.

## R7 — Client UI shape

**Decision**: Multi-select mode is entered via an explicit button in the roster screen's header
(not a gesture — long-press on a child card is already used for marking absence, FR-005/017 of
feature 010, so it's unavailable). While active, each present child's existing roster card gains a
selected/unselected visual state (checkmark, per `design-system.md`'s "never color alone" rule) in
place of its normal check-in/out tap behavior. A bottom action bar appears once ≥1 child is
selected, showing the count and a "Log event" button. Tapping it opens `QuickActionSheet` in a
"batch mode" — the same component, given a `childIds: string[]` instead of a single `childId`,
with `EVENT_TYPES` filtered to the eight multi-select-eligible types (excludes `temperature`,
`medication`, `weight`, `growth_check`, which also excludes the `AdministratorConfirmation` PIN
step entirely, since none of the batch-eligible types need it). On submit, `QuickActionSheet` calls
a new `recordChildEventBatch()` (mirrors `recordChildEvent()` in `childEvents.ts`) instead of
`recordChildEvent()`.

**Rationale**: Reusing `QuickActionSheet` for the form step (rather than building a second sheet)
keeps the event-type icons, quick-choice rows, and per-type field layouts identical between
single- and multi-child flows, per FR-004's "form is identical to the single-child flow"
requirement — the only thing that changes is what happens on submit and which event types are
offered.

## R8 — i18n

**Decision**: New keys added under the existing `groupView.*` and `childEvents.*` namespaces
(`groupView.multiSelect.*`, `childEvents.batch.*`) across all three locale files (`en`, `nl`,
`fr`), following the existing flat-key convention in `mobile/i18n/`.
