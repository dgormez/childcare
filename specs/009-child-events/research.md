# Research: Child Event Timeline

All items below were resolved by reading existing shipped code rather than external research —
this feature builds directly on infrastructure features 006/007/008/008a already established.
No NEEDS CLARIFICATION markers remained in Technical Context.

## R1: Payload storage — per-type owned JSON columns vs. one generic JSON column

**Decision**: A single `jsonb` column (`Payload`), mapped as a raw string in EF Core
(`HasColumnType("jsonb")`), holding a `System.Text.Json` document whose shape depends on
`EventType`. Validated in the Application layer by a `ChildEventPayloadValidator` that switches
on `EventType` and applies the field set/types from spec.md's event-type table (via
FluentValidation), not by EF Core owned-type mapping.

**Rationale**: Feature 007's `Contract.ContractedDays`/`Consent` use `OwnsMany`/`OwnsOne` +
`ToJson()` because each Contract has exactly one fixed shape. `child_events` is different: 11
distinct payload shapes share one table by design (constitution's Development Workflow section:
"single JSONB-backed table — do not create a separate table per event type"). EF Core's owned-JSON
mapping requires one CLR type per owned property; a discriminated union of 11 shapes doesn't fit
that model without either 11 nullable owned properties (schema clutter, most always null) or a
`Dictionary<string, object>` (loses compile-time shape safety for the 90% of code that only
touches one type at a time). A raw JSON string column plus an Application-layer typed
serialize/validate step keeps the table narrow and puts per-type validation exactly where
FluentValidation already lives for every other command in this codebase (constitution Principle
III).

**Alternatives considered**:

- One nullable column per possible field (wide table) — rejected: 20+ mostly-null columns,
  fights the "single JSONB table" constitution line directly.
- A separate table per event type — rejected: explicitly the thing the constitution line says
  not to do, and would require the timeline query to UNION 11 tables instead of one indexed scan.
- EF Core owned-entity JSON per type behind an inheritance hierarchy (TPH) — rejected: EF Core's
  JSON column mapping doesn't compose cleanly with TPH discriminators for this version; adds
  complexity with no query benefit since almost every access pattern is by `(child_id,
  occurred_at)`, not per-field filtering inside the payload.

## R2: `recorded_by` / `administered_by` attribution

**Decision**: Reuse `IShiftAttributionService.ResolveRecordedByAsync(locationId, groupId,
occurredAtUtc)` (already built in `backend/ChildCare.Application/RoomShifts/`, whose own doc
comment names "feature 009/010's command handlers" as its intended caller) to populate
`RecordedBy` (a `Guid[]`/JSONB array column, per spec's data model) at write time. For
`AdministeredBy` on medication/temperature events, reuse the existing
`POST /api/room-shifts/confirm-administrator` endpoint (feature 008a) from the mobile client
before submitting the event — the command already implements the exact "currently checked-in
roster, select-then-PIN, skippable" flow spec.md's User Story 2 (Acceptance Scenario 5)
describes. No new attribution mechanism is built by this feature.

**Rationale**: This exact reuse was the stated intent when `IShiftAttributionService` was built
during feature 008a — building a second attribution mechanism now would contradict that.

**Alternatives considered**: A new child-event-specific attribution query — rejected as pure
duplication of already-tested, already-shipped code.

## R3: Sleep in-progress + offline end-merge

**Decision**: A sleep event is created with `EndedAt = null`. `PATCH` with an `endedAt` value
completes it. On the mobile side, `syncEngine.ts`'s existing `SyncHandler.onBeforeEnqueue` hook
(already defined in `syncEngine.ts`, unused by any handler yet) is used by the new
`child_event` handler: when queuing a sleep-end update, it inspects `offlineQueue`'s pending rows
for a still-unsynced `create` row for the same client-generated event id and, if found, merges
`endedAt`/computed `durationMinutes` directly into that row's payload instead of enqueueing a
second `PATCH` row. If the create has already synced (no matching pending row), a normal `PATCH`
row is queued instead.

**Rationale**: `onBeforeEnqueue`'s signature (`existingPending: QueueRow[], newEntry: unknown`)
was already shaped for exactly this merge use case when feature 008 shipped it, unused until now.
This matches spec.md FR-013 precisely and requires no sync-engine changes — only a handler.

**Alternatives considered**: Always queue a separate PATCH and let the server merge — rejected:
the server has no way to distinguish "PATCH against an id that hasn't arrived yet" from "PATCH
against an unknown id" without a riskier out-of-order-arrival reconciliation step; merging
client-side before the create is ever sent is simpler and matches how the queue already orders
by `created_at`.

**Matching key (CHK007)**: The lookup scans pending `offline_queue` rows where `entity_type =
'child_event' AND operation = 'create'`, parses each candidate row's JSON `payload`, and matches
on the event's own client-generated `id` field embedded in that payload (the same id the sleep-end
action already knows, since it's ending a specific event it's currently displaying) — not a
secondary/derived key.

