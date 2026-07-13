# Implementation Plan: Vaccine & Health Records

**Branch**: `013c-vaccine-health-records` | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013c-vaccine-health-records/spec.md`

## Summary

Add structured vaccination history and detailed health records per child, replacing the
unwired/legacy `vaccination_records` table from feature 006 with a richer schema (dose number,
administering clinic, notes, recorded-by, soft-delete). Director web gains a "Gezondheid" tab on
a new minimal child-detail screen (director web currently has no per-child detail page) with
vaccine/health-record CRUD and a director dashboard "Vaccinations due soon" block. The caregiver
app extends its existing feature-008 medical quick-access sheet with the same data, read-only,
cached for offline use the same way today's allergy summary already is.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (Next.js 15 web admin, Expo/React
Native caregiver app)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Next.js App Router, Tailwind, shadcn/ui, openapi-fetch (web); Expo Router, NativeWind,
expo-sqlite, openapi-fetch (mobile)

**Storage**: PostgreSQL 16, schema-per-tenant (`vaccine_records`, `health_records` tables); GCP
Cloud Storage (signed URLs) for the optional health-record attachment.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V); Jest + `@testing-library/react-native` (mobile); Vitest + `@testing-library/react` (web).

**Target Platform**: Cloud Run (backend API); web browsers ≥1280px (director); iPad-class tablet,
landscape (caregiver).

**Project Type**: Web application — ASP.NET Core API + Next.js web admin + Expo caregiver app
(existing monorepo structure, no new projects).

**Performance Goals**: Due-soon dashboard aggregation query returns in line with this codebase's
other director-web list queries (no specific SLA set elsewhere in this repo) — the requirement
that matters is avoiding N+1 (FR-020 below), not a specific latency number.

**Constraints**: Offline-capable caregiver read access (cache-on-load, per feature 008's existing
pattern); GDPR — medical data excluded from any bulk export/email summary by default (FR-016);
attachment access only via signed, time-limited URLs (constitution Principle VI).

**Scale/Scope**: Tens of vaccine/health records per child, tens to low hundreds of children per
tenant — no different in scale from any other per-child record type already shipped (feature 009
child_events, 013b incident_reports).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | Both new tables live in `TenantDbContext`, tenant-schema-scoped exactly like every other domain table. No cross-tenant read path introduced. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/contract-overlap/closure-calendar feature — no regulatory ratio logic applies. The Belgian legal requirement this feature *serves* (Vaccinatieboekje tracking) is enforced in the Application layer via the due-soon query, not client-side-only. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (create/update/delete vaccine record, create/update/delete health record) go through MediatR commands; the due-soon aggregate and list/get reads go through MediatR queries; endpoint files only map HTTP↔MediatR. |
| IV. Internationalization First | PASS | All new user-facing strings (web Gezondheid tab, dashboard block, caregiver summary sheet, validation/error messages) ship as NL/FR/EN locale keys from the start. |
| V. Test with Real Infrastructure | PASS | New integration tests run against TestContainers PostgreSQL, covering happy path + key negative/regulatory flows (GDPR export-exclusion, caregiver read-only enforcement, location-eligibility scoping) per constitution Principle V's stated coverage bar. |
| VI. Secure Configuration & Storage | PASS | Health-record attachment uses a new signed-URL storage port (see research.md) — no public blob URLs, no hardcoded secrets. Migration removing/superseding `vaccination_records` is authored as a normal reviewed EF Core migration, not auto-applied to existing tenant schemas (constitution's own migration-application rule, unaffected by this feature). |
| VII. Monolith-First Simplicity | PASS | No new deployable/service — new MediatR handlers and endpoints inside the existing `ChildCare.Api`/`Application`/`Domain`/`Infrastructure`/`Contracts` five-project solution. |

No violations. Complexity Tracking section not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013c-vaccine-health-records/
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
│   ├── VaccineRecord.cs                     # NEW — replaces VaccinationRecord.cs (removed)
│   └── HealthRecord.cs                      # NEW
├── ChildCare.Application/
│   ├── Common/
│   │   └── IHealthAttachmentStorage.cs       # NEW — signed-URL port for health-record attachments
│   ├── VaccineRecords/                       # NEW — mirrors IncidentReports/ folder convention
│   │   ├── CreateVaccineRecordCommand.cs
│   │   ├── UpdateVaccineRecordCommand.cs
│   │   ├── DeleteVaccineRecordCommand.cs
│   │   ├── ListChildVaccineRecordsQuery.cs
│   │   ├── ListVaccinationsDueSoonQuery.cs   # director dashboard aggregate
│   │   ├── VaccineRecordMapper.cs
│   │   └── VaccineRecordResult.cs
│   ├── HealthRecords/                        # NEW
│   │   ├── CreateHealthRecordCommand.cs
│   │   ├── UpdateHealthRecordCommand.cs
│   │   ├── DeleteHealthRecordCommand.cs
│   │   ├── ListChildHealthRecordsQuery.cs
│   │   ├── HealthRecordMapper.cs
│   │   └── HealthRecordResult.cs
│   └── Children/
│       └── GetChildHealthSummaryQuery.cs     # NEW — caregiver read-only combined summary
│   (Groups/RecordVaccinationCommand.cs, ListChildVaccinationsQuery.cs — REMOVED)
├── ChildCare.Infrastructure/
│   ├── Storage/HealthAttachmentStorage.cs    # NEW
│   └── Persistence/Migrations/Tenant/…       # NEW migration: drop vaccination_records,
│                                              #   create vaccine_records + health_records
├── ChildCare.Contracts/
│   ├── Requests/VaccineRecordRequests.cs     # NEW
│   ├── Requests/HealthRecordRequests.cs      # NEW
│   ├── Responses/VaccineRecordResponse.cs    # NEW
│   └── Responses/HealthRecordResponse.cs     # NEW
├── ChildCare.Api/Endpoints/
│   ├── VaccineRecordEndpoints.cs             # NEW
│   └── HealthRecordEndpoints.cs              # NEW
│   (GroupsEndpoints.cs — vaccination routes REMOVED)
└── ChildCare.Api.Tests/
    ├── VaccineRecords/…                      # NEW (replaces ChildVaccinationTests.cs)
    └── HealthRecords/…                       # NEW

web/
├── app/(app)/children/[id]/                  # NEW — minimal child-detail screen (didn't exist)
│   └── health/page.tsx                       # "Gezondheid" tab content
├── app/(app)/dashboard/                      # due-soon block added to existing dashboard/home
├── components/health/                        # VaccineRecordForm, HealthRecordForm, DueSoonBlock
└── lib/generated/api-types.ts                # regenerated

mobile/
├── app/(app)/child/[id].tsx                  # extended: health/allergy summary sheet
├── components/health/HealthSummarySheet.tsx  # NEW — extends feature 008's quick-access sheet
├── services/offline/entityHandlers/          # no new offline WRITE handler needed (read-only)
└── services/generated/api-types.ts           # regenerated
```

**Structure Decision**: Existing monorepo layout (`backend/`, `web/`, `mobile/`) — no new
top-level projects. Backend follows the flat-folder-per-feature MediatR convention established by
`IncidentReports/` (013b): one folder per aggregate (`VaccineRecords/`, `HealthRecords/`), command/
query + validator + handler co-located in one file each, a static `Mapper`, and a `Result`
wrapper. The director-web child-detail screen is new (per spec.md's Assumptions: this feature
builds the minimal screen needed to host the Gezondheid tab, not a full child-file screen).

## Complexity Tracking

No Constitution Check violations — this section is not needed.
