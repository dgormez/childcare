# Implementation Plan: Web Admin Scaffold

**Branch**: `007a-web-admin-scaffold` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007a-web-admin-scaffold/spec.md`

## Summary

Bootstrap the director-facing Next.js web app: remove the Habits walking-skeleton template,
migrate the existing raw-`fetch` API client to the openapi-typescript + openapi-fetch pattern
feature 008 established for mobile, wire director authentication (email/password + Google OAuth,
reusing feature 003's contract as-is) with the refresh token in an httpOnly cookie (already
scaffolded), build a collapsible sidebar navigation shell, and ship two real screens — Staff
(list/search/PIN-reset/deactivate, over feature 005 + 008a's existing endpoints) and Devices
(list/revoke, over feature 008a's endpoints). Two small, minimal backend additions are required
because the consuming UI surfaced gaps no existing endpoint covers: a read-only device-listing
endpoint (008a built pairing/revocation but never a list), and exposing the tenant's and
director's display names (no existing endpoint returns either). Both follow existing
conventions exactly — no new tables, no new authorization rules, no new business logic.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, minimal addition only); TypeScript 5 / Next.js 15
(App Router) — new work is 100% web; backend is a two-endpoint, additive-field touch.

**Primary Dependencies**: Next.js 15, React 19, Tailwind CSS + shadcn/ui, `next-intl` (new — no
i18n exists in `web/` today), `openapi-typescript` + `openapi-fetch` (new to `web/`, already
proven in `mobile/`). Backend: no new packages — same ASP.NET Minimal APIs / MediatR / EF Core 9
stack, extending `StaffEndpoints`-sibling patterns.

**Storage**: PostgreSQL 16, tenant schema — no schema changes. The new devices-list query reads
the existing `DevicePairing` table (feature 008a); the name-exposure addition reads existing
`TenantUser.Name` and `Tenant.Name` columns. No migrations.

**Testing**: Vitest (already configured in `web/`) for component/unit tests of the login flow,
sidebar shell, staff table (search/filter/actions), and devices table. xUnit + TestContainers
(constitution Principle V) for the two new backend query handlers (`ListDevicesQuery`,
organisation-name endpoint) and the `AuthenticatedUser.Name` addition across all four auth
handlers (login, refresh, Google, Apple — Apple is parent-app-only but shares the response type,
so it must stay consistent even though this feature doesn't touch parent-app UI).

**Target Platform**: Web (director), desktop/laptop browser — high-density layout per
`platform-rules.md`. Backend: existing ASP.NET Core API on Cloud Run, unchanged deployment.

**Project Type**: Mixed (frontend web + minimal backend) — mirrors feature 008's
mobile-plus-zero-backend-changes shape, except this feature needs two small backend additions
feature 008 didn't.

**Performance Goals**: Staff/devices tables must filter client-side with no perceptible lag at
tens-to-low-hundreds of rows (spec SC-002) — no server-side pagination required yet.

**Constraints**: Refresh token MUST be stored as an httpOnly cookie (existing
`app/api/set-refresh-token` route), never `localStorage`/`sessionStorage`. No offline behavior —
network failures show a retryable error state only. All new UI strings ship with NL/FR/EN keys
from day one (constitution Principle IV).

**Scale/Scope**: Two real screens (Staff, Devices) plus login + sidebar shell. Placeholder nav
entries for not-yet-built sections (Locations, Contracts, Children, etc.) are inert.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | All new/extended endpoints (`GET /api/devices`, the org-name read) run behind `TenantMiddleware`/`DirectorOnly` exactly like every existing `StaffEndpoints`/`LocationEndpoints` route — queries filter through `ITenantDbContext`/`ICurrentTenantService`, never a raw cross-tenant query. |
| II. Regulatory Compliance by Design | PASS (not applicable) | This feature introduces no regulatory logic (BKR, overlap, closure) — it is a UI layer over already-compliant backend features. |
| III. CQRS via MediatR & Thin Endpoints | PASS | The new `ListDevicesQuery` and the organisation-name read follow the existing MediatR query pattern (mirrors `ListStaffQuery`); the `AuthenticatedUser.Name` addition is a field on an existing response, populated inside the existing four auth command handlers — no endpoint gains business logic. |
| IV. Internationalization First | PASS | `next-intl` is wired from this feature's first commit; every user-facing string (login form, sidebar labels, table headers, empty/error states, confirmation dialogs) ships with NL/FR/EN keys, no hardcoded text. |
| V. Test with Real Infrastructure | PASS | Backend additions get TestContainers-backed integration tests (real Postgres), consistent with every prior feature. Frontend gets Vitest component tests against a mocked API layer (typed via the generated OpenAPI client), matching `web/`'s existing Vitest setup — there is no frontend equivalent of "real infrastructure" beyond the real generated types. |
| VI. Secure Configuration & Storage | PASS | Refresh token stays in an httpOnly cookie (existing BFF route), never client-readable storage. No secrets introduced. Errors surfaced to the director are locale-keyed messages, never raw exceptions. |
| VII. Monolith-First Simplicity | PASS | No new backend project. The two additions land in the existing `ChildCare.Api`/`Application`/`Contracts` projects, following existing file/folder conventions (`Endpoints/`, `Application/Devices/`, `Application/Organisations/`). |

**Constitution Check re-affirmed post-Phase-1**: research.md's two decisions (minimal
devices-list query, name-exposure via existing entities) introduce no new tenant-isolation
surface, no new authorization policy, and no schema change — the table above holds unchanged
after design.

## Project Structure

### Documentation (this feature)

```text
specs/007a-web-admin-scaffold/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── devices-list-api.md
│   └── auth-name-exposure-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Application/
│   ├── Devices/
│   │   └── ListDevicesQuery.cs          # NEW — mirrors ListStaffQuery.cs
│   ├── Organisations/
│   │   └── GetCurrentOrganisationQuery.cs  # NEW
│   └── Auth/
│       ├── LoginCommandHandler.cs        # EDIT — AuthenticatedUser.Name
│       ├── RefreshTokenCommandHandler.cs # EDIT — AuthenticatedUser.Name
│       ├── GoogleSignInCommandHandler.cs # EDIT — AuthenticatedUser.Name
│       └── AppleSignInCommandHandler.cs  # EDIT — AuthenticatedUser.Name
├── ChildCare.Api/
│   └── Endpoints/
│       ├── DevicePairingEndpoints.cs     # EDIT — add GET /api/devices
│       └── OrganisationEndpoints.cs      # EDIT — add GET /api/organisations/me
├── ChildCare.Contracts/
│   └── Responses/
│       ├── RoomShiftResponses.cs         # EDIT — add DeviceSummaryResponse
│       ├── AuthSessionResponse.cs        # EDIT — AuthenticatedUser gains Name
│       └── OrganisationResponse.cs       # NEW
└── ChildCare.Api.Tests/
    ├── DeviceListingTests.cs             # NEW
    ├── OrganisationEndpointTests.cs      # NEW
    └── AuthEndpointTests.cs              # EDIT — assert Name on existing flows