**Race with an in-flight sync (CHK008)**: `syncPendingQueue()` reads its batch of pending rows
once at the start of a run (`getPending()`), then iterates that in-memory snapshot sequentially.
If the merge check only inspected that same in-memory snapshot, a merge landing after the batch
was read but before that specific row's `replay()` call would update local storage while the
in-flight request still transmits the stale, pre-merge payload captured at batch-read time —
`markSynced` would then mark the row synced with the merge silently never sent. To close this,
`replay()` MUST re-read each row's current `payload` from local storage immediately before
transmitting it, rather than trusting the batch-read snapshot — this guarantees a merge applied
at any point before actual transmission is always picked up. Once a row's response has been
received and `markSynced` has run, the merge check (which runs against local storage, the same
source `markSynced` updates) correctly sees `synced_at` set and falls back to enqueuing a normal
`PATCH` row instead (FR-013's "already synced" branch) — the write is never silently lost in
either ordering.

## R4: Same-day edit authorization — "any caregiver at the location"

**Decision (corrected during implementation)**: `ChildEventEditWindowPolicy` checks: (a) caller
is a Director (user JWT) → always allowed; (b) caller is a paired tablet (device token) →
allowed only if `OccurredAt`'s `Europe/Brussels`-anchored calendar day (research.md R8) equals
today, AND the requesting device's own `LocationId` claim (the same claim
`RoomShiftEndpoints.DeviceClaimsOf` already extracts) matches the event's `LocationId`.

The original design (spec.md's first 2026-07-08 clarification session) called for checking a
*caregiver's* `StaffLocationEligibility` row. That doesn't hold up: routine tablet actions are
device-token authenticated only (constitution's Technology Stack Constraints — "the device token
is the tablet's actual security boundary... individual caregivers then identify via a 4-digit PIN
... accountability tracking, not a second HTTP authentication mechanism"), so there is no
individual caregiver identity attached to an edit/delete request to check eligibility against.
Discovered while implementing `ChildEventEditWindowPolicy` (T008) — checking a `StaffId` that
doesn't exist on this auth path isn't just a documentation gap, it's not implementable as
originally specified. Corrected to a device-location check instead, which still delivers the
clarification's actual intent (any caregiver physically at that location's tablet can correct any
same-day event recorded there — feature 008a's shared-room reality) without requiring an identity
the auth model doesn't provide.

**Rationale**: A tablet is permanently paired to one location/group (feature 008a); "which
caregiver is at this tablet right now" was never authenticated for routine actions, only tracked
via the shift-presence log for accountability. Checking the device's own location claim is the
only caregiver-facing eligibility signal actually available on this request, and it reproduces
the intended team-correction behavior exactly: any of the 1-2 caregivers physically present at
that room's tablet can fix a same-day mistake.

**Alternatives considered**: Track `RecordedBy`-based ownership for edit permission — rejected by
the original clarification answer; also awkward since `RecordedBy` can already be a multi-caregiver
array or null (spec.md FR-003/edge cases). Checking `StaffLocationEligibility` for a specific
staff id — rejected once discovered there is no staff id available on the device-token auth path
to check it against (see above). Requiring a PIN-confirmed staff identity before every edit (to
make a `StaffLocationEligibility` check meaningful) — rejected as disproportionate: it would turn
every routine correction into a PIN-gated action, contradicting FR-021's low-friction quick-entry
goal and 008a's explicit "PIN is for accountability on sensitive actions, not routine gating."

## R5: Temperature push notifications — Expo Push Notification Service, server-triggered only

**Decision**: A new `ITemperatureAlertService`/`ExpoPushSender` pair in `Infrastructure/Push/`
sends via Expo's HTTP push API directly from the `RecordChildEventCommandHandler` (or a MediatR
notification handler fired after a successful temperature-event save) — never from the mobile
client. Recipients are resolved via existing `ChildContact` rows with `CanPickup = true`. Per the
2026-07-08 clarification (Q1), no interim push-token registration path for `Contact` is built by
this feature: since no parent-facing client exists yet to obtain a token, the lookup will
currently find zero deliverable recipients in practice. The dispatch mechanism and its trigger
condition (>38.0°C, per spec.md) still ship now so the future parent-app feature only needs to
add token registration, not alerting logic.

**Rationale**: `expo-notifications` is already an installed mobile dependency (used for the
caregiver app's own token per feature 009's originating backlog text: "Register caregiver app
push token at login" — out of scope here per spec's User Story boundaries; that registration
already isn't this feature's push-recipient target, contacts are). Building a full contact-facing
registration flow now would be scope creep ahead of the parent app existing, per the
clarification answer.

**Alternatives considered**: Defer building any push mechanism until the parent app exists —
rejected: the spec's User Story 2 (P1) explicitly requires the alert *logic and threshold
detection* to exist now; only the recipient-registration gap is deferred, not the feature itself.

**Dispatch-failure and repeat-alert handling (CHK014/CHK015)**: A transport-level failure calling
Expo's HTTP push API (as opposed to FR-011's "no eligible recipients" case) is caught, logged,
and treated the same as a successful dispatch attempt from the event-save's point of view (FR-
011a) — the temperature event itself must never fail to save because a notification couldn't be
sent. No cooldown/de-duplication window is applied across multiple qualifying readings for the
same child (FR-011b) — each event is independent; suppressing a second genuine high reading
shortly after the first would be a real patient-safety regression for a marginal notification-
volume benefit.

