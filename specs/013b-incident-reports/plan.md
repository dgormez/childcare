# Implementation Plan: Incident Reports

**Branch**: `013b-incident-reports` | **Date**: 2026-07-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013b-incident-reports/spec.md`

## Summary

Add a new `incident_reports` tenant table and full CRUD-minus-delete lifecycle: caregivers file a
report from the child's tablet profile (required description + injury type, optional first-aid/
doctor/parent-notification detail), `reported_by` resolved server-side via the existing
`IShiftAttributionService` (feature 009) rather than a client-submitted value or PIN confirmation
(per this spec's Clarifications session — mirrors `ChildEvent.RecordedBy` exactly). Reports lock
after 24 hours (everything except `follow_up`), enforced in the command handler, never
client-trusted. A new director-only web screen (`/incidents`) lists every report across locations,
filterable by date range/location/child, tracks a sticky `reviewed` flag as the in-app substitute
for a director push channel that doesn't exist (spec Assumptions), and offers a single-report PDF
export mirroring feature 007's `IContractPdfGenerator` pattern. Offline filing reuses feature 008's
`offline_queue`/sync engine (`entity_type = "incident_report"`), following the exact
`registerSyncHandler` convention `childEvents.ts` already established.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web: Next.js 15 App Router; mobile:
Expo/React Native)

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9,
QuestPDF (backend); Next.js 15 + Tailwind + shadcn/ui (web); Expo Router + NativeWind (mobile);
openapi-typescript + openapi-fetch (both frontends)

**Storage**: PostgreSQL 16, schema-per-tenant — one new table, `incident_reports`

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, constitution Principle V);
Vitest + `@testing-library/react` (web, jsdom, feature 007a precedent); `@testing-library/
react-native` (mobile, feature 008/009 precedent)

**Target Platform**: Cloud Run (backend API); web browser ≥1280px (director); iOS/Android tablet
via Expo (caregiver)

**Project Type**: Web application (backend + two frontends) — existing monorepo structure

**Performance Goals**: Standard interactive-app expectations; the cross-KDV inspection view must
stay responsive against years of accumulated reports (FR-017) via indexing, not special caching

**Constraints**: 24-hour immutability enforced server-side only, regardless of client input
(spec FR-005); offline filing must never block on a network round-trip (rules out any PIN/
confirmation step for `reported_by`, per Clarifications); never cascade-delete a report when its
child is deactivated (FR-008)

**Scale/Scope**: One new tenant table, one new domain entity, ~6 new endpoints (create, get, list
with filters, mark-reviewed-on-open, update within-window, PDF export), one new mobile form +
sync-handler registration, one new web screen (list + detail + filters + PDF button) replacing a
sidebar placeholder

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `incident_reports` is a new tenant-schema table registered on `TenantDbContext`, scoped through the existing `ICurrentTenantService`/`TenantMiddleware` pipeline like every entity since 002. No cross-tenant read path. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/overlap/closure rule, but this feature *is itself* a Besluit Kwaliteit Kinderopvang legal-record requirement — the 24-hour immutability lock (FR-005) is enforced in `UpdateIncidentReportCommandHandler`, never client-only, matching the principle's spirit for a compliance-critical constraint. |
| III. CQRS via MediatR & Thin Endpoints | PASS | `FileIncidentReportCommand`, `UpdateIncidentReportCommand`, `MarkIncidentReportReviewedCommand` are MediatR commands with FluentValidation; `ListIncidentReportsQuery`/`GetIncidentReportQuery`/`GenerateIncidentReportPdfQuery` are MediatR queries. `IncidentReportEndpoints.cs` only maps HTTP ↔ MediatR. |
| IV. Internationalization First | PASS | All new web/mobile strings via i18n keys (next-intl / react-i18next); new API error responses return locale-aware `errorKey` values (e.g. `errors.incident_reports.locked`, `errors.incident_reports.missing_injury_type`). |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL, per every feature since 003. |
| VI. Secure Configuration & Storage | PASS | No secrets, no new file-storage integration (PDF bytes streamed directly, same as `IContractPdfGenerator` — no GCS upload). Reviewed EF Core migration adds one new table. |
| VII. Monolith-First Simplicity | PASS | No new project, no new service. Extends the existing 5-project backend solution + existing `web`/`mobile` apps. |

No violations. Complexity Tracking section not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013b-incident-reports/
├── plan.md                              # This file
├── research.md                          # Phase 0 output
├── data-model.md                        # Phase 1 output
├── quickstart.md                        # Phase 1 output
├── contracts/
│   └── incident-reports-api.md          # Phase 1 output
└── tasks.md                             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/IncidentReport.cs                      # new
│   └── Enums/IncidentInjuryType.cs                     # new
│   └── Enums/ParentNotifiedHow.cs                      # new
├── ChildCare.Application/
│   └── IncidentReports/
│       ├── FileIncidentReportCommand.cs                 # new
│       ├── FileIncidentReportCommandHandler.cs          # new (calls IShiftAttributionService)
│       ├── FileIncidentReportCommandValidator.cs        # new
│       ├── UpdateIncidentReportCommand.cs               # new (24h-lock enforcement)
│       ├── UpdateIncidentReportCommandHandler.cs        # new
│       ├── UpdateIncidentReportCommandValidator.cs      # new
│       ├── MarkIncidentReportReviewedCommand.cs         # new
│       ├── MarkIncidentReportReviewedCommandHandler.cs  # new
│       ├── GetIncidentReportQuery.cs                    # new
│       ├── ListIncidentReportsQuery.cs                  # new (date range/location/child filters)
│       ├── GenerateIncidentReportPdfQuery.cs             # new
│       ├── IncidentReportResult.cs                       # new
│       └── IncidentReportMapper.cs                       # new
├── ChildCare.Contracts/
│   ├── Requests/IncidentReportRequests.cs                # new
│   └── Responses/IncidentReportResponse.cs               # new
├── ChildCare.Api/Endpoints/
│   └── IncidentReportEndpoints.cs                        # new
├── ChildCare.Infrastructure/
│   ├── Persistence/TenantDbContext.cs                    # + IncidentReport DbSet/config
│   ├── Persistence/Migrations/Tenant/<timestamp>_AddIncidentReports.cs   # new
│   └── Pdf/QuestPdfIncidentReportGenerator.cs             # new (mirrors QuestPdfContractGenerator)
└── ChildCare.Api.Tests/
    └── IncidentReports/
        ├── FileIncidentReportTests.cs                     # new
        ├── IncidentReportImmutabilityTests.cs             # new (24h lock)
        ├── ListIncidentReportsFilterTests.cs               # new
        ├── IncidentReportChildDeactivationTests.cs         # new (FR-008)
        └── GenerateIncidentReportPdfTests.cs               # new

mobile/
├── app/(app)/child/[id].tsx                # + "Incident melden" entry point
├── components/IncidentReportForm.tsx       # new (chips for injury type, per design-system)
├── services/incidentReports.ts             # new — registerSyncHandler("incident_report", {...})
└── services/generated/api-types.ts         # regenerated, committed per 008/009 precedent

web/
├── app/(app)/incidents/
│   ├── page.tsx                            # new — list + filters (replaces sidebar placeholder)
│   └── [id]/page.tsx                       # new — detail view, marks reviewed on open, PDF button
├── components/
│   ├── IncidentReportsTable.tsx             # new (mirrors StaffTable/DayReservationsTable pattern)
│   └── IncidentReportFilters.tsx            # new (date range, location, child)
├── components/Sidebar.tsx                  # "incidents" added to REAL_NAV
└── lib/generated/api-types.ts              # regenerated, committed per 007a precedent
```

**Structure Decision**: Follows the existing three-surface monorepo layout exactly — no new
projects. `IncidentReport` lives under its own `ChildCare.Application/IncidentReports/` folder
(not `ChildEvents/`) since it's a distinct entity/table with its own lifecycle (immutability lock,
reviewed state, PDF export) rather than an event-payload variant of `child_events`. Web gets a new
top-level `/incidents` route (not nested under the still-placeholder `/children`) per this
feature's Assumptions — the child-file screen doesn't exist yet, so incident history is reached via
this dedicated screen's child filter instead, consistent with `reference-products.md`'s "avoid
hidden actions, prefer a real route" guidance already applied in 007a/013f.

## Complexity Tracking

*No violations — table not needed.*
