# Research: Vaccine Catalog & Attachments

## R1 — Storage location for the shared catalog: `PublicDbContext`, not tenant schema

**Decision**: `vaccine_types` lives in the `public` schema, mapped via a new `DbSet<VaccineType>`
on `PublicDbContext` — the same context that already holds `Tenants`/`Invitations`. It is the
first table added to `PublicDbContext` that is shared reference data rather than a
tenant-management concern.

**Rationale**: Constitution Principle I requires tenant domain data to live in a tenant schema;
`vaccine_types` is the inverse case — data that must be *identical* and readable across every
tenant, never tenant-specific. `PublicDbContext` is the only context that already exists outside
`TenantMiddleware`'s per-request schema switching, so it is the correct home structurally, not a
workaround. This is worth naming explicitly since it is a new category of data for this context
(previously: tenant lifecycle records only) — a future platform-admin-managed catalog (see
spec.md Assumptions) would follow the same placement.

**Alternatives considered**:
- *Duplicate the catalog into every tenant schema (seeded per-tenant, like tenant-scoped
  tables)*: rejected — directly contradicts the "shared, not tenant-specific" requirement; a
  rename would need to fan out to every tenant instead of being one row update.
- *A new, dedicated `PlatformDbContext`*: rejected as unnecessary — `PublicDbContext` already
  exists for exactly this class of non-tenant-scoped data, and Constitution Principle VII
  (Monolith-First Simplicity) counsels against a new context/project without a proven need.

## R2 — `VaccineRecord.VaccineTypeId` has no DB-level foreign-key constraint

**Decision**: `VaccineTypeId` is a plain nullable `uuid` column on `vaccine_records` (tenant
schema), with an index for lookup, but **no** database foreign-key constraint pointing at
`public.vaccine_types`. Referential integrity is enforced at the Application layer only (the
command handler validates the id resolves to an active-or-inactive catalog entry before saving).

**Rationale**: A real FK would be this codebase's first cross-schema constraint (`tenant_x` →
`public`), which EF Core supports only via mapping a read-only shadow entity into
`TenantDbContext` and excluding it from that context's migrations — a first-of-its-kind pattern
with real complexity. It buys nothing here: `vaccine_types` rows are **soft-deleted only**
(spec.md FR-002/FR-010 — "never hard-delete"), so the one failure mode a FK exists to prevent
(a referenced row disappearing) is already structurally impossible by the catalog's own design.
Adding the cross-schema-FK machinery to guard against a deletion that can never happen is exactly
the premature-complexity trap Constitution Principle VII warns against.

**Alternatives considered**:
- *Cross-schema FK via a shadow entity mapping*: rejected per above — real engineering cost,
  zero integrity benefit given soft-delete-only.
- *No index either, just a plain column*: rejected — the picker's "auto-fill from catalog"
  read path and any future due-soon-by-vaccine-type aggregation benefit from the index at
  negligible cost.

## R3 — Tenant-scoped remembered custom entries: new table, case-insensitive dedupe via a normalized-name unique index

**Decision**: New tenant-schema table `tenant_custom_vaccine_entries` (`id`, `name`,
`normalized_name` computed at write time as `RemoveDiacritics(lower(trim(name)))` — case-folded,
whitespace-trimmed, **and** diacritic-stripped (e.g. "Rabiës" → "rabies") via Unicode NFKD
normalization + combining-mark removal, done in C# before the value ever reaches Postgres, not a
Postgres-side `unaccent` extension dependency — `created_at`. A unique index on `normalized_name`
per tenant schema (schema-per-tenant already gives tenant isolation for free — no `tenant_id`
column needed, same as every other tenant-scoped table in this codebase) enforces FR-007's
case/whitespace/diacritic-insensitive dedupe at the database level, not just in application code.
`VaccineRecord` gains a second nullable FK, `CustomVaccineEntryId` → `tenant_custom_vaccine_entries(id)`
(a real, same-schema FK — no cross-schema complexity here, unlike R2).

On save, the command handler resolves the typed vaccine name in this order: (1) if the director
picked a catalog entry, set `VaccineTypeId`, leave `CustomVaccineEntryId` null; (2) otherwise,
look up (or create, via `INSERT ... ON CONFLICT (normalized_name) DO NOTHING` then re-select) a
`tenant_custom_vaccine_entries` row for the typed name and set `CustomVaccineEntryId`, leave
`VaccineTypeId` null. The two FKs are mutually exclusive by construction, mirroring this
codebase's existing "two nullable FKs, exactly one set" idiom (e.g. `DayReservation`'s
swap-request pairing).

**Rationale**: A unique index makes the dedupe guarantee robust against concurrent writes from
two directors at the same KDV (spec.md Edge Cases) — an application-layer-only "check then
insert" has a race window a DB constraint closes for free. Storing `normalized_name` as a real
column (rather than a functional/expression index) keeps the dedupe logic visible and queryable
without relying on Postgres-specific expression-index syntax scattered through application code.

**Alternatives considered**:
- *Single polymorphic "VaccineReferenceId" column with a discriminator*: rejected — this
  codebase has no existing polymorphic-FK precedent, and two plain nullable FKs is simpler and
  matches the `DayReservation` precedent already in the domain.
- *Application-layer-only dedupe (no unique index)*: rejected — leaves a real race condition for
  the concurrent-typo scenario spec.md's Edge Cases explicitly calls out.

## R4 — Attachment storage: extend `IHealthAttachmentStorage` with a category parameter, not a new port