## R8: Calendar-day boundary for edit window and daily summary

**Decision**: Both the same-day edit window (FR-006) and the daily summary's "one calendar day"
(FR-017/FR-018a) use a single fixed `Europe/Brussels` reference, applied server-side, rather than
a per-location timezone.

**Rationale**: `Location` (feature 004) has no timezone field, and the product's constitution
scopes Phase 1 to Belgian KDVs exclusively — there is currently no multi-country requirement
anywhere else in the codebase that would justify introducing a per-location timezone column just
for this feature. Anchoring both day-boundary-sensitive rules to the same fixed reference also
guarantees they can never silently drift apart from each other (CHK022).

**Alternatives considered**: Add a `TimeZone` field to `Location` now — rejected as speculative
scope beyond this feature's need; if a future feature (e.g., multi-country expansion) needs
per-location timezones, it can add the column and this feature's two call sites are the only
places that would need to switch from the fixed constant to a per-location lookup.

## R6: Pagination

**Decision**: Cursor-style pagination on `ListChildEventsQuery` ordered by `(occurred_at DESC,
id)`, using an opaque `before` cursor (last-seen `occurred_at`+`id`) rather than page-number
offset pagination.

**Rationale**: This table is explicitly called out in the constitution/spec as high-growth
("This table grows fast" — spec.md Key Constraints). Offset pagination degrades on large offsets
and shifts under concurrent inserts (a busy room inserting new events while a director scrolls
older history); cursor pagination avoids both, and the existing `(child_id, occurred_at DESC)`
index (spec.md FR requirement) serves it directly with no extra index needed.

**Alternatives considered**: Page-number/offset pagination — rejected for the reasons above; no
existing feature in this codebase has needed pagination yet, so there's no established
precedent to match instead.

**Ordering-guarantee scope (CHK010)**: FR-014's offline-delivery ordering guarantee ("process
them in the order they originally occurred") applies per-tablet only — each device's own
`offline_queue` is replayed in its own `created_at ASC` order (unchanged from feature 008's
existing sync engine behavior). Events from two different tablets are never ordering-guaranteed
relative to each other and may interleave arbitrarily on arrival, which is safe because routine
events are independent, non-conflicting records (spec.md: "ALL WRITES PRESERVED") — the only
cross-tablet case requiring a deterministic outcome (two tablets ending the same sleep event) is
handled separately by FR-015/R3, not by this ordering guarantee.

## R9: EF Core migration approach

**Decision**: A standard EF Core migration adding `child_events` to the tenant migration set
(`ChildCare.Infrastructure/Persistence/Migrations/Tenant/`), applied the same way every prior
tenant-schema change has been (via the existing per-tenant migration-runner mechanism from
feature 002) — no new migration mechanism needed.

**Rationale**: No deviation from established practice; this is a new table, not a schema-strategy
change.

## R10: Permanent vs. transient sync failure (analyze finding C1)

**Decision**: `syncEngine.ts`'s response handling currently branches only on `200`s, `409`, and
`401`, falling through to a single "transient — leave pending, retry next run" branch for
everything else, including a `422`. FR-014a requires a genuine validation rejection to surface as
a distinct "needs review" state rather than retry forever, so `replay()`'s response handling adds
one more branch: a `422` marks the row with a `sync_error` value distinguishable from an ordinary
transient failure (e.g. prefixed `"rejected: "`), which `EventTimeline` reads to render the
"needs review" state instead of "pending sync."

**Rationale**: Without this, a genuinely malformed offline-queued event (rare, since the same
validation already ran client-side before queuing — spec.md Assumptions) would retry
indefinitely every sync run with no path to resolution, since nothing about the row or the
request changes between retries.

**Alternatives considered**: Treat a `422` the same as any other failure (status quo) — rejected,
since it directly contradicts FR-014a, discovered during `/speckit-analyze`'s cross-artifact
review (finding C1).
