# Data Model: Vaccine & Health Records

## VaccineRecord (`vaccine_records`, tenant schema)

Replaces `VaccinationRecord`/`vaccination_records` (feature 006 — see research.md R1). One row
per vaccination event for one child.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK, `gen_random_uuid()` default |
| `child_id` | `uuid` | no | FK → `children(id)`. No cascade delete (child deactivation never removes history, same pattern as `IncidentReport`). |
| `vaccine_name` | `text` | no | Free text (e.g. "DTP", "MMR", "Hep B") — no fixed catalog (spec.md Assumptions). Max length 200. |
| `dose_number` | `int` | yes | 1, 2, 3, 4… for multi-dose vaccines. |
| `administered_on` | `date` | no | Must not be in the future (mirrors the legacy `RecordVaccinationCommandValidator`'s existing rule). |
| `next_due_date` | `date` | yes | Drives the due-soon dashboard block (FR-009/FR-010). No constraint requiring it to be after `administered_on` at the DB level — validated in the Application layer. |
| `administered_by` | `text` | yes | Doctor/clinic name, free text. Max length 200. |
| `notes` | `text` | yes | Free text. |
| `recorded_by` | `uuid` | yes | FK → `users(id)` (`TenantUser`) — the director who entered the record. Populated from the caller's JWT on every new write (director-authenticated, FR-001); nullable only so migrated legacy `vaccination_records` rows (research.md R1), which never captured an actor, have a valid representation rather than a fabricated one. |
| `created_at` | `timestamptz` | no | Default `now()`. |
| `updated_at` | `timestamptz` | no | Set on every edit. |
| `deleted_at` | `timestamptz` | yes | Soft-delete (FR-002); null = active. Same convention as `ChildEvent.DeletedAt`. |

**Indexes**: `(child_id)` for the Gezondheid tab list; `(next_due_date)` (partial, `WHERE deleted_at
IS NULL`) supporting the due-soon aggregate query (research.md R4) without a full scan.

**Validation rules** (Application layer, FluentValidation):
- `vaccine_name`: required, ≤200 chars.
- `administered_on`: required, not in the future.
- `dose_number`: if provided, ≥1.
- `administered_by`/`notes`: ≤200 / ≤2000 chars respectively (mirrors 012a's `Notes` length-limit
  precedent — an unbounded text field reaching Postgres as an unhandled oversized value is exactly
  the class of gap that feature's `/speckit-converge` caught).

## HealthRecord (`health_records`, tenant schema)

New entity — the structured counterpart to the free-text medical fields already on `Child`
(feature 006). One row per health record for one child.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK, `gen_random_uuid()` default |
| `child_id` | `uuid` | no | FK → `children(id)`. No cascade delete. |
| `record_type` | `text` (enum-backed) | no | `allergy` \| `chronic_condition` \| `medication_standing` \| `doctor_note` \| `other`. Stored as a C# enum mapped to its lowercase-with-underscore wire string (same `ChildEventTypeExtensions`-style explicit mapping feature 009 introduced, not the default `ToString().ToLowerInvariant()` convention, since `medication_standing` and `chronic_condition` are multi-word). |
| `title` | `text` | no | ≤200 chars. |
| `description` | `text` | no | ≤2000 chars. |
| `valid_from` | `date` | yes | |
| `valid_until` | `date` | yes | Past `valid_until` marks the record "expired" in the UI (FR-008) — never hidden, never deleted. |
| `attachment_object_path` | `text` | yes | GCS object path (not a URL — signed download URLs are generated fresh per read, per `IHealthAttachmentStorage`, research.md R2). Null = no attachment. |
| `recorded_by` | `uuid` | yes | FK → `users(id)`. Populated from the caller's JWT on every write (director-authenticated, FR-004); nullable for schema consistency with `VaccineRecord.recorded_by`, though every `HealthRecord` row will have it populated in practice since this is a new table with no legacy data to migrate. |
| `created_at` | `timestamptz` | no | Default `now()`. |
| `updated_at` | `timestamptz` | yes | Set on every edit; null until first edit. |
| `deleted_at` | `timestamptz` | yes | Soft-delete (FR-005); null = active. |

**Indexes**: `(child_id)` for the Gezondheid tab list and the caregiver summary query.

**Validation rules**:
- `record_type`: required, one of the five fixed values.
- `title`: required, ≤200 chars.
- `description`: required, ≤2000 chars.
- `valid_until`: if both `valid_from` and `valid_until` are set, `valid_until >= valid_from`.
- Attachment: content-type restricted to PDF/JPEG/PNG, max 10MB (spec.md FR-006), enforced at
  upload-URL issuance time (mirrors this codebase's existing photo-upload content-type handling);
  attachment upload failure must never block saving the record itself (FR-007) — the attachment is
  a separate follow-up call against `IHealthAttachmentStorage`, not part of the create/update
  transaction.

## Relationships

```
Child (1) ──── (0..n) VaccineRecord
Child (1) ──── (0..n) HealthRecord
TenantUser (1) ──── (0..n) VaccineRecord   [recorded_by]
TenantUser (1) ──── (0..n) HealthRecord    [recorded_by]
```

Neither entity registers an `IChildDeactivationGuard` — records persist after a child is
deactivated (spec.md Edge Cases; matches `IncidentReport`'s precedent of "never cascade-deleted,
no guard needed" rather than a blocking guard).

## Removed (superseded by this feature — research.md R1)

- `VaccinationRecord` entity, `vaccination_records` table.
- `RecordVaccinationCommand`, `RecordVaccinationCommandValidator`, `RecordVaccinationCommandHandler`.
- `ListChildVaccinationsQuery`, `ListChildVaccinationsQueryHandler`.
- `VaccinationResponse`, `VaccinationResult`, `GroupMapper.ToVaccinationResponse`.
- `GET/POST /api/children/{childId}/vaccinations` endpoints (`GroupsEndpoints.cs`).
- `ChildVaccinationTests.cs`.

A migration copies any existing `vaccination_records` rows into `vaccine_records` before dropping
the old table (mapping `child_id`/`vaccine_name`/`date_administered`→`administered_on`/
`next_due_date`; `recorded_by` is left `null` for migrated rows, since the legacy table never
captured an actor and the column is nullable precisely to allow this).
