# Data Model: Vaccine Catalog & Attachments

## VaccineType (`vaccine_types`, **`public` schema** — research.md R1)

Shared, platform-wide reference data. Identical across every tenant. Never tenant-scoped.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK, `gen_random_uuid()` default |
| `name` | `text` | no | e.g. "DTPa-IPV-Hib-HepB", "MMR". Max length 200. Denormalised into `VaccineRecord.VaccineName` at selection time (spec.md Edge Cases — a later rename never changes past records). |
| `category` | `text` | yes | One of `basisvaccinatieschema` \| `aanbevolen_niet_gratis` (research.md R6 — fixed, i18n-translated wire values, not raw display text). Null = uncategorized. |
| `sort_order` | `int` | no | Display order within a category. |
| `is_active` | `bool` | no | Default `true`. Soft-delete only — never hard-deleted (spec.md FR-002/FR-010); a `VaccineRecord` referencing a deactivated entry still renders via its own denormalised `VaccineName`. |
| `created_at` | `timestamptz` | no | Default `now()`. |
| `updated_at` | `timestamptz` | no | Set on every edit (by the platform operator, outside any director-facing UI — spec.md Assumptions). |

**Indexes**: `(category, sort_order)` for the grouped-picker read; `(is_active)` partial for the
active-only default query.

**Seed data** (migration `AddVaccineTypeCatalog`, `Migrations/Public/` — research.md R7):

| Name | Category |
|---|---|
| DTPa-IPV-Hib-HepB | `basisvaccinatieschema` |
| Pneumokokken (PCV) | `basisvaccinatieschema` |
| BMR (bof, mazelen, rodehond) | `basisvaccinatieschema` |
| MenACWY | `basisvaccinatieschema` |
| HPV | `basisvaccinatieschema` |
| RSV (zuigelingen) | `aanbevolen_niet_gratis` |
| MenB | `aanbevolen_niet_gratis` |
| Hepatitis A | `aanbevolen_niet_gratis` |
| Waterpokken (varicella) | `aanbevolen_niet_gratis` |

## TenantCustomVaccineEntry (`tenant_custom_vaccine_entries`, tenant schema)

New table. One row per distinct (case/whitespace/diacritic-insensitive) vaccine name a director
at this KDV has ever typed that didn't match the catalog (research.md R3).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK, `gen_random_uuid()` default |
| `name` | `text` | no | As originally typed (display form). Max length 200. |
| `normalized_name` | `text` | no | Case-folded, whitespace-trimmed, diacritic-stripped (`RemoveDiacritics(lower(trim(name)))`, research.md R3) — computed at write time in the command handler, not a DB generated column, to keep the normalization rule visible in application code. |
| `created_at` | `timestamptz` | no | Default `now()`. |

**Indexes**: unique index on `normalized_name` (enforces per-tenant case/whitespace-insensitive
dedupe, research.md R3 — schema-per-tenant already isolates this across KDVs with no `tenant_id`
column needed, same as every other tenant table in this codebase).

**Validation rules**: `name` required, ≤200 chars (mirrors `VaccineRecord.VaccineName`'s existing
limit).

## VaccineRecord (`vaccine_records`, tenant schema) — extended

Adds three columns to the existing 013c entity. All other columns unchanged.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `vaccine_type_id` | `uuid` | yes | References `public.vaccine_types(id)` — **no DB-level FK constraint** (research.md R2: soft-delete-only catalog makes a real cross-schema FK unnecessary complexity). Mutually exclusive with `custom_vaccine_entry_id`. |
| `custom_vaccine_entry_id` | `uuid` | yes | Real, same-schema FK → `tenant_custom_vaccine_entries(id)`. No cascade delete. Mutually exclusive with `vaccine_type_id`. |
| `attachment_object_path` | `text` | yes | GCS object path under the `vaccine-records/` prefix (research.md R4). Null = no attachment. Same signed-URL-on-read pattern as `HealthRecord.AttachmentObjectPath`. |

**Indexes**: `(vaccine_type_id)` for catalog-driven lookups (e.g. a future due-soon-by-vaccine-type
aggregation).

**Validation rules** (Application layer, FluentValidation, in addition to 013c's existing rules):
- At most one of `vaccine_type_id` / `custom_vaccine_entry_id` may be set (never both) — the
  command handler's resolution order (research.md R3) guarantees this by construction (never
  user-supplied directly), and a `CHECK ("vaccine_type_id" IS NULL OR "custom_vaccine_entry_id"
  IS NULL)` constraint enforces it at the database level too (spec.md FR-004 states this as a
  MUST, not merely an application convention).
- Attachment: content-type restricted to `application/pdf`/`image/jpeg`/`image/png`, max 10MB
  (spec.md Clarifications), enforced at upload-URL issuance time — identical constraint to
  `HealthRecord`'s attachment (013c), reusing the same enforcement code path via
  `IHealthAttachmentStorage`'s existing content-type switch.
- Attachment upload failure never blocks or rolls back the vaccine record's save (spec.md FR-012)
  — same two-step flow as `HealthRecord` (create/update the record; attach via a separate,
  subsequent call).

## Relationships

```
VaccineType (public schema, 1) ──── (0..n) VaccineRecord   [vaccine_type_id, no DB FK]
TenantCustomVaccineEntry (1) ──── (0..n) VaccineRecord      [custom_vaccine_entry_id]
Child (1) ──── (0..n) VaccineRecord                          [unchanged, 013c]
TenantUser (1) ──── (0..n) VaccineRecord                     [recorded_by, unchanged, 013c]
```

`TenantCustomVaccineEntry` registers no `IChildDeactivationGuard` — it has no relationship to
`Child` at all; it is pure per-tenant picker memory, never deleted once created (matches this
codebase's "never delete accumulated reference/history data" precedent, e.g. `IncidentReport`,
013c's `VaccineType`/`HealthRecord`).
