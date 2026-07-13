# Implementation Plan: Vaccine Catalog & Attachments

**Branch**: `013g-vaccine-catalog` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013g-vaccine-catalog/spec.md`

## Summary

Add a shared, platform-wide vaccine catalog (seeded reference data, read-only from every
tenant-facing endpoint) backing 013c's free-text `VaccineRecord.VaccineName`, a per-tenant
"remembered custom entry" mechanism so a director never retypes the same non-catalog vaccine name
twice, and attachment support on `VaccineRecord` itself (photo/scan of the paper
vaccinatieboekje), reusing 013c's existing signed-URL health-attachment infrastructure under a
distinct object-path prefix. Director web's `VaccineRecordForm` gains a combobox (catalog grouped
by category + tenant custom entries + free-text fallback) and an attachment control mirroring
`HealthRecordAttachmentControl`. No caregiver-facing or platform-admin-UI changes ship in this
feature (spec.md Assumptions).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (Next.js 15 web admin)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Next.js App Router, Tailwind, shadcn/ui, openapi-fetch (web) — no new npm dependency
(research.md R5).

**Storage**: PostgreSQL 16 — one new `public`-schema table (`vaccine_types`, research.md R1), one
new tenant-schema table (`tenant_custom_vaccine_entries`), two new nullable columns +
one attachment-path column on the existing tenant-schema `vaccine_records` table. GCP Cloud
Storage (signed URLs), same bucket as 013c's health-record attachment, new object-path prefix.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V); Vitest + `@testing-library/react` (web, for the new combobox component).

**Target Platform**: Cloud Run (backend API); web browsers ≥1280px (director only — this feature
has zero caregiver-tablet or parent-mobile surface).

**Project Type**: Web application — ASP.NET Core API + Next.js web admin (existing monorepo
structure, no new projects, no mobile changes).

**Performance Goals**: Catalog + custom-entries reads are small, bounded, uncached-but-cheap
lookups (tens of rows) — no specific SLA beyond this codebase's existing director-web list-query
norm (no N+1 across the combobox's two source lists).

**Constraints**: No DB-level cross-schema FK (research.md R2); attachment content-type/size
limit identical to 013c's `HealthRecord` (PDF/JPEG/PNG, 10MB — spec.md Clarifications); canonical
catalog has zero director-facing write path in this feature (spec.md Assumptions).

**Scale/Scope**: ~9 seeded catalog rows; low tens of custom entries per tenant at most (one per
distinct non-catalog vaccine name ever typed) — smaller in scope than any other per-child record
type already shipped.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS (with a named new pattern) | `tenant_custom_vaccine_entries` lives in `TenantDbContext`, tenant-schema-scoped exactly like every other domain table — dedupe via a per-schema unique index, no `tenant_id` column needed, no cross-tenant read path. `vaccine_types` is the first `PublicDbContext` table that is shared *reference* data rather than a tenant-management record (research.md R1) — this is intentional and matches the principle's actual purpose (preventing cross-*tenant-domain-data* leakage): the catalog carries no tenant-specific or personal data at all, so there is nothing to leak between tenants by construction. `VaccineRecord.VaccineTypeId` deliberately has no DB-level FK across the schema boundary (research.md R2) — an application-layer-validated reference to non-sensitive, soft-delete-only reference data, not a tenant-isolation gap. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/contract-overlap/closure-calendar feature — no regulatory ratio logic applies. This feature improves data quality feeding 013c's existing due-soon compliance reminder; it does not change that reminder's own enforcement logic. |
| III. CQRS via MediatR & Thin Endpoints | PASS | Catalog/custom-entries reads go through MediatR queries (`ListVaccineTypesQuery`, `ListTenantCustomVaccineEntriesQuery`); the vaccine-record create/update handlers gain the catalog/custom-entry resolution logic (research.md R3) inside the existing MediatR command handlers, not in endpoint code. |
| IV. Internationalization First | PASS | `VaccineType.Category`'s two wire values are translated client-side via i18n keys (research.md R6), not stored/rendered as raw display text. All new combobox/attachment-control UI strings ship as NL/FR/EN locale keys from the start, matching `HealthRecordAttachmentControl`'s existing precedent. |
| V. Test with Real Infrastructure | PASS | New integration tests run against TestContainers PostgreSQL: catalog seed content, custom-entry creation + reuse + case-insensitive dedupe (including a concurrent-write race test exercising the unique index, research.md R3), retired-catalog-entry rendering, attachment upload failure not blocking record save. |
| VI. Secure Configuration & Storage | PASS | Attachment reuses the existing signed-URL-only `IHealthAttachmentStorage` port (research.md R4) — no public blob URLs, no new bucket/Terraform change. The `vaccine_types` seed migration is a normal reviewed EF Core migration, not auto-applied to production (constitution's standing migration rule, unaffected by this feature). |
| VII. Monolith-First Simplicity | PASS | No new deployable/service, no new npm dependency for the combobox (research.md R5) — new MediatR handlers/endpoints inside the existing five-project backend solution and existing web app. |

No unjustified violations. The one item worth flagging explicitly (`vaccine_types` as the first
non-tenant-management row in `PublicDbContext`) is addressed above as a deliberate, in-scope
design choice rather than a gate failure — see research.md R1 for the full reasoning.

## Project Structure

### Documentation (this feature)

```text
specs/013g-vaccine-catalog/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/
│   ├── VaccineType.cs                        # NEW — public-schema reference entity
│   ├── TenantCustomVaccineEntry.cs           # NEW — tenant-schema
│   └── VaccineRecord.cs                      # EXTENDED — VaccineTypeId, CustomVaccineEntryId, AttachmentObjectPath
├── ChildCare.Application/
│   ├── Common/
│   │   └── IHealthAttachmentStorage.cs       # EXTENDED — optional `category` parameter
│   ├── VaccineTypes/                          # NEW — mirrors existing per-aggregate folder convention
│   │   ├── ListVaccineTypesQuery.cs
│   │   └── VaccineTypeMapper.cs
│   ├── VaccineCustomEntries/                  # NEW
│   │   └── ListTenantCustomVaccineEntriesQuery.cs
│   └── VaccineRecords/                        # EXTENDED
│       ├── CreateVaccineRecordCommand.cs      # + catalog/custom-entry resolution (research.md R3)
│       ├── UpdateVaccineRecordCommand.cs      # + same resolution
│       ├── CreateVaccineRecordAttachmentUploadUrlCommand.cs  # NEW
│       └── VaccineRecordMapper.cs             # + attachmentDownloadUrl, vaccineTypeId
├── ChildCare.Infrastructure/
│   ├── Storage/GcsHealthAttachmentStorage.cs  # EXTENDED — category parameter
│   └── Persistence/
│       ├── PublicDbContext.cs                 # EXTENDED — DbSet<VaccineType>
│       ├── TenantDbContext.cs                 # EXTENDED — DbSet<TenantCustomVaccineEntry>
│       └── Migrations/
│           ├── Public/…AddVaccineTypeCatalog.cs      # NEW — table + InsertData seed (research.md R7)
│           └── Tenant/…AddVaccineCatalogAndAttachments.cs  # NEW
├── ChildCare.Contracts/
│   ├── Responses/VaccineTypeResponse.cs       # NEW
│   ├── Responses/CustomVaccineEntryResponse.cs # NEW
│   ├── Responses/VaccineRecordResponse.cs      # EXTENDED
│   └── Requests/VaccineRecordRequests.cs       # EXTENDED — vaccineTypeId
├── ChildCare.Api/Endpoints/
│   ├── VaccineTypeEndpoints.cs                 # NEW
│   └── VaccineRecordEndpoints.cs               # EXTENDED — attachment-upload-url route
└── ChildCare.Api.Tests/
    ├── VaccineTypes/…                          # NEW
    ├── VaccineRecords/…                        # EXTENDED
    ├── TenantMigrationRolloutTests.cs           # EXTENDED — revert helper (research.md R8)
    └── VaccineRecords/LegacyVaccinationMigrationTests.cs  # EXTENDED — same reason