web/
├── app/
│   ├── (auth)/
│   │   └── login/page.tsx                # EDIT — rebuilt on design-system tokens + i18n
│   ├── (app)/
│   │   ├── layout.tsx                    # EDIT — sidebar shell, org/director name, nav
│   │   ├── staff/page.tsx                # NEW — staff table, search, PIN reset, deactivate
│   │   └── devices/page.tsx              # NEW — devices table, revoke
│   ├── habits/, subscription/, settings/ # REMOVED (Habits template)
│   └── api/                              # UNCHANGED (set-refresh-token/refresh/logout/clear)
├── components/
│   ├── AuthProvider.tsx                  # EDIT — session gains organisation/director name
│   ├── GoogleSignInButton.tsx            # EDIT — i18n strings, design tokens
│   ├── Sidebar.tsx                       # NEW
│   ├── StaffTable.tsx                    # NEW
│   ├── DevicesTable.tsx                  # NEW
│   ├── ConfirmDialog.tsx                 # NEW — shared confirmation modal
│   ├── EmptyState.tsx                    # NEW — shared empty state
│   └── ErrorState.tsx                    # NEW — shared retryable error state
├── lib/
│   ├── apiClient.ts                      # NEW — openapi-fetch client, replaces lib/api.ts
│   ├── auth.ts                           # EDIT — built on apiClient.ts
│   └── generated/api-types.ts            # NEW — openapi-typescript output, committed (matches mobile/services/generated/api-types.ts's precedent — CI never runs a live backend to regenerate it)
├── i18n/
│   └── locales/{nl,fr,en}.json           # NEW
├── theme/
│   └── colors.ts                         # NEW — TS port of mobile/theme/colors.js for web reuse
├── tailwind.config.ts                    # EDIT — consume theme/colors.ts, add shadcn preset
└── __tests__/                            # EDIT/NEW — cover login, sidebar, staff/devices tables
```

**Structure Decision**: Web application (Option 2 shape), but only the `web/` side is
substantially new — `backend/` gets two small, additive endpoints/fields rather than a new
surface. This mirrors feature 008's "scaffold + shared infra, zero domain features" shape,
adapted for Next.js's App Router conventions (route groups `(auth)`/`(app)`) instead of Expo
Router.

## Complexity Tracking

*No constitution violations — table intentionally omitted.*
