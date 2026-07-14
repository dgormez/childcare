# Implementation Plan: Monthly Menu CSV Import

**Branch**: `013i-monthly-menu-csv-import` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013i-monthly-menu-csv-import/spec.md`

## Summary

Add a client-side "Import CSV" action to the existing director-web Menu section
(`web/app/(app)/menu/page.tsx` + `web/components/menu/MonthlyMenuDayGrid.tsx`) that parses a
director-supplied CSV entirely in the browser, validates it row-by-row, shows a preview of what
will be applied versus skipped (with reasons), and — on confirmation — merges the valid rows into
the day grid's existing in-memory per-date state. No new backend endpoint, no new data model: the
merged state is written through 013e's existing `PUT /api/locations/{locationId}/monthly-menus/
{year}/{month}` whole-month-replace call when the director presses the pre-existing Save button.

## Technical Context

**Language/Version**: TypeScript 5, React 19 (matches `web/`'s existing stack; no new language).

**Primary Dependencies**: Next.js 15 (App Router), Tailwind, shadcn/ui primitives already used by
`MonthlyMenuDayGrid.tsx`. New dependency: a small client-side CSV parser (`papaparse`) — chosen
over a hand-rolled parser because CSV quoting/escaping edge cases (commas or newlines inside a
quoted field, e.g. a `notes` value like `"Soep, dan hoofdgerecht"`) are easy to get subtly wrong
by hand and `papaparse` is a well-established, dependency-free, MIT-licensed parser already
common in this stack's ecosystem.

**Storage**: N/A — no new persisted data; reuses 013e's existing `monthly_menus`/
`monthly_menu_days` tables unchanged, through the existing write path only.

**Testing**: Vitest + `@testing-library/react` (`web/vitest.config.ts`, jsdom environment) —
matches every existing `web/__tests__` suite, including `MonthlyMenuDayGrid.test.tsx`.

**Target Platform**: Director-web only (desktop browser, 1280px+ viewport per
`platform-rules.md`). No caregiver-tablet or parent-mobile changes.

**Project Type**: Web application (existing Next.js `web/` app — this feature adds no new app or
service, only components/logic within it).

**Performance Goals**: Parsing and validating a full month's CSV (≤31 data rows) must complete
without a perceptible UI freeze — no specific numeric target needed given the trivial row count.

**Constraints**: Zero new backend endpoints or database changes (explicit spec constraint, FR-003
and FR-013). All new user-facing strings via `next-intl` (Constitution Principle IV).

**Scale/Scope**: One new UI flow (import button → file picker → preview → confirm) layered onto
one existing page and one existing component; a new CSV parsing/validation module; no scope
beyond director-web.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | No new endpoint, no new query. The feature reuses 013e's existing `PUT /api/locations/{locationId}/monthly-menus/{year}/{month}`, which already runs through `TenantMiddleware`/`ICurrentTenantService`. Nothing in this feature touches tenant data resolution. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass (N/A) | Monthly menu content is not a BKR/regulatory-ratio concern; this principle's specific rules (BKR ratios, split-location overlap, closure notifications) are untouched by a menu-authoring convenience. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass (N/A) | No new endpoint or command — this is a pure frontend feature. The eventual write still goes through 013e's existing `UpsertMonthlyMenuCommand`, unchanged. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (import action label, preview column headers, per-row error reasons, summary counts, template-download label) MUST be added as `next-intl` keys under the existing `menu` namespace in `web/i18n/locales/{en,fr,nl}.json` — no hardcoded text. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass (N/A) | No backend/integration surface is added; this principle governs API/integration tests against real PostgreSQL, which is unaffected since there's no new query or command. Frontend coverage uses this repo's existing Vitest/RTL pattern (see Testing above). |
| VI. Secure Configuration & Storage | ✅ Pass (N/A) | No secrets, no file storage — the CSV never leaves the browser; no GCP Cloud Storage or signed-URL path is involved. |
| VII. Monolith-First Simplicity | ✅ Pass | Adds no new project, service, or deployable — a new component/module inside the existing `web/` app only. |

No violations. Complexity Tracking table below is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013i-monthly-menu-csv-import/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── csv-format.md
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (repository root)

```text
web/
├── app/(app)/menu/
│   └── page.tsx                        # existing (013e) — wires the new import action in
├── components/menu/
│   ├── MonthlyMenuDayGrid.tsx           # existing (013e) — exposes a merge entry point
│   ├── MonthlyMenuCsvImportDialog.tsx   # NEW — file picker + preview + confirm UI
│   └── MealPreferenceRequestQueue.tsx   # existing (013e), untouched
├── lib/menu/
│   └── csvImport.ts                     # NEW — parse/validate/merge logic (no React, pure functions)
├── i18n/locales/
│   ├── en.json                          # existing — new keys added under "menu"
│   ├── fr.json                          # existing — new keys added under "menu"
│   └── nl.json                          # existing — new keys added under "menu"
└── __tests__/
    ├── MonthlyMenuDayGrid.test.tsx      # existing (013e), untouched
    ├── menuCsvImport.test.ts            # NEW — parser/validator unit tests
    └── MonthlyMenuCsvImportDialog.test.tsx  # NEW — component tests
```

**Structure Decision**: Pure frontend addition inside the existing `web/` Next.js app — no
`backend/` changes, no new top-level directory. Parsing/validation logic is isolated in
`web/lib/menu/csvImport.ts` as framework-free functions (mirrors this codebase's existing
pattern of keeping validation/business logic out of components, testable without rendering),
consumed by a new dialog component that only handles presentation and the confirm/merge
interaction.

## Complexity Tracking

Not applicable — Constitution Check above has no violations to justify.