web/
├── components/health/
│   ├── VaccineNameCombobox.tsx                 # NEW (research.md R5)
│   ├── VaccineRecordForm.tsx                   # EXTENDED — combobox + attachment control
│   └── HealthRecordAttachmentControl.tsx       # kept generic name, reused by VaccineRecordForm too
├── app/(app)/children/[id]/page.tsx            # EXTENDED — vaccine catalog/custom-entries fetch, upload handler
└── lib/generated/api-types.ts                  # regenerated
```

**Structure Decision**: Existing monorepo layout (`backend/`, `web/`, `mobile/`) — no new
top-level projects, no mobile changes (this feature is director-web only). Backend follows the
existing flat-folder-per-aggregate MediatR convention (`VaccineTypes/`, `VaccineCustomEntries/`
as new folders; `VaccineRecords/` extended in place). `HealthRecordAttachmentControl.tsx` is
reused as-is by `VaccineRecordForm` (same `attachmentDownloadUrl`/`onUpload` prop shape already
generic enough — confirmed during implementation, no rename needed) rather than duplicated,
per design-system.md's "shared components reused rather than reimplemented."

## Complexity Tracking

No unjustified Constitution Check violations — this section is not needed. See the Constitution
Check table above for the one deliberate new pattern (`vaccine_types` in `PublicDbContext`),
addressed there with its rationale rather than listed as a violation requiring justification.
