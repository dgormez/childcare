# Implementation Plan: Invoice Payments Plus

**Branch**: `014a-invoice-payments-plus` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014a-invoice-payments-plus/spec.md`

## Summary

Add online invoice payment on top of 014's shipped invoicing: directors connect their
organisation's Mollie account (OAuth, Mollie Connect for Platforms), parents pay a `Sent`
invoice via a Mollie-hosted checkout link, a tenant-resolving public webhook confirms payment
and drives 014's existing `Sent → Paid` transition, a betalingsbewijs renders on demand once
paid, and a new per-tenant CLI command (the first scheduled-job infrastructure in this
codebase) sends up to 3 capped, cadence-respecting reminders for overdue invoices at locations
that opt in. All behind an `IPaymentProvider` abstraction so a second PSP can be added later
without touching calling code.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / React 19 (`web/`,
`parent-mobile/`).

**Primary Dependencies**: MediatR + FluentValidation (Constitution III); EF Core 9 / PostgreSQL;
Mollie's REST API (Connect + Payments), called via a plain `HttpClient`-based adapter — no
Mollie .NET SDK dependency, matching this codebase's existing "thin HTTP port" pattern
(`IExpoPushSender`/`ExpoPushSender`, research.md R1); ASP.NET Core Data Protection
(`IDataProtector`, already part of the framework — research.md R3); Next.js/Tailwind (`web/`,
extending `settings/page.tsx` and `InvoiceSettingsForm.tsx`); Expo/React Native
(`parent-mobile/`, extending `services/invoices.ts` and the existing invoice detail screen).

**Storage**: PostgreSQL. New public-schema tables `payment_provider_connections` and
`payments` (research.md R2/R3 — cross-tenant webhook resolution requires these outside any
tenant schema). `Invoice` (tenant schema) gains `ReminderCount`/`LastReminderSentAt`. `Location`
(tenant schema) gains `PaymentRemindersEnabled`/`PaymentReminderDelayDays`/
`PaymentReminderCadenceDays`. Full shapes in data-model.md.

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, Constitution V) — webhook
idempotency and tenant-resolution-from-forged-payload are exactly the kind of correctness/
security logic this principle calls out for real-database integration tests, not a mocked
double. Vitest + Testing Library (`web/`). Jest + `@testing-library/react-native`
(`parent-mobile/`).

**Target Platform**: Director web (Mollie connection, reminder settings) and parent mobile
("Pay now", receipt). One new public (non-tenant-scoped) API route: the Mollie webhook. No
caregiver-tablet surface.

**Performance Goals**: Not a hot path for director/parent flows (same bound as 014 — an
explicit user action). The reminder CLI command iterates every tenant schema once daily;
bounded by total overdue-invoice count across the platform, run outside request-serving time
(research.md R4).

**Constraints**: Money in integer cents throughout (spec.md FR-019, matching 014's existing
convention); PSP fees recorded separately, invoice `TotalCents` never mutated (FR-011). OAuth
tokens encrypted at rest, never returned to any client (FR-002). The webhook MUST resolve
tenant/invoice only from a system-generated reference, never a client-supplied claim (FR-006).
014's billable-day rules, PDF content, and manual mark-paid flow are untouched (FR-021).

**Scale/Scope**: Two new public-schema entities, three small tenant-schema entity extensions, a
new `IPaymentProvider` abstraction + Mollie adapter, ~9 new endpoints, one new CLI command +
Cloud Scheduler/Cloud Run Job Terraform wiring (the first scheduled-job infra in this codebase),
one new director-web settings section + one extended settings tab, one new parent-mobile
payment/receipt flow on the existing invoice detail screen.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation (NON-NEGOTIABLE) | ✅ Pass | All tenant-schema domain reads/writes go through `ITenantDbContext`, identical to every existing feature. The two new public-schema tables (`PaymentProviderConnection`, `Payment`) are the deliberate exception required for webhook resolution (research.md R2/R3) — the webhook handler resolves `TenantId` from a system-generated reference it controls, then explicitly sets tenant context, before touching any tenant-schema data; it never trusts a client-supplied tenant claim. This is the same posture 013g's cross-schema `vaccine_types` reference already established, applied to a case where the cross-schema lookup is a security boundary, not just shared reference data. |
| II. Regulatory Compliance by Design (NON-NEGOTIABLE) | ✅ Pass (N/A) | Not a BKR-ratio, split-location, or closure-notification concern. |
| III. CQRS via MediatR & Thin Endpoints | ✅ Pass | Connect/disconnect/create-payment/webhook-process/send-reminder are MediatR commands with FluentValidation; status/payment-status/betalingsbewijs are MediatR queries. Endpoint files gain only route/DTO mapping, including the webhook endpoint (its handler is a command, not inline logic in `*Endpoints.cs`). |
| IV. Internationalization First (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | All new strings (connection status, reminder-settings labels, reminder notification copy, receipt content, payment states) go through `web/i18n/locales/{en,fr,nl}.json` and `parent-mobile`'s equivalent, plus the betalingsbewijs PDF's per-locale `Labels` dictionary (mirrors `QuestPdfInvoiceGenerator`'s existing pattern). |
| V. Test with Real Infrastructure (NON-NEGOTIABLE) | ✅ Pass, must verify at implementation | Webhook idempotency, cross-tenant-resolution-from-forged-reference, and the reminder cadence/cap logic are exactly the money-correctness/security-correctness logic this principle calls out for real-PostgreSQL integration tests. |
| VI. Secure Configuration & Storage | ✅ Pass | Mollie OAuth tokens are the first per-tenant third-party credential this codebase stores — encrypted at rest via `IDataProtector` (research.md R3), never logged, never returned to a client. Betalingsbewijs is rendered on-demand, no GCS storage/signed-URL concern (research.md R5, same as 014's own invoice PDF). No secret is hardcoded; Mollie's own OAuth client ID/secret come from the existing GCP Secret Manager / Terraform-provisioned config path. |
| VII. Monolith-First Simplicity | ✅ Pass, with a deliberate first-of-its-kind addition | No new backend project or microservice. The reminder job is this codebase's first scheduled-job infrastructure (a new Cloud Scheduler + Cloud Run Job execution of the existing container, research.md R4) — explicitly anticipated and deferred by two prior features' research notes ("revisit if a future feature needs real scheduled jobs... and fold this in then"); this is that feature. It reuses the existing CLI-command pattern (`Cli/`) rather than introducing an in-process `BackgroundService` or a new deployable. |

No unjustified violations. Complexity Tracking table below documents the one deliberate,
precedent-following addition (scheduled-job infra) rather than a violation needing rejection.

## Project Structure

### Documentation (this feature)

```text
specs/014a-invoice-payments-plus/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── payments-api.md  # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/PaymentProviderConnection.cs         # NEW (public schema)
│   ├── Entities/Payment.cs                            # NEW (public schema)
│   ├── Entities/Invoice.cs                             # extended: ReminderCount/LastReminderSentAt
│   ├── Entities/Location.cs                            # extended: PaymentReminders{Enabled,DelayDays,CadenceDays}
│   └── Enums/PaymentConnectionStatus.cs, PaymentStatus.cs  # NEW
├── ChildCare.Application/
│   ├── Common/IPaymentProvider.cs                      # NEW (research.md R1)
│   ├── Payments/
│   │   ├── ConnectPaymentProviderCommand.cs            # NEW
│   │   ├── CompletePaymentProviderOAuthCommand.cs      # NEW
│   │   ├── DisconnectPaymentProviderCommand.cs         # NEW
│   │   ├── GetPaymentConnectionStatusQuery.cs          # NEW
│   │   ├── CreatePaymentLinkCommand.cs                 # NEW (research.md R6 reuse logic)
│   │   ├── GetPaymentStatusQuery.cs                    # NEW
│   │   ├── ProcessPaymentWebhookCommand.cs              # NEW (research.md R2/R6)
│   │   ├── GenerateBetalingsbewijsQuery.cs              # NEW (research.md R5)
│   │   └── PaymentReminderNotificationService.cs        # NEW (mirrors InvoiceNotificationService)
│   ├── Locations/
│   │   └── UpdateLocationPaymentReminderSettingsCommand.cs  # NEW (mirrors UpdateLocationInvoiceSettingsCommand, 014)
│   └── Invoices/
│       └── InvoiceMapper.cs                             # extended: reminderCount/lastReminderSentAt
├── ChildCare.Contracts/
│   ├── Requests/PaymentRequests.cs                       # NEW
│   ├── Requests/LocationRequests.cs                      # extended
│   └── Responses/PaymentConnectionResponse.cs, PaymentLinkResponse.cs  # NEW
├── ChildCare.Infrastructure/
│   ├── Payments/MolliePaymentProvider.cs                  # NEW (research.md R1)
│   ├── Pdf/QuestPdfBetalingsbewijsGenerator.cs             # NEW (mirrors QuestPdfInvoiceGenerator)
│   └── Persistence/
│       ├── PublicDbContext.cs                              # extended: PaymentProviderConnection/Payment DbSets
│       ├── TenantDbContext.cs                              # extended: Invoice/Location field config
│       └── Migrations/{Public,Tenant}/                     # NEW migrations
├── ChildCare.Api/
│   ├── Endpoints/PaymentEndpoints.cs                        # NEW (director/parent/webhook routes)
│   ├── Endpoints/LocationEndpoints.cs                        # extended: payment-reminder-settings route
│   └── Cli/SendPaymentRemindersCommand.cs                    # NEW (research.md R4)

infra/gcp/
└── payment-reminders-scheduler.tf                             # NEW — Cloud Scheduler + Cloud Run Job wiring (research.md R4)

web/
├── app/(app)/settings/page.tsx                                 # extended: payment-connection section
├── app/(app)/locations/[id]/page.tsx                           # extended: reminder-settings fields on the existing Invoicing tab
├── components/InvoiceSettingsForm.tsx                          # extended
└── i18n/locales/{en,fr,nl}.json                                 # extended

parent-mobile/
├── services/payments.ts                                         # NEW
├── app/(app)/invoices/[id].tsx                                  # extended: "Pay now" + betalingsbewijs
└── i18n/locales/{en,fr,nl}.json                                  # extended
```

**Structure Decision**: A new `Payments`/`Payment*` slice across the existing five backend
projects (no new project — Constitution VII), a new public-schema pair of entities (the
deliberate cross-tenant exception, research.md R2/R3), one new Terraform-managed scheduled job
(the first in this codebase, research.md R4), and UI additions to two already-existing screens
per platform rather than new screens (research.md R7) — director web's org-level Settings page
and the location's existing Invoicing tab; parent-mobile's existing invoice detail screen.
