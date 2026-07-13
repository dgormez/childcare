# Implementation Plan: Child Profile UI

**Branch**: `006a-child-profile-ui` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006a-child-profile-ui/spec.md`

## Summary

Feature 006 shipped a working `Child` domain model, create/update commands, and `DirectorOnly`
endpoints, but no screen anywhere (web or mobile) lets a director create or edit a child record
— research confirmed every field except a pediatrician contact already exists end-to-end on the
backend, unreachable through any UI. This feature (a) adds `PediatricianName`/`PediatricianPhone`
to the existing `Child` entity/commands/contracts/migration, (b) builds the first tabbed
structure on the director-web child-detail screen (`web/app/(app)/children/[id]/page.tsx`) with
a new "Profiel" tab alongside the existing 013c "Gezondheid" tab, (c) adds a "New child" modal
create flow on `/children` (mirroring `InviteParentDialog`'s existing modal pattern — no
react-hook-form, no new route), and (d) extends the caregiver mobile child screen's existing
read-only summary to show both GP and pediatrician contact, sourced from the same cached
`ChildResponse` the screen already reads — no new mobile offline-cache mechanism needed.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web, Next.js 15 App Router; mobile,
Expo/React Native).

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9 (backend); Radix UI + shadcn/ui,
`next-intl`, openapi-fetch generated client (web); `expo-router`, `react-i18next`,
`expo-localization` (mobile). One new dependency: `@radix-ui/react-tabs` (shadcn `Tabs`
primitive, not yet present in `web/components/ui/`).

**Storage**: PostgreSQL 16, schema-per-tenant — one additive, nullable-column migration on the
tenant `children` table.

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, per Constitution V);
Jest + React Testing Library (web, per feature 007a's precedent); Jest +
`@testing-library/react-native` (mobile).

**Target Platform**: Director web (desktop, 1280px+); caregiver tablet (landscape, read-only
extension); backend (ASP.NET Core Minimal API, Cloud Run).

**Project Type**: Web application (backend API + Next.js web + Expo mobile, existing monorepo
structure — no new project).

**Performance Goals**: None beyond existing single-record CRUD patterns — no new list/bulk
operation.

**Constraints**: EF Core migrations MUST NOT auto-apply in production (CLAUDE.md); all
user-facing strings MUST use i18n keys (NL/FR/EN, Constitution IV); create/edit MUST remain
`DirectorOnly`, read MUST remain `DeviceOrStaffOrDirector` (Constitution I/III via existing
policy reuse).

**Scale/Scope**: Single-tenant CRUD screen additions — 2 new backend fields, 1 migration, ~3 web
components (Tabs primitive, ProfileTab, CreateChildDialog), 1 mobile screen extension.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | Pass | No new endpoint, no new tenant-data read path — reuses `ChildrenEndpoints.cs`'s existing `DirectorOnly`/`DeviceOrStaffOrDirector` groups, both already behind `TenantMiddleware`. |
| II. Regulatory Compliance by Design | N/A | This feature touches no BKR ratio, contract-overlap, or closure-notification logic. |
| III. CQRS via MediatR & Thin Endpoints | Pass | Extends existing `CreateChildCommand`/`UpdateChildCommand` handlers; no new command/query type introduced; no business logic added to `ChildrenEndpoints.cs` (research.md R1). |
| IV. Internationalization First | Pass | All new UI strings (Profiel tab labels, pediatrician labels, create-form labels, validation messages) ship as NL/FR/EN keys (spec FR-008; research.md confirms the existing per-page-namespace convention to follow in both `web/i18n/locales/` and `mobile/i18n/locales/`). |
| V. Test with Real Infrastructure | Pass | Backend tests for the extended create/update handlers run against TestContainers PostgreSQL, per existing test-project setup — no InMemory provider introduced. |
| VI. Secure Configuration & Storage | Pass | Migration is authored as a normal EF Core migration file, SQL script generated and run manually (research.md R7) — no auto-apply. No new secret/storage surface. |
| VII. Monolith-First Simplicity | Pass | No new project, no new service — extends the existing five-project backend and the existing `web`/`mobile` apps. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/006a-child-profile-ui/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── child-profile-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/Child.cs                              # + PediatricianName/Phone
├── ChildCare.Application/Children/
│   ├── CreateChildCommand.cs                                       # + 2 params
│   ├── CreateChildCommandValidator.cs                              # + 2 rules
│   ├── UpdateChildCommand.cs                                       # + 2 params
│   ├── UpdateChildCommandValidator.cs                              # + 2 rules
│   └── (CreateChildCommandHandler / UpdateChildCommandHandler — extend field mapping)
├── ChildCare.Contracts/
│   ├── Requests/ChildRequests.cs                                   # + 2 fields (Create/Update)
│   └── Responses/ChildResponse.cs                                  # + 2 fields
├── ChildCare.Api/Endpoints/ChildrenEndpoints.cs                    # + 2 fields wired through
├── ChildCare.Infrastructure/Persistence/
│   ├── TenantDbContext.cs                                          # + 2 property mappings
│   └── Migrations/Tenant/<timestamp>_AddPediatricianContactToChild.cs
└── ChildCare.Tests/ (or equivalent test project)
    └── Children/ — extend Create/UpdateChildCommandHandler tests (happy path + validation)

web/
├── components/ui/tabs.tsx                                          # NEW — shadcn Tabs primitive
├── components/children/
│   ├── ChildProfileTab.tsx                                         # NEW — Profiel tab content
│   └── ChildFormDialog.tsx                                         # NEW — shared create/edit form
├── app/(app)/children/
│   ├── page.tsx                                                    # + "New child" action, opens ChildFormDialog
│   └── [id]/page.tsx                                                # restructured into Tabs (Profiel | Gezondheid), existing health content moved under Gezondheid tab, unchanged behavior
├── i18n/locales/{en,fr,nl}.json                                    # + children.profile.* / children.pediatrician* keys
├── lib/generated/api-types.ts                                      # regenerated (mechanical)
└── __tests__/ (or co-located) — ChildFormDialog, ChildProfileTab, tab navigation

mobile/
├── app/(app)/child/[id].tsx                                        # + GP/pediatrician summary block
├── i18n/locales/{en,fr,nl}.json                                    # + child.gpName / child.pediatrician* keys
└── __tests__/ — extended summary rendering, including cached-fallback case (research.md R4)
```

**Structure Decision**: No new top-level project or app. Backend changes extend the existing
feature-006 vertical slice in place (Domain → Application → Contracts → Api →
Infrastructure/Migrations). Web adds one new shared UI primitive (`tabs.tsx`) and two new
feature components under a new `web/components/children/` directory (mirroring the existing
`web/components/health/` convention from 013c), restructuring `[id]/page.tsx` without changing
its existing 013c behavior. Mobile extends one existing screen in place — no new mobile route or
service.