**Decision**: `IHealthAttachmentStorage.CreateUploadUrlAsync` gains an optional `category`
parameter (default `"health-records"`, preserving every existing call site unchanged), used to
build the object path as `{category}/{subjectId}/attachment.{ext}` instead of the current
hardcoded `health-records/{subjectId}/attachment.{ext}`. `VaccineRecord`'s attachment flow calls
it with `category: "vaccine-records"`.

**Rationale**: The BACKLOG prompt explicitly calls for reusing "the existing
`IHealthAttachmentStorage`/`GcsHealthAttachmentStorage` port from 013c via a distinct object-path
prefix" — this is the minimal change that satisfies that: one new parameter with a
backward-compatible default, versus introducing a second nearly-identical interface
(`IVaccineAttachmentStorage`) that would just duplicate `GcsHealthAttachmentStorage`'s signing
logic. Same bucket (`Storage:ProfilePhotosBucketName`), same signed-URL idiom, same 15-minute
upload/download URL lifetime, same PDF/JPEG/PNG + 10MB constraint (spec.md Clarifications) — only
the path prefix differs, which is exactly what the parameter controls.

**Alternatives considered**:
- *New `IVaccineAttachmentStorage` interface, separate GCS implementation class*: rejected —
  would duplicate `GcsHealthAttachmentStorage.cs` almost verbatim for zero behavioral difference.
- *Hardcode a second fixed path inside the existing implementation via a type check on the
  caller*: rejected — a parameter is more explicit and testable than inferring category from
  caller identity.

## R5 — Vaccine-name picker: a small first-party combobox component, not a new UI-library dependency

**Decision**: Build a single, reusable `VaccineNameCombobox` web component from a plain
`<input>` + a positioned `<ul role="listbox">` filtered on keystroke, arrow-key navigation, and
`aria-activedescendant` wiring for screen readers — no new npm dependency.

**Rationale**: `web/package.json` has no combobox/autocomplete primitive today (no `cmdk`, no
`@radix-ui/react-popover`) — this is the first searchable-picker UI in this codebase. Given
Constitution Principle VII's monolith/dependency-restraint spirit and this design system's
explicit "no reinventing a component that already exists" only applies to components that
*already exist*, the right call for a first-of-its-kind need is a small first-party component,
not a new dependency for one screen. `platform-rules.md`'s director-web keyboard-navigation
requirement and `design-system.md`'s Forms styling (`surface-soft` fill, `8px` radius) apply to it
like any other input.

**Alternatives considered**:
- *Add `@radix-ui/react-popover` + build on top*: rejected for now — a new dependency for a
  single combobox is disproportionate; revisit if a second picker need arises elsewhere and the
  duplication cost outweighs the dependency cost.
- *Native `<datalist>`*: rejected — no grouping support (catalog-by-category vs "used before"),
  inconsistent cross-browser rendering, and no way to surface an explicit "add custom" affordance.

## R6 — Catalog `category` is a constrained, i18n-translated value, not raw display text

**Decision**: `VaccineType.Category` stores one of two fixed wire values,
`"basisvaccinatieschema"` or `"aanbevolen_niet_gratis"` (nullable — a future entry could have
neither), translated client-side via an i18n key
(`children.health.vaccines.catalogCategory.basisvaccinatieschema` etc.), the same
`ChildEventTypeExtensions`/`HealthRecordType` idiom feature 009/013c already established for
multi-word enum-like wire strings.

**Rationale**: Constitution Principle IV (NON-NEGOTIABLE) forbids hardcoded user-facing strings
anywhere; a raw free-text `category` column storing display text directly would violate that the
moment it's rendered. A fixed, translated set also matches spec.md FR-013's requirement that the
UI group entries by category consistently, which a free-text column can't guarantee.

**Alternatives considered**:
- *Free-text `category` column, displayed as-is*: rejected — violates Principle IV and risks
  inconsistent category spelling across seed rows.

## R7 — Seed data: `PublicDbContext` migration `InsertData`, matching this codebase's manual-rollout convention

**Decision**: The catalog seed rows are inserted via `migrationBuilder.InsertData` in the same EF
Core migration that creates the `vaccine_types` table (`AddVaccineTypeCatalog`, in
`Migrations/Public/`). Like every other migration in this codebase, it is **not** auto-applied in
production (Constitution Principle VI) — the generated SQL script is reviewed and run manually,
same as any other schema change.

**Rationale**: `InsertData` is EF Core's standard idiom for small, static reference-data seeding
and keeps the seed content in the same reviewable migration file as the schema it populates,
rather than a separate seeding service/script this codebase has no existing precedent for.

**Alternatives considered**:
- *A separate CLI seeding command (mirrors `backfill-growth-check`, feature 009a)*: rejected —
  that precedent exists for a *data migration* over pre-existing tenant rows across every tenant
  schema; this is static reference data with zero existing rows to migrate, a much simpler case
  that `InsertData` handles directly.

## R8 — Test-suite maintenance: the recurring tenant-migration-revert-helper fix applies again

**Finding, not a decision**: `TenantMigrationRolloutTests.RevertToPreExtensionSchemaAsync` and
`LegacyVaccinationMigrationTests.RevertToPreVaccineHealthRecordsAsync` both revert a tenant schema
to a fixed earlier point and must be extended for every migration added since — a pattern every
migration-adding feature since 003 has hit (012a, 013c, 013d, 006a all logged this same fix in
their shipped-notes). This feature's tenant migration (new `tenant_custom_vaccine_entries` table
+ two new nullable columns on `vaccine_records`) needs the identical treatment in both files:
drop the new table and the two new columns, and add this migration's name to the
`__EFMigrationsHistory` cleanup `WHERE` clause. Tracked as a task in tasks.md, not deferred.
