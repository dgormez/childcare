# Implementation Plan: Meal List (Maaltijdenlijst)

**Branch**: `013d-meal-list` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013d-meal-list/spec.md`

## Summary

Build a daily, per-location meal list that aggregates existing data — no new business workflow,
a new read model over three existing ones. A new `MealPreference` entity (one row per child:
texture, dietary tags, portion size, notes) is the only new persisted data; everything else
(attendance presence, contract-derived "expected" children, group membership, allergy severity,
standing medication) is read from features 006/007/010/013c and combined server-side into one
`GET /locations/{id}/meal-list?date=` response. Ships a director-web print-ready page (new
`web/app/(app)/meal-list/` route, sidebar nav entry, CSS print stylesheet — no PDF) and a
caregiver-tablet read-only screen reachable from the room home screen, scoped to the device's own
`GroupId` claim (feature 008a's existing device-token pattern) and following the existing
offline-cache-fallback convention (`healthSummary.ts`'s precedent) for offline reads.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web, Next.js 15 App Router; mobile,
Expo/React Native).

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9 (backend); Tailwind + shadcn/ui,
`next-intl`, openapi-fetch generated client (web); `expo-router`, `react-i18next`,
`expo-localization`, `lucide-react-native` (mobile). No new package dependencies on any surface.

**Storage**: PostgreSQL 16, schema-per-tenant — one additive migration creating the
`child_meal_preferences` table on the tenant schema.

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, per Constitution V);
Jest + React Testing Library (web, per feature 007a's precedent); Jest +
`@testing-library/react-native` (mobile, per feature 013c's cache-fallback test precedent).

**Target Platform**: Director web (desktop, 1280px+, print output); caregiver tablet (landscape,
read-only); backend (ASP.NET Core Minimal API, Cloud Run).

**Project Type**: Web application (backend API + Next.js web + Expo mobile, existing monorepo
structure — no new project).

**Performance Goals**: Single aggregation query per location/date request — no N+1 per child, for
locations with up to ~40 children.

**Constraints**: EF Core migrations MUST NOT auto-apply in production (CLAUDE.md); all
user-facing strings MUST use i18n keys (NL/FR/EN, Constitution IV); write (meal-preference
create/update) MUST remain `DirectorOnly`; the meal-list read MUST be reachable by Director,
Staff, and the caregiver-tablet device token (`DeviceOrStaffOrDirector`), scoped to the device's
own `GroupId` claim for the tablet case; MUST NEVER be reachable by the parent app.

**Scale/Scope**: One new backend table + migration, one new aggregation query, one new write
command, ~1 new web route + sidebar entry + print stylesheet, one new mobile screen + service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | Pass | New endpoints sit behind existing `TenantMiddleware`; the aggregation query reads only `ITenantDbContext` tables, all already tenant-scoped. |
| II. Regulatory Compliance by Design | N/A | This feature enforces no BKR ratio, contract-overlap, or closure-notification rule — it is a read-model aggregation, not a new regulatory control. |
| III. CQRS via MediatR & Thin Endpoints | Pass | New `GetMealListQuery` (read) and `UpsertMealPreferenceCommand` (write) as MediatR requests; `MealListEndpoints.cs` maps HTTP only, per research.md R1. |
| IV. Internationalization First | Pass | All new UI strings (texture/dietary/portion labels, "Geen voorkeur", "Inclusief verwacht", "Verwacht") ship as NL/FR/EN keys on both `web/i18n/locales/` and `mobile/i18n/locales/`. |
| V. Test with Real Infrastructure | Pass | Backend tests for the aggregation query and the upsert command run against TestContainers PostgreSQL — no InMemory provider. |
| VI. Secure Configuration & Storage | Pass | Migration authored as a normal EF Core migration file; SQL script generated and run manually, no auto-apply. No new secret/storage surface — no photo/attachment on this entity. |
| VII. Monolith-First Simplicity | Pass | No new project or service — extends the existing five-project backend and the existing `web`/`mobile` apps. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013d-meal-list/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── meal-list-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/MealPreference.cs                     # NEW
├── ChildCare.Domain/Enums/MealTexture.cs                           # NEW
├── ChildCare.Domain/Enums/MealPortionSize.cs                       # NEW
├── ChildCare.Domain/Enums/DietaryType.cs                           # NEW
├── ChildCare.Application/MealPreferences/
│   ├── UpsertMealPreferenceCommand.cs                              # NEW — DirectorOnly write
│   ├── UpsertMealPreferenceCommandValidator.cs                     # NEW
│   ├── GetMealListQuery.cs                                         # NEW — aggregation read
│   └── MealListMapper.cs                                           # NEW
├── ChildCare.Contracts/
│   ├── Requests/MealPreferenceRequests.cs                          # NEW
│   └── Responses/MealListResponse.cs                               # NEW
├── ChildCare.Api/Endpoints/MealListEndpoints.cs                    # NEW — thin HTTP mapping
├── ChildCare.Infrastructure/Persistence/
│   ├── TenantDbContext.cs                                          # + MealPreferences DbSet
│   └── Migrations/Tenant/<timestamp>_AddChildMealPreferences.cs
└── ChildCare.Api.Tests/MealList/
    ├── MealListAggregationTests.cs                                 # happy path + negative flows
    └── UpsertMealPreferenceTests.cs

web/
├── app/(app)/meal-list/page.tsx                                    # NEW — director screen + print
├── app/(app)/meal-list/print.css                                   # NEW — print stylesheet (or scoped <style>)
├── components/meal-list/
│   ├── MealListTable.tsx                                           # NEW
│   └── AllergySeverityBadge.tsx                                    # NEW — icon+color pairing
├── components/children/ChildMealPreferenceForm.tsx                 # NEW — director edit, on child profile
├── components/Sidebar.tsx                                          # + "Maaltijdenlijst" nav entry
├── i18n/locales/{en,fr,nl}.json                                    # + mealList.* keys
├── lib/generated/api-types.ts                                      # regenerated (mechanical)
└── __tests__/ — MealListTable, AllergySeverityBadge, ChildMealPreferenceForm

mobile/
├── app/(room)/meal-list.tsx                                        # NEW — caregiver read-only screen
├── services/mealList.ts                                            # NEW — fetch + cache-fallback, mirrors healthSummary.ts
├── i18n/locales/{en,fr,nl}.json                                    # + mealList.* keys
└── __tests__/ — meal-list screen rendering, offline-cache-fallback case
```

**Structure Decision**: No new top-level project or app. Backend adds one new vertical slice
(`MealPreferences/` in Application, mirroring the existing per-feature folder convention) reading
across four existing entities without modifying any of them. Web adds a new top-level route
(`meal-list/`) plus a `components/meal-list/` directory (mirroring `components/health/`'s
existing convention) and one new sidebar entry. Mobile adds one new screen under the existing
`(room)/` route group (alongside the existing room-mode screens) and one new service module
following `healthSummary.ts`'s established fetch-then-cache-fallback pattern exactly — no new
mobile offline-queue or cache mechanism.
