# Implementation Plan: Child Events — Custom Type & Growth Check Rename

**Branch**: `009a-child-events-custom-type` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009a-child-events-custom-type/spec.md`

## Summary

Add a 12th `ChildEventType` value, `custom` (`{ label, text? }`, label required, plain free
text with no autocomplete), to feature 009's existing single-table `child_events` design and
its caregiver-tablet quick-entry/timeline UI. Bundle a migration-safe rename of the existing
`measurement` type to `growth_check` (same payload, new name) as a one-time per-tenant data
backfill (new `backfill-growth-check` CLI subcommand, mirroring feature 002's `migrate-tenants`
pattern) run as an explicit pre-deploy step before the app code that removes `measurement`
recognition ships — a hard cutover, not an ongoing dual-write window.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / React Native + Expo (mobile,
Expo Router, NativeWind) — unchanged from feature 009.

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9 (Npgsql) — all reused as-is; no
new dependency introduced.

**Storage**: PostgreSQL 16, tenant schema — extends the existing `child_events` table (feature
009); no new table, no column change. One-time data backfill via raw SQL against the existing
`event_type` column.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, constitution Principle V),
Jest (mobile) — same suites feature 009 already established, extended with new cases.

**Target Platform**: Caregiver Expo app (tablet, landscape) for the `custom` quick-entry UI;
ASP.NET Core Minimal API (Cloud Run) for validator/enum/CLI changes; no parent-facing or
web-admin UI (neither exists for child events today).

**Project Type**: Data-model change with an accompanying caregiver-tablet UI addition (per
spec.md Product Context).

**Performance Goals**: No new performance target — `custom`/`growth_check` reuse feature 009's
existing indexes and query patterns unchanged.

**Constraints**: The rename must be a one-time cutover with no ongoing dual-write window
(FR-008); the backfill must complete, per tenant, before the new binary serves traffic
(research.md R2) to avoid a read-time parse failure on any un-migrated row.

**Scale/Scope**: One new enum value + validator arm + mobile quick-entry/timeline case; one enum
value renamed end-to-end (backend wire mapping, mobile TS union, i18n keys, test fixtures); one
new CLI subcommand; no new endpoint, no new table.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Multi-Tenant Isolation (NON-NEGOTIABLE)**: PASS. No change to how `child_events` is
  scoped — still exclusively via `TenantDbContext`/`search_path`. The new `backfill-growth-check`
  CLI command loops per-tenant using the existing `ITenantDbContextResolver.ForSchema` pattern
  (identical to `migrate-tenants`), never touching more than one tenant's schema per operation.
- **II. Regulatory Compliance by Design**: PASS. Not a BKR/day-overlap/closure-calendar feature;
  `growth_check` preserves the same legally-relevant weight/height/head-circumference fields
  `measurement` already had, with no change to what's recorded.
- **III. CQRS via MediatR & Thin Endpoints**: PASS. No new endpoint. The existing
  `RecordChildEventCommand`/`UpdateChildEventCommand` handlers and `ChildEventPayloadValidator`
  gain a new switch arm each; no business logic is added to `ChildEventEndpoints.cs`. The CLI
  command is a standalone startup-time operation (same pattern as `migrate-tenants`), not a
  MediatR request — consistent with how `migrate-tenants` itself is structured today.
- **IV. Internationalization First (NON-NEGOTIABLE)**: PASS. The `custom` label
  prompt/placeholder and the renamed `growth_check` display string go through the existing
  `i18n/locales/{nl,fr,en}.json` mechanism; no hardcoded strings introduced.
- **V. Test with Real Infrastructure (NON-NEGOTIABLE)**: PASS. New validator/CLI tests run
  against TestContainers PostgreSQL per existing `ChildCare.Api.Tests` setup — the backfill
  command in particular needs a real Postgres to prove its raw SQL runs correctly per schema.
- **VI. Secure Configuration & Storage**: PASS. No new secrets, no new file storage. The
  backfill is a manually-triggered, reviewed SQL operation — the exact discipline this principle
  already requires for schema migrations, applied here to a data-value change with the same
  care (research.md R2 explains why this one is not a candidate for the new-tenant-schema
  auto-apply carve-out: it touches existing, populated schemas).
- **VII. Monolith-First Simplicity**: PASS. No new project/service.

No violations. Complexity Tracking table is not needed.

**Constitution documentation note (non-gating)**: the constitution's Development Workflow &
Phase Discipline section lists `child_events`'s event types by name (including `measurement`) as
descriptive context, not a governing rule. This feature's implementation phase updates that list
to reflect `growth_check`/`custom` as a documentation-accuracy fix (PATCH-level wording update,
no principle change) — the same category of upkeep feature 008a's MINOR amendment made for a
substantive rule change, but this one is purely descriptive.

## Project Structure

### Documentation (this feature)

```text
specs/009a-child-events-custom-type/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── child-events-api-delta.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Enums/
│       ├── ChildEventType.cs                    # Measurement → GrowthCheck; + Custom
│       └── ChildEventTypeExtensions.cs          # wire-string mapping updated
├── ChildCare.Application/
│   └── ChildEvents/
│       └── ChildEventPayloadValidator.cs        # + Custom arm; Measurement arm → GrowthCheck
├── ChildCare.Api/
│   └── Cli/
│       └── BackfillGrowthCheckCommand.cs        # new — mirrors MigrateTenantsCommand
├── ChildCare.Api.Tests/
│   └── ChildEvents/
│       ├── ChildEventPayloadValidationTests.cs  # measurement fixtures → growth_check; + custom cases
│       └── BackfillGrowthCheckCommandTests.cs   # new
└── ChildCare.Api/Program.cs                     # + `backfill-growth-check` CLI dispatch

mobile/
├── types/index.ts                               # ChildEventType union: measurement → growth_check; + custom
├── components/
│   ├── QuickActionSheet.tsx                     # + Custom free-text entry (label + optional text)
│   └── EventTimeline.tsx                        # + custom render case (label as headline)
├── components/EditEventModal.tsx                # measurement → growth_check references updated
└── i18n/locales/{nl,fr,en}.json                  # measurement key renamed; + custom keys
```

**Structure Decision**: Pure extension of feature 009's existing files — no new backend
project, no new mobile module. The only new file is the CLI command
(`BackfillGrowthCheckCommand.cs`), which follows `MigrateTenantsCommand.cs`'s exact existing
pattern in the same directory.

## Complexity Tracking

> No violations — table intentionally omitted.
