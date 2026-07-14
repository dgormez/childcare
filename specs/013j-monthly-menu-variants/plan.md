# Implementation Plan: Monthly Menu Variants

**Branch**: `013j-monthly-menu-variants` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013j-monthly-menu-variants/spec.md`

## Summary

Extend 013e's monthly menu with an optional dietary-variant dimension: `MonthlyMenu` gains a
`Variant` column (one of 013d's 5 `DietaryType` values, or "base" for the existing menu), and
`Location` gains a director-configured, ordered `MenuVariantPriorityOrder`. Director-web reuses
the exact same day-grid/CSV-import UI, parameterized by variant. The parent-facing read is
restructured from one entry per location to one entry per (location, child) pair, resolving each
child's `MealPreference.DietaryType` list against the location's priority order to pick the
correct published menu, falling back to the base menu when nothing matches.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, matching this codebase's fixed stack); TypeScript 5
/ React 19 (`web/`, `parent-mobile/`).

**Primary Dependencies**: MediatR + FluentValidation (backend CQRS, Constitution III); EF Core 9
/ PostgreSQL (existing `text[]`/enum-as-text conversion patterns, `MealPreference.DietaryType`
and `DayReservation.Type` respectively); Next.js/Tailwind/shadcn (`web/`, reusing
`ReservationSettingsForm.tsx`'s per-location-settings pattern, 013f); Expo/React Native
(`parent-mobile/`, reusing `services/menu.ts`'s fetch-then-cache-fallback pattern, 013e).

**Storage**: PostgreSQL, tenant schema. New migration: `MonthlyMenu.Variant` (non-nullable
`text`, default `'base'`) plus a rebuilt unique index; `Location.MenuVariantPriorityOrder`
(`text[]`, default `'{}'`).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V — real
database, never InMemory); Vitest + Testing Library (`web/`); Jest + `@testing-library/
react-native` (`parent-mobile/`).

**Target Platform**: Director web (settings + authoring) and parent mobile (resolved read). No
caregiver-tablet surface — 013e never gave caregivers any menu interaction.

**Performance Goals**: Not a hot path — at most 6 `MonthlyMenu` rows per location/month (base +
5 variants), resolution is O(children × locations) per parent request, all small bounded counts.

**Constraints**: The base-menu path (`Variant == "base"`) MUST be behaviorally identical to
013e/013i's shipped behavior for any location that never configures a variant (FR-012/SC-003) —
this is the primary regression risk of this feature and drives the "reuse the base menu's exact
write/read path, just parameterized" architecture below rather than a parallel implementation.

**Scale/Scope**: One new backend command (`UpdateLocationMenuVariantSettingsCommand`), four
extended commands/queries (Get/Upsert/Publish/Unpublish MonthlyMenu, now variant-aware),
one restructured query (`GetParentMonthlyMenuQuery`), one migration, director-web variant
selector + settings UI, parent-mobile per-child rendering.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | All new/extended queries and commands go through `ITenantDbContext`, identical to every existing `MonthlyMenus`/`Locations` access in this codebase. No new context or connection path. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass (N/A) | Menu variants are not a BKR/regulatory-ratio concern; untouched by this feature. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | New settings write is a MediatR command with FluentValidation (mirrors `UpdateLocationReservationSettingsCommand`, 013f). Existing menu commands/queries are extended in place, not bypassed. Endpoint files gain only a `variant` parameter passthrough, no new logic. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (variant selector labels, settings UI, parent-facing per-child variant label) MUST be added as locale keys on both `web/i18n/locales/{en,fr,nl}.json` and `parent-mobile`'s equivalent — no hardcoded text. `DietaryType` display names should reuse whatever 013d already established for these five values rather than inventing new copy. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | The resolution logic (multi-`DietaryType` priority walk, fallback to base) is exactly the kind of regulatory-adjacent-but-not-regulatory correctness logic this principle calls out for real-PostgreSQL integration testing, not a unit-test-only double. |
| VI. Secure Configuration & Storage | ✅ Pass (N/A) | No secrets, no file storage. |
| VII. Monolith-First Simplicity | ✅ Pass | No new project or service — extensions within the existing five backend projects and three client apps. |

No violations. Complexity Tracking table below is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013j-monthly-menu-variants/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/            # Phase 1 output
│   └── monthly-menu-variants-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/MonthlyMenu.cs                        # extended: Variant field
│   └── Entities/Location.cs                            # extended: MenuVariantPriorityOrder
├── ChildCare.Application/
│   ├── MonthlyMenus/
│   │   ├── GetMonthlyMenuQuery.cs                       # extended: variant param
│   │   ├── UpsertMonthlyMenuCommand.cs                  # extended: variant param + not-enabled rejection
│   │   ├── PublishMonthlyMenuCommand.cs                 # extended: variant param
│   │   ├── UnpublishMonthlyMenuCommand.cs               # extended: variant param
│   │   ├── GetParentMonthlyMenuQuery.cs                 # rewritten: per-(location,child) resolution
│   │   └── MonthlyMenuMapper.cs                         # extended: variant in response
│   └── Locations/
│       ├── UpdateLocationMenuVariantSettingsCommand.cs  # NEW
│       └── LocationMapper.cs                            # extended: MenuVariantPriorityOrder in response
├── ChildCare.Contracts/
│   ├── Requests/MonthlyMenuRequests.cs                  # extended
│   ├── Requests/LocationRequests.cs                     # extended
│   └── Responses/MonthlyMenuResponse.cs                 # extended (variant, per-child parent entry)
├── ChildCare.Infrastructure/Persistence/
│   ├── TenantDbContext.cs                               # extended: Variant/MenuVariantPriorityOrder config
│   └── Migrations/Tenant/                                # NEW migration
└── ChildCare.Api/Endpoints/
    ├── MonthlyMenuEndpoints.cs                           # extended: variant query param
    └── LocationEndpoints.cs                              # extended: menu-variant-settings route

web/
├── app/(app)/menu/page.tsx                               # extended: variant selector wiring
├── app/(app)/locations/[id]/page.tsx                     # extended: new settings tab/section
├── components/menu/
│   ├── MonthlyMenuDayGrid.tsx                            # extended: accepts variant prop (thin)
│   └── MonthlyMenuVariantSelector.tsx                    # NEW
├── components/
│   └── MenuVariantSettingsForm.tsx                       # NEW (mirrors ReservationSettingsForm.tsx)
└── i18n/locales/{en,fr,nl}.json                           # extended

parent-mobile/
├── services/menu.ts                                      # extended: per-child response shape
├── app/(app)/menu/                                       # extended: per-child sections
└── i18n/locales/{en,fr,nl}.json                           # extended
```

**Structure Decision**: Extends three existing surfaces (backend, `web/`, `parent-mobile/`) in
place — no new projects. The base-menu code paths are extended with a variant parameter rather
than duplicated, so there is exactly one implementation of authoring/resolution logic serving
both the base menu and every variant (the mechanism that makes FR-012/SC-003's "zero behavior
change for the common case" guarantee actually hold, rather than something two parallel
implementations could silently drift apart on).
