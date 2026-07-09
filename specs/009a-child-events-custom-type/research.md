# Research: Child Events — Custom Type & Growth Check Rename

## R1 — Migration mechanism for the `measurement` → `growth_check` rename

**Decision**: Add a new CLI subcommand, `backfill-growth-check`, that mirrors the existing
`migrate-tenants` subcommand's tenant-loop structure (`ChildCare.Api/Cli/MigrateTenantsCommand.cs`):
loop over every `Ready` tenant from `PublicDbContext.Tenants`, and run a single schema-qualified
raw SQL statement per tenant directly via `PublicDbContext.Database.ExecuteSqlRawAsync`
(**correction made during implementation**: `ITenantDbContext` has no raw-SQL-execution member —
only DbSets and `MigrateAsync`/`HasPendingMigrationsAsync` — so `ITenantDbContextResolver.ForSchema`
isn't the right tool here; `PublicDbContext`'s own connection can run a fully schema-qualified
statement directly, the same pattern `ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` already
uses to reach into a specific tenant schema for test setup):

```sql
UPDATE "<schema_name>".child_events SET "EventType" = 'growth_check' WHERE "EventType" = 'measurement';
```

executed via `db.Database.ExecuteSqlRawAsync(...)` (parameterizing only the fixed literal
strings, never user input — the schema name comes from `PublicDbContext.Tenants`, not request
input). Reports a per-tenant row-count and a final summary, same shape as `migrate-tenants`'s
`{slug}: migrated` / `{slug}: failed — {message}` output.

**Rationale**: This is a one-time data value change, not a DDL schema change (`child_events`'s
`event_type` column stays `text`, per feature 009's `ChildEventTypeExtensions`-driven value
converter) — so it doesn't belong as an EF Core migration file. Using raw SQL against the
already-persisted string value, rather than loading rows through EF Core's `ChildEventType`
value converter, deliberately avoids ever needing the C# enum to parse the literal string
`"measurement"` at all — sidestepping a real deployment-ordering hazard (see R2).

**Alternatives considered**:
- An EF Core migration performing the same `UPDATE` in its `Up()` method — rejected because
  `MigrateTenantsCommand` already exists specifically for schema migrations and conflates a
  one-time data fix with the ongoing schema-migration mechanism; a dedicated command keeps the
  two operationally distinct (a schema migration failure and a data-backfill failure have very
  different blast radii and recovery steps).
- Loading `ChildEvent` rows via the `DbSet<ChildEvent>` and rewriting `EventType` in memory —
  rejected because materializing each row requires successfully parsing its current
  `event_type` string through `ChildEventTypeExtensions.TryParseWireString`, which is exactly
  the parse path this rename is removing support from (see R2). Raw SQL never invokes that path.

## R2 — Deployment ordering: avoiding a read-time break on un-backfilled rows

**Decision**: `backfill-growth-check` MUST be run (once, against every tenant) as a deploy-time
step *before* the new application binary (the one whose `ChildEventTypeExtensions` no longer
recognizes `"measurement"` at all) starts serving traffic — the same manual,
operator-triggered-before-deploy discipline the constitution already requires for schema
migrations (Principle VI: "EF Core migrations MUST NOT auto-apply in production; a SQL script
is generated and reviewed/run manually"). This is documented as an explicit pre-deploy step in
`quickstart.md` and the feature's PR description, not automated into app startup.

**Rationale**: `ChildEventTypeExtensions.TryParseWireString`/`ToWireString` is a hard cutover
(FR-008: no dual-write compatibility window) — once the new binary is live, any `child_events`
row still holding the literal string `"measurement"` would fail to parse when EF Core's value
converter reads it back (`ParseChildEventType` throws `FormatException`), breaking every
list/timeline/daily-summary query that touches that row. Running the backfill first eliminates
every such row before the new code path can ever encounter one. This mirrors exactly how a
column-adding EF Core migration must run before code that assumes the column exists.

**Alternatives considered**:
- Keep `TryParseWireString` tolerant of `"measurement"` as a read-only backward-compatible
  alias indefinitely — rejected: contradicts FR-008/FR-009's explicit "one-time cutover, not an
  ongoing dual-write window" requirement, and leaves permanent, untestable dead code whose only
  purpose is masking a missed deployment step.
- Make the backfill part of the app's own startup (auto-run on boot) — rejected for the same
  reason `001`'s new-tenant-schema provisioning carve-out is scoped narrowly: this touches
  existing tenant schemas with live data, which constitution Principle VI's core rule (no
  auto-apply against populated schemas) squarely covers; the carve-out only exempts brand-new,
  empty schemas.

## R3 — `custom` payload shape and validator integration

**Decision**: Add `ChildEventType.Custom` (wire string `"custom"`, falls through
`ChildEventTypeExtensions`'s default `ToString().ToLowerInvariant()` case since it's a single
word — no special-case entry needed, same as `Sleep`/`Diaper`/etc.). Note: `GrowthCheck` is
**not** in the same situation — like `FeedingBottle`/`FeedingSolid`, it's a multi-word C#
identifier whose default-cased form (`"growthcheck"`) doesn't match the required snake_case
wire value (`"growth_check"`), so it needs its own explicit `ToWireString`/`TryParseWireString`
case, same as those two. Add a `Custom` entry to
`ChildEventPayloadValidator.AllowedFields`: `["label", "text"]`, with a new `RequireString`
check on `label` (already-existing helper, reused as-is) plus a length cap (100) enforced via a
new small range check alongside the existing `RequireString`/`RequireDecimalInRange` helpers.
`text` uses the existing optional free-text pattern (no required-field call), matching how
`Medication.reason`/`Diaper.notes` already handle optional string fields in this validator.

**Rationale**: Every other event type in this validator follows the same
allowed-fields-plus-per-type-switch shape; `custom` needs no new validation primitive, only a
new switch arm — consistent with the "no new exception paths" requirement in spec.md's FR-005.

## R4 — Mobile: quick-action sheet placement and timeline rendering

**Decision**: `custom` is added to `QuickActionSheet.tsx`'s free-text bucket alongside
`activity`/`note` (a text-entry form, not a choice-list `labelPrefix` picker) — it structurally
requires typing (a label), so it cannot satisfy 009's FR-021 2-tap constraint the same way
`diaper`/`mood` do; this mirrors how `activity`/`note` are already exempted from that constraint
in feature 009's own spec. `EventTimeline.tsx` gains one new render case: `custom` events display
`payload.label` as the row's headline (same visual slot other types use for their computed
one-line summary) with `payload.text` (if present) rendered as secondary/detail text beneath it
— no new list-item component, just a new switch arm in the existing per-type renderer.

**Rationale**: Reuses 009's existing sheet/timeline architecture exactly; no new UI primitive is
introduced for a single new type, consistent with the standing "don't reinvent a component that
already exists" instruction.

## R5 — Existing test fixtures referencing `measurement`

**Decision**: Grep the backend/mobile test suites for literal `"measurement"` string fixtures
and rename them to `"growth_check"` in place, as part of this feature's implementation — not
left passing against a now-invalid value (spec.md Assumptions).
