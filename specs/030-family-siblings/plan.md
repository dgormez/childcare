# Implementation Plan: Family Siblings

**Branch**: `030-family-siblings` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/030-family-siblings/spec.md`

## Summary

Close the genuine sibling-support gaps left after auditing the existing multi-child data model
(006/013's `Contact`/`ChildContact`, already-multi-child-aware parent-mobile screens): a bulk
day-reservation action across siblings, an opt-in per-location sibling invoice discount, an
opt-in per-location family invoice bundling (PDF/payment grouped, per-child data untouched), a
first-ever web admin UI for the already-existing contact-linking endpoints (with duplicate-
contact detection), two new `ContactRelationship` values, and a parent-facing "previous
children" view for departed siblings. No new tables — every change extends `Location`,
`Invoice`, or the `ContactRelationship` enum additively, and reuses `ChildContact.IsPrimary` as
the sibling-grouping key throughout.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / React 19 (`web/`,
`parent-mobile/`).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III, unchanged); EF Core 9 /
PostgreSQL; QuestPDF (extends the existing `QuestPdfInvoiceGenerator` pattern for the new
combined family PDF, research.md R5); Next.js/Tailwind (`web/`, new Contacts tab + extended
location settings form); Expo/React Native (`parent-mobile/`, extended day-reservation form +
new "previous children" screen).

**Storage**: PostgreSQL, tenant schema only — no public-schema or new-table changes. `Location`
gains `SiblingDiscountPct`/`FamilyInvoiceBundlingEnabled`; `Invoice` gains `FamilyGroupId`
(nullable). Full shapes in data-model.md.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V) — sibling-
discount tie-breaking, bundling group assignment, and the paid-cascade across grouped invoices
are exactly the money-correctness logic this principle calls out for real-database integration
tests. Jest + `@testing-library/react-native` (`parent-mobile/`). Vitest + Testing Library
(`web/`).

**Target Platform**: Parent mobile (bulk day reservations, previous children, combined invoice
view) and director web (sibling-billing settings, new Contacts tab). No caregiver-tablet
surface — this feature has no caregiver-facing behavior.

**Performance Goals**: Not a hot path — bulk day-reservation submission fans out to at most a
handful of siblings per action (KDV family sizes), and sibling-discount/bundling grouping runs
once per monthly invoice-generation batch, an already-existing, already-bounded operation
(`GenerateInvoicesCommand` loops the location's active contracts once).

**Constraints**: Money in integer cents throughout (matches 014/014a's existing convention);
`SiblingDiscountPct`/`FamilyInvoiceBundlingEnabled` default to 0/false so no existing location's
invoice output changes unless a director explicitly opts in (spec SC-005). A bundled invoice
never replaces or restructures the underlying per-child `Invoice` row (Clarifications) — 018's
management reporting and every existing per-`Invoice` query continue to work unmodified.

**Scale/Scope**: Two `Location` field additions, one `Invoice` field addition, one enum
extension (no new entities/tables), one new bulk-day-reservation endpoint + handler, ~4 new
invoice-related endpoints/handlers (settings, family PDF, paid-cascade extension, generation
extension), one new parent "previous children" endpoint + screen, one new web Contacts tab
consuming five already-existing endpoints for the first time.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | All reads/writes go through `ITenantDbContext`, identical to every existing feature. No public-schema entity introduced. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass (N/A) | Not a BKR-ratio, split-location, or closure-notification concern. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | `SubmitBulkDayReservationCommand`, `UpdateLocationSiblingBillingSettingsCommand` are MediatR commands with FluentValidation; `GetParentPreviousChildrenQuery`, `GenerateFamilyInvoicePdfQuery` are queries. `GenerateInvoicesCommand` and `MarkInvoicePaidCommand` are extended in place, not duplicated. Endpoint files gain only route/DTO mapping. |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (bulk-reservation UI, sibling-discount/bundling settings labels, discount line-item label, Contacts tab, "previous children" UI) go through `web/i18n/locales/{en,fr,nl}.json` and `parent-mobile`'s equivalent. |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Sibling-discount tie-breaking, bundling group assignment/paid-cascade, and duplicate-contact linking are exactly the correctness logic this principle calls out for real-PostgreSQL integration tests, not mocks. |
| VI. Secure Configuration & Storage | ✅ Pass | No new secrets/credentials. Combined family PDF is rendered on-demand like the existing invoice PDF (no new storage/signed-URL concern). Authorization for the new parent endpoints reuses the existing `ICurrentParentContactResolver` link-check pattern — a parent can only ever reach children/invoices they're linked to. |
| VII. Monolith-First Simplicity | ✅ Pass | No new backend project, no new table, no new external dependency. Reuses existing `QuestPdfInvoiceGenerator`, `ContactsEndpoints`, and `SubmitDayReservationCommand` rather than parallel implementations. |

No unjustified violations. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/030-family-siblings/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── family-siblings-api.md  # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Location.cs                             # extended: SiblingDiscountPct, FamilyInvoiceBundlingEnabled
│   ├── Entities/Invoice.cs                               # extended: FamilyGroupId
│   └── Enums/ContactRelationship.cs                       # extended: FosterParent, Other
├── ChildCare.Application/
│   ├── DayReservations/
│   │   └── SubmitBulkDayReservationCommand.cs             # NEW (research.md R1)
│   ├── Invoices/
│   │   ├── GenerateInvoicesCommand.cs                     # extended: discount + bundling grouping (research.md R2/R3/R4)
│   │   ├── MarkInvoicePaidCommand.cs                      # extended: paid-cascade across FamilyGroupId (research.md R5)
│   │   ├── GenerateFamilyInvoicePdfQuery.cs                # NEW (research.md R5)
│   │   └── GetParentInvoicesQuery.cs                       # extended: group by FamilyGroupId in response mapping
│   ├── Locations/
│   │   └── UpdateLocationSiblingBillingSettingsCommand.cs  # NEW (mirrors UpdateLocationInvoiceSettingsCommand, 014)
│   └── Parent/
│       └── GetParentPreviousChildrenQuery.cs               # NEW (research.md R8)
├── ChildCare.Contracts/
│   ├── Requests/DayReservationRequests.cs                  # extended: BulkDayReservationRequest
│   ├── Requests/LocationRequests.cs                        # extended: sibling-billing settings
│   └── Responses/InvoiceResponses.cs                        # extended: FamilyGroupId/family grouping shape; ParentPreviousChildResponse NEW
├── ChildCare.Infrastructure/
│   ├── Pdf/QuestPdfFamilyInvoiceGenerator.cs                # NEW (research.md R5)
│   └── Persistence/Migrations/Tenant/                        # NEW migration: Location + Invoice columns
├── ChildCare.Api/
│   ├── Endpoints/DayReservationEndpoints.cs                  # extended: POST bulk route
│   ├── Endpoints/InvoiceEndpoints.cs                          # extended: family PDF route
│   ├── Endpoints/LocationEndpoints.cs                         # extended: sibling-billing-settings route
│   └── Endpoints/ParentEndpoints.cs                           # extended: previous-children route

web/
├── app/(app)/children/[id]/page.tsx                            # extended: new Contacts tab
├── components/children/ChildContactsTab.tsx                    # NEW
├── components/children/LinkContactDialog.tsx                    # NEW (duplicate-detection UI, research.md R7)
├── app/(app)/locations/[id]/page.tsx                            # extended: sibling-billing fields on existing Invoicing tab
├── components/InvoiceSettingsForm.tsx                            # extended
└── i18n/locales/{en,fr,nl}.json                                  # extended

parent-mobile/
├── components/DayReservationForm.tsx                             # extended: "apply to all children" option
├── app/(app)/children/previous.tsx                               # NEW ("previous children" list, research.md R8)
├── app/(app)/index.tsx                                            # extended: entry point to previous.tsx when applicable
├── app/(app)/invoices/index.tsx                                   # extended: render grouped family invoice entries
├── services/dayReservations.ts                                    # extended: bulk submit
└── i18n/locales/{en,fr,nl}.json                                    # extended
```

**Structure Decision**: Extends five existing backend projects in place (no new project —
Constitution VII) and three existing frontend surfaces (no new app/screen family beyond one new
parent-mobile route and one new web component) rather than introducing new top-level structure —
matches the "audit and extend, don't duplicate" posture the spec itself established.
