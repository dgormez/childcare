# Research: Vaccine & Health Records

## R1 — Legacy `vaccination_records` table (feature 006) supersession

**Decision**: Migrate and replace. The existing `VaccinationRecord` entity, `vaccination_records`
tenant table, `RecordVaccinationCommand`/`ListChildVaccinationsQuery`
(`ChildCare.Application/Groups/`), and `GET/POST /api/children/{id}/vaccinations` endpoints
(`GroupsEndpoints.cs`) are removed entirely. A new EF Core migration drops `vaccination_records`
and creates `vaccine_records` (this feature's richer schema: `dose_number`, `administered_by`,
`notes`, `recorded_by`, soft-delete) plus a data migration step that copies any existing
`vaccination_records` rows into `vaccine_records` (mapping the four shared columns; new columns
default to null/not-deleted) before the drop.

**Rationale**: The new schema is a near-superset of the old one (same `child_id`/`vaccine_name`/
`date_administered`(`administered_on`)/`next_due_date` core, plus richer fields this feature's own
BACKLOG prompt specifies). Confirmed via codebase search that zero client code (mobile/web/
parent-mobile) actually calls the old endpoints — only the auto-generated, unused
`api-types.ts` type definitions reference them — so there is no live consumer to break. Mirrors
feature 009a's precedent of consolidating an overlapping concept via a backfill migration rather
than shipping two parallel, confusing mechanisms (measurement→growth_check). Confirmed with the
user directly (this was a genuinely new, no-precedent scope question raised only by planning-phase
codebase research, not by the BACKLOG prompt itself — see the standing process rule on pausing for
such questions).

**Alternatives considered**:
- *Leave the legacy table/endpoints untouched, build a fully separate new table*: rejected — ships
  two overlapping vaccination concepts permanently, orphans any real tenant data already recorded
  in `vaccination_records`, and contradicts this project's own precedent (009a) and standing
  "fix debt now, don't defer" rule.
- *Keep old endpoints as thin backward-compat wrappers over the new table*: rejected as
  unnecessary — no consumer exists to be compatible with; the wrapper would be pure dead code.

## R2 — Health-record attachment storage port

**Decision**: A new, dedicated signed-URL storage port, `IHealthAttachmentStorage`, rather than
reusing `IProfilePhotoStorage` directly or generalizing it further.

