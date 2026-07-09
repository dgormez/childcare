# Quickstart: Child Events — Custom Type & Growth Check Rename

## Prerequisites

- Backend running locally against Docker PostgreSQL, same dev setup as feature 009.
- A tenant with at least one location/group/child, and a paired kiosk device with a caregiver
  checked in (feature 009's own quickstart prerequisites, reused as-is).
- For the rename verification: at least one pre-existing `child_events` row with
  `event_type = 'measurement'` — seed one directly if the dev tenant has none.

## Migration verification (run first, before the new build serves traffic)

1. Seed or confirm a `measurement` row exists:
   ```sql
   select id, event_type, payload from child_events where event_type = 'measurement';
   ```
2. Run the backfill CLI command:
   ```bash
   dotnet run --project backend/ChildCare.Api -- backfill-growth-check
   ```
   Expect one line per tenant (`{slug}: N row(s) updated`) and a final summary
   (`Summary: X/X tenants succeeded.`).
3. Re-run the same `select` — expect zero rows with `event_type = 'measurement'` and the
   previously-seeded row now reading `event_type = 'growth_check'` with its `weightKg`/`heightCm`/
   `headCm` payload values unchanged.

## Backend validation (curl, device-token authenticated)

1. Record a `custom` event:
   ```bash
   curl -X POST "$API/api/child-events" \
     -H "Authorization: Bearer $DEVICE_TOKEN" -H "Content-Type: application/json" \
     -d '{"childId":"'$CHILD_ID'","eventType":"custom","occurredAt":"'$(date -u +%FT%TZ)'","payload":{"label":"Sunscreen applied"},"visibleToParent":true}'
   ```
   Expect `201` with `payload.label` echoed back unchanged.

2. Attempt a `custom` event with no label:
   ```bash
   curl -X POST "$API/api/child-events" ... -d '{"eventType":"custom","payload":{},...}'
   ```
   Expect `422` with a `label` field error.

3. Record a `growth_check` event (post-migration):
   ```bash
   curl -X POST "$API/api/child-events" ... \
     -d '{"eventType":"growth_check","payload":{"weightKg":9.2,"heightCm":72.5},...}'
   ```
   Expect `201` — identical behavior to the old `measurement` request shape from feature 009's
   quickstart.

4. Attempt to submit the old value after the cutover:
   ```bash
   curl -X POST "$API/api/child-events" ... -d '{"eventType":"measurement","payload":{"weightKg":9.2},...}'
   ```
   Expect `422 errors.child_events.invalid_event_type` — `measurement` is no longer accepted.

## Mobile validation (Expo caregiver app, manual)

1. Open the quick-action sheet for a child — confirm a "Custom" entry appears alongside
   `note`/`activity` (free-text bucket, not the 2-tap choice list).
2. Select "Custom", enter a label with no detail text, save — confirm the timeline shows the
   label as the headline (not a generic "Note").
3. Select "Custom" again, enter both a label and detail text, save — confirm the timeline shows
   the label as headline with the detail text beneath it.
4. Attempt to save "Custom" with an empty label — confirm the save is blocked with a validation
   message, consistent with feature 009's existing required-field UX.
5. Confirm the entry previously labeled "Measurement" in the quick-action sheet now reads as the
   growth-check label (i18n key updated) and behaves identically (same optional weight/height/
   head fields).

## Expected test coverage (backend, TestContainers per constitution Principle V)

- `custom` payload validator: rejects missing/empty/over-length label; accepts label-only and
  label+text; rejects unexpected fields.
- `growth_check` payload validator: identical test coverage `measurement` had in feature 009
  (at-least-one-field-required, per-field range checks), just against the new wire string.
- `backfill-growth-check` CLI: seeds a `measurement` row across 2+ tenant schemas, runs the
  command, asserts every row now reads `growth_check` with unchanged payload values, and asserts
  a schema with zero `measurement` rows completes as a no-op.
- A request submitting the literal `eventType: "measurement"` after the rename ships is rejected
  with the standard validation-error shape.
- Timeline/daily-summary rendering: a `custom` event's `label` surfaces as its headline.

## Expected test coverage (mobile, Jest)

- `QuickActionSheet` renders a "Custom" free-text entry point.
- Submitting `custom` with no label is blocked client-side (or surfaces the server's `422`
  cleanly) consistent with existing required-field UX.
- `EventTimeline` renders a `custom` event's `label` as its headline and `text` as detail.