**Rationale**: `IProfilePhotoStorage`'s object path is hardcoded to `{category}/{subjectId}/
photo.jpg` (`GcsProfilePhotoStorage.cs`) — a fixed `.jpg` extension baked into the contract,
appropriate for "exactly one photo per subject" but wrong for a health-record attachment, which
may be a PDF or an image of varying type. `IHealthAttachmentStorage` follows the exact same
shape/pattern (category + subjectId → signed upload/download URL pair, GCS V4 signing, no public
URLs) but accepts a content-type/extension at upload time, storing it as
`health-records/{healthRecordId}/attachment.{ext}`. This mirrors `IGroupActivityPhotoStorage`'s
precedent of "same signed-URL idiom, new port, when the shape genuinely differs" rather than
bending an existing single-purpose interface. Uses the existing `Storage:ProfilePhotosBucketName`
GCS bucket (category-segmented, same bucket already serves staff/child photos) — no new bucket or
Terraform change needed, since the category prefix already provides isolation.

**Alternatives considered**:
- *Extend `IProfilePhotoStorage`'s signature with an extension parameter*: rejected — would
  change a stable, already-shipped interface's contract for every existing caller (staff/child
  photo upload flows) to serve a new, unrelated use case.
- *Store attachments as base64 in the `health_records` row*: rejected — contradicts constitution
  Principle VI (signed URLs only) and this codebase's established pattern for any binary content.

## R3 — Caregiver read-only health/allergy summary access control

**Decision**: `GetChildHealthSummaryQuery` (new, in `ChildCare.Application/Children/`) reuses the
exact `StaffLocationEligibility`-based scoping already implemented in `GetChildByIdQuery`
(`Children/GetChildByIdQuery.cs`) — join `StaffProfiles`→`StaffLocationEligibility` for the
caller's eligible `LocationId`s, then require the target child to have an active
`ChildGroupAssignment` in a `Group` at one of those locations. Out-of-scope access returns the
same generic not-found response as a nonexistent child id (never reveals existence), matching
FR-007a's existing precedent. The endpoint is registered under the existing `DeviceOrStaffOrDirector`
authorization policy (`Program.cs`) — a kiosk device-token caller (feature 008a) is authorized by
the device token itself (no staff-scoping branch applies, consistent with how device-token callers
are already treated on `ChildrenEndpoints.cs`); a per-caregiver JWT caller (if ever used on this
route) goes through the location-eligibility check.

**Rationale**: This is the identical access-control shape feature 008 already built and feature
009's `health-safety.md` already documents (FR-007a) — no new authorization concept is needed
model, only a new query returning a different (richer) response shape.

**Alternatives considered**: None seriously considered — inventing a parallel eligibility check
for the same actor/resource pair this codebase already solved would be exactly the kind of
avoidable duplication the constitution's simplicity principle warns against.

## R4 — "Vaccinations due soon" dashboard aggregation query

**Decision**: `ListVaccinationsDueSoonQuery` (new — first instance of this "due within N days"
pattern in the codebase; no existing precedent to mirror). A single query against
`vaccine_records` joined to `children` (and `children`'s active location, via the same
`ChildGroupAssignment`→`Group`→`Location` path `GetChildByIdQuery` already uses), filtered to
`next_due_date <= today + 30 days AND is_deleted = false`, with `is_overdue` computed as
`next_due_date < today` (a due date of exactly today is due-soon, not overdue — spec.md FR-009),
scoped to the signed-in director's
accessible locations, returning one row per (child, soonest-or-most-overdue vaccine record) —
not one row per vaccine record, so a child with multiple due-soon vaccines appears once, showing
its most urgent one. Sorted by `next_due_date` ascending (most overdue/soonest first). A
supporting index on `vaccine_records (child_id, next_due_date)` avoids a full-table scan and, with
the join, avoids the N+1 pattern FR-020 (spec's Technical Requirements) rules out.

**Rationale**: Straightforward aggregate query, no different in kind from any other director-web
list query in this codebase (e.g. `ListIncidentReportsQuery`'s pagination/filter pattern) — the
only genuinely new piece is the date-threshold filter and the "one row per child, not per record"
collapsing rule, both spec-driven (FR-009/FR-010).

**Alternatives considered**:
- *Denormalized "next due" column on `children`, updated on every vaccine write*: rejected —
  premature optimization for the scale this system operates at (tens to low hundreds of children
  per tenant, per plan.md's Scale/Scope); adds write-path complexity and a cache-invalidation
  surface for no measured need.

## R5 — MediatR folder/file convention

**Decision**: Mirror `IncidentReports/`'s flat-folder-per-aggregate layout exactly: one folder per
entity (`VaccineRecords/`, `HealthRecords/`), each command/query file self-contained (record +
FluentValidation validator + handler together, no separate `Commands/`/`Queries/` subfolders), a
static `Mapper` class, and a `Result`/`Failure` wrapper type per aggregate.

**Rationale**: Established, consistent convention across every feature since 001 — no reason to
deviate.

## R6 — Tenant migration rollout test coverage

**Decision**: Extend `TenantMigrationRolloutTests.cs`'s `RevertToPreExtensionSchemaAsync` to (a)
remove the now-obsolete `DROP TABLE "{schema}"."vaccination_records"` line (replaced by this
feature's own migration dropping it), (b) add `DROP TABLE "{schema}"."vaccine_records";` and
`DROP TABLE "{schema}"."health_records";` — both before `children` in drop order since both FK to
it — and (c) add this feature's migration name(s) to the `__EFMigrationsHistory` cleanup clause.

**Rationale**: Every migration-adding feature since 003 has needed this identical fix (per
BACKLOG.md's own shipped-notes, most recently 012a and 013b) — checked and extended as a matter of
course, not a discovery.
