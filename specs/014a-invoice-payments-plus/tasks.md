# Tasks: Invoice Payments Plus

**Input**: Design documents from `specs/014a-invoice-payments-plus/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [ ] T001 [P] Add `PaymentConnectionResponse`, `PaymentLinkResponse`, `PaymentStatusResponse`
  response DTOs and `CompletePaymentConnectionRequest`
  (`{ authorizationCode }`) request DTO per contracts/payments-api.md in
  `backend/ChildCare.Contracts/Responses/PaymentResponses.cs` and
  `backend/ChildCare.Contracts/Requests/PaymentRequests.cs`
- [ ] T002 [P] Add `UpdateLocationPaymentReminderSettingsRequest`
  (`{ enabled, delayDays, cadenceDays }`) and extend `LocationResponse` with
  `paymentRemindersEnabled`/`paymentReminderDelayDays`/`paymentReminderCadenceDays` in
  `backend/ChildCare.Contracts/Requests/LocationRequests.cs` and
  `backend/ChildCare.Contracts/Responses/LocationResponse.cs`
- [ ] T003 [P] Add director-web `settings.paymentConnection.*` i18n keys (not-connected,
  connect action, connecting, connected + account label, disconnect, OAuth-failure retry) to
  `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [ ] T004 [P] Add director-web `locations.paymentReminderSettings.*` i18n keys (enabled toggle,
  delay days, cadence days, save action) to the same three locale files
- [ ] T005 [P] Add parent-mobile `payments.*` i18n keys (pay now action, confirming-payment
  state, payment failed/cancelled, offline-disabled state, betalingsbewijs view/download,
  receipt not-yet-available) to `parent-mobile/i18n/locales/en.json`,
  `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The public-schema entities, tenant-schema extensions, and `IPaymentProvider`
abstraction every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Add `PaymentProviderConnection` entity + `PaymentConnectionStatus` enum
  (`Connected`/`Disconnected`) in
  `backend/ChildCare.Domain/Entities/PaymentProviderConnection.cs` and
  `backend/ChildCare.Domain/Enums/PaymentConnectionStatus.cs` per data-model.md
- [ ] T007 Add `Payment` entity + `PaymentStatus` enum
  (`Open`/`Paid`/`Failed`/`Cancelled`/`Expired`) in
  `backend/ChildCare.Domain/Entities/Payment.cs` and
  `backend/ChildCare.Domain/Enums/PaymentStatus.cs` per data-model.md
- [ ] T008 [P] Add `ReminderCount` (default 0) and `LastReminderSentAt` (nullable) to
  `backend/ChildCare.Domain/Entities/Invoice.cs`
- [ ] T009 [P] Add `PaymentRemindersEnabled` (default false), `PaymentReminderDelayDays`
  (default 3), `PaymentReminderCadenceDays` (default 7) to
  `backend/ChildCare.Domain/Entities/Location.cs`
- [ ] T010 Configure `PaymentProviderConnection` (unique index on `TenantId`) and `Payment`
  (unique index on `PaymentReference`; non-unique index on `(TenantId, InvoiceId, Status)`) in
  `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs` (depends on T006, T007)
- [ ] T011 Configure the new `Invoice`/`Location` columns in
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T008, T009)
- [ ] T012 Add public migration `AddPaymentProviderConnectionsAndPayments` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/` (depends on T010)
- [ ] T013 Add tenant migration `AddInvoiceRemindersAndLocationPaymentSettings` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T011)
- [ ] T014 [P] Define `IPaymentProvider` (research.md R1's five methods) and its result records
  (`ConnectAccountResult`, `CreatePaymentResult`, `PaymentStatusResult`) in
  `backend/ChildCare.Application/Common/IPaymentProvider.cs`
- [ ] T015 Implement `MolliePaymentProvider` (`HttpClient`-based adapter — OAuth authorization
  URL, OAuth token exchange, create payment, get payment status; no Mollie SDK dependency) in
  `backend/ChildCare.Infrastructure/Payments/MolliePaymentProvider.cs` (depends on T014)
- [ ] T016 Implement token encryption/decryption via `IDataProtector` (purpose string
  `"PaymentProviderConnection.MollieTokens"`, research.md R3) as a small helper used by
  `MolliePaymentProvider`/connection commands, in
  `backend/ChildCare.Infrastructure/Payments/PaymentTokenProtector.cs`
- [ ] T017 Register `IPaymentProvider`→`MolliePaymentProvider`, its `HttpClient`, and Mollie
  OAuth client ID/secret (from configuration/Secret Manager per Constitution VI, never
  hardcoded) in `backend/ChildCare.Api/Program.cs`
- [ ] T018 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new `Invoice`/
  `Location` columns (the recurring pattern every migration-adding feature since 003 has needed)
  in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T013)

**Checkpoint**: Entities, `IPaymentProvider` abstraction, and Mollie adapter ready — user story
implementation can now begin.

---

## Phase 3: User Story 1 - Parent pays an invoice online (Priority: P1) 🎯 MVP

**Goal**: A parent can pay a `Sent` invoice via a Mollie-hosted checkout link; a webhook
confirms payment and drives the existing `Sent → Paid` transition, idempotently and only for the
correct tenant/invoice.

**Independent Test**: Seed a `PaymentProviderConnection` row directly (`Connected`, test-mode
credentials) and a `Sent` invoice; call `POST /api/invoices/{id}/payment-link`, simulate Mollie
confirming the payment, deliver the webhook, and confirm the invoice becomes `Paid` exactly
once — independent of the OAuth connect UI (Story 2) and reminders/receipts (Stories 3/4).

### Tests for User Story 1

- [ ] T019 [P] [US1] Integration test: `CreatePaymentLinkCommand` on a `Sent` invoice with a
  `Connected` payment provider creates a `Payment` row (`Open`) and returns a checkout URL, in
  `backend/ChildCare.Api.Tests/Payments/CreatePaymentLinkCommandTests.cs`
- [ ] T020 [P] [US1] Integration test: calling `CreatePaymentLinkCommand` again while an `Open`
  `Payment` already exists for that invoice reuses the same row/checkout URL rather than
  creating a second one (research.md R6, 2026-07-16 clarification), in the same test file
- [ ] T021 [P] [US1] Integration test: `CreatePaymentLinkCommand` on an invoice belonging to an
  organisation with no `Connected` provider fails with `errors.paymentConnection.not_connected`,
  in the same test file
- [ ] T022 [P] [US1] Integration test: `CreatePaymentLinkCommand` on a `Draft` or `Paid` invoice
  fails with `errors.invoice.not_sent`, in the same test file
- [ ] T023 [P] [US1] Integration test: `ProcessPaymentWebhookCommand` with a valid
  `paymentReference`, where Mollie confirms `paid`, transitions the invoice `Sent → Paid`, sets
  `PaidAt`, and updates the `Payment` row to `Paid`, in
  `backend/ChildCare.Api.Tests/Payments/ProcessPaymentWebhookCommandTests.cs`
- [ ] T024 [P] [US1] Integration test: delivering the identical webhook twice for an
  already-`Paid` invoice is a no-op the second time — no duplicate transition, no exception, in
  the same test file
- [ ] T025 [P] [US1] Integration test: a webhook call with a `paymentReference` that doesn't
  resolve to any `Payment` row changes no invoice anywhere and does not reveal why, in the same
  test file
- [ ] T026 [P] [US1] Integration test: `ProcessPaymentWebhookCommand` never trusts a
  tenant/invoice identifier from the webhook payload itself — construct a payload claiming a
  different tenant's invoice ID and confirm only the `PaymentReference`-resolved invoice is ever
  touched, in the same test file (Constitution I regression guard)
- [ ] T027 [P] [US1] Integration test: `GetPaymentStatusQuery` reflects `Open` before webhook
  processing and `Paid` after, for the "confirming payment" polling flow (FR-010), in
  `backend/ChildCare.Api.Tests/Payments/GetPaymentStatusQueryTests.cs`
- [ ] T027a [P] [US1] Integration test: after `ProcessPaymentWebhookCommand` records a PSP fee
  on the `Payment` row, `Invoice.TotalCents` is byte-for-byte unchanged from before the payment
  (FR-011), in `backend/ChildCare.Api.Tests/Payments/ProcessPaymentWebhookCommandTests.cs`

### Implementation for User Story 1

- [ ] T028 [US1] Implement `CreatePaymentLinkCommand` (open-payment reuse per research.md R6;
  calls `IPaymentProvider.CreatePaymentAsync` with a new `PaymentReference`) in
  `backend/ChildCare.Application/Payments/CreatePaymentLinkCommand.cs` (depends on T014, T007)
- [ ] T029 [US1] Implement `ProcessPaymentWebhookCommand` (resolves `PaymentReference` →
  `TenantId`/`InvoiceId` from `PublicDbContext.Payments`, switches tenant context, calls
  `IPaymentProvider.GetPaymentStatusAsync` to verify — never trusts the payload per FR-007,
  applies the `Sent → Paid` transition, idempotent per `Payment.Status` guard) in
  `backend/ChildCare.Application/Payments/ProcessPaymentWebhookCommand.cs` (depends on T028)
- [ ] T030 [US1] Implement `GetPaymentStatusQuery` in
  `backend/ChildCare.Application/Payments/GetPaymentStatusQuery.cs`
- [ ] T031 [US1] Wire `POST /api/invoices/{id}/payment-link` (tenant-scoped, parent
  authorization), `GET /api/invoices/{id}/payment-status` (tenant-scoped, parent authorization),
  and `POST /api/webhooks/mollie/{paymentReference}` (`TenantExempt`, per
  contracts/payments-api.md) in `backend/ChildCare.Api/Endpoints/PaymentEndpoints.cs` (depends
  on T028, T029, T030)
- [ ] T032 [US1] Regenerate and commit `parent-mobile/services/generated/api-types.ts` against
  the new endpoints
- [ ] T033 [US1] Create `parent-mobile/services/payments.ts` (create payment link, poll payment
  status, mirrors `services/invoices.ts`'s existing fetch pattern)
- [ ] T034 [US1] Add "Pay now" to the invoice detail screen in
  `parent-mobile/app/(app)/invoices/[id].tsx` — only rendered when the invoice is `Sent` (the
  client checks connection status via the organisation's public invoice-detail response, with
  FR-005's server-side guard as the real enforcement); redirects to the returned checkout URL;
  on return, polls `payment-status` and shows the "confirming payment" state (FR-010) until
  resolved; disabled with the standard offline banner when offline (per design-system.md)
  (depends on T033)
- [ ] T035 [P] [US1] Parent-mobile component test: "Pay now" is shown only on a `Sent` invoice
  and hidden on `Draft`/`Paid`, in `parent-mobile/__tests__/invoiceDetail.test.tsx`
- [ ] T036 [P] [US1] Parent-mobile component test: tapping "Pay now" and returning shows the
  "confirming payment" state, resolving to "Paid" once `payment-status` reports `paid`, in the
  same test file

**Checkpoint**: Online payment works end to end for a pre-connected organisation — User Story 1
is independently demonstrable.

---

## Phase 4: User Story 2 - Director connects the organisation's Mollie account (Priority: P1)

**Goal**: A director can connect, view the status of, disconnect, and reconnect the
organisation's Mollie account via OAuth from director web.

**Independent Test**: As director, complete the OAuth flow (test-mode), confirm connected status
persists across a reload, disconnect, and reconnect — independent of any invoice payment.

### Tests for User Story 2

- [ ] T037 [P] [US2] Integration test: `ConnectPaymentProviderCommand` returns a Mollie
  authorization URL scoped to the requesting tenant, in
  `backend/ChildCare.Api.Tests/Payments/ConnectPaymentProviderCommandTests.cs`
- [ ] T038 [P] [US2] Integration test: `CompletePaymentProviderOAuthCommand` with a valid
  authorization code creates/updates a `Connected` `PaymentProviderConnection` row with
  encrypted tokens (assert the stored value is not the plaintext token), in
  `backend/ChildCare.Api.Tests/Payments/CompletePaymentProviderOAuthCommandTests.cs`
- [ ] T039 [P] [US2] Integration test: `CompletePaymentProviderOAuthCommand` with a failing/
  rejected code returns a clear failure without leaving a false-`Connected` row, in the same
  test file
- [ ] T040 [P] [US2] Integration test: `DisconnectPaymentProviderCommand` sets `Disconnected`
  and a subsequent `CreatePaymentLinkCommand` for that tenant fails with
  `errors.paymentConnection.not_connected` (regression link to US1 T021), in
  `backend/ChildCare.Api.Tests/Payments/DisconnectPaymentProviderCommandTests.cs`
- [ ] T041 [P] [US2] Integration test: reconnecting after disconnect updates the same row back
  to `Connected` (not a duplicate row) — the "reconnect" edge case, in the same test file
- [ ] T042 [P] [US2] Integration test: `GetPaymentConnectionStatusQuery` never returns
  `EncryptedAccessToken`/`EncryptedRefreshToken` in its response shape, in
  `backend/ChildCare.Api.Tests/Payments/GetPaymentConnectionStatusQueryTests.cs`

### Implementation for User Story 2

- [ ] T043 [US2] Implement `ConnectPaymentProviderCommand`,
  `CompletePaymentProviderOAuthCommand`, `DisconnectPaymentProviderCommand`, and
  `GetPaymentConnectionStatusQuery` in `backend/ChildCare.Application/Payments/` (depends on
  T014, T015, T016, T006)
- [ ] T044 [US2] Wire `POST /api/organisations/me/payment-connection/authorize`,
  `POST /api/organisations/me/payment-connection/callback`,
  `DELETE /api/organisations/me/payment-connection`, and
  `GET /api/organisations/me/payment-connection` (`DirectorOnly`) in
  `backend/ChildCare.Api/Endpoints/PaymentEndpoints.cs` (depends on T043)
- [ ] T045 [US2] Regenerate and commit `web/lib/generated/api-types.ts` against the new
  endpoints
- [ ] T046 [US2] Add a payment-connection section to `web/app/(app)/settings/page.tsx`
  (not-connected/connecting/connected/error states per spec.md, redirect-based OAuth flow,
  disconnect action) (depends on T045)
- [ ] T047 [P] [US2] Web component test: the settings page shows "not connected" by default and
  a "Connect Mollie" action, in `web/__tests__/settings.test.tsx` (or this repo's existing test
  file for that page)
- [ ] T048 [P] [US2] Web component test: a connected state shows the linked account label and a
  disconnect action; disconnecting reverts to "not connected", in the same test file

**Checkpoint**: Directors can self-serve connect/disconnect Mollie — combined with User Story 1,
the full parent-payment loop now works without any pre-seeded test data.

---

## Phase 5: User Story 3 - Parent receives an automatic payment reminder (Priority: P2)

**Goal**: A recurring process sends up to 3 cadence-respecting reminders for `Sent`+overdue
invoices at locations that opt in, with zero director action per invoice.

**Independent Test**: Seed a `Sent` invoice past its due date at a location with reminders
enabled; run the reminder CLI command; confirm exactly one reminder is sent, a second same-day
run doesn't duplicate it, and it stops entirely once the invoice is paid or the 3-reminder cap
is reached — independent of the payment/OAuth flows.

### Tests for User Story 3

- [ ] T049 [P] [US3] Integration test: `UpdateLocationPaymentReminderSettingsCommand` persists
  `enabled`/`delayDays`/`cadenceDays`; a location that never calls it keeps the defaults
  (disabled, 3, 7), in
  `backend/ChildCare.Api.Tests/Locations/UpdateLocationPaymentReminderSettingsCommandTests.cs`
- [ ] T050 [P] [US3] Integration test: the reminder command sends a reminder for a `Sent`
  invoice past `DueDate + delayDays` at an enabled location, and increments
  `ReminderCount`/sets `LastReminderSentAt`, in
  `backend/ChildCare.Api.Tests/Payments/SendPaymentRemindersCommandTests.cs`
- [ ] T051 [P] [US3] Integration test: running the command twice the same day does not send a
  second reminder for the same invoice, in the same test file
- [ ] T052 [P] [US3] Integration test: an invoice that already has `ReminderCount == 3` receives
  no further reminder, in the same test file
- [ ] T053 [P] [US3] Integration test: an invoice at a location with reminders disabled receives
  no reminder, in the same test file
- [ ] T054 [P] [US3] Integration test: an invoice that transitions to `Paid` (either path)
  receives no further reminder on the next run, in the same test file
- [ ] T055 [P] [US3] Integration test: the reminder command iterates every tenant schema (mirrors
  `MigrateTenantsCommand`'s existing cross-tenant test pattern) and reminder notifications use
  the `Notification` + best-effort push pattern (mirrors `InvoiceNotificationService`'s existing
  test), in the same test file
- [ ] T055a [P] [US3] Integration test: when processing one tenant schema throws (simulated
  failure), the reminder command still processes every other tenant schema and completes rather
  than aborting the whole run (spec.md Technical Requirements' per-tenant failure isolation), in
  the same test file

### Implementation for User Story 3

- [ ] T056 [US3] Implement `UpdateLocationPaymentReminderSettingsCommand` (mirrors
  `UpdateLocationInvoiceSettingsCommand`, 014) in
  `backend/ChildCare.Application/Locations/UpdateLocationPaymentReminderSettingsCommand.cs`
  (depends on T009)
- [ ] T057 [US3] Wire `PUT /api/locations/{locationId}/payment-reminder-settings` in
  `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` (depends on T056)
- [ ] T058 [US3] Implement `PaymentReminderNotificationService` (mirrors
  `InvoiceNotificationService`'s `Notification` row + best-effort push pattern, dedicated i18n
  copy per FR-014) in
  `backend/ChildCare.Application/Payments/PaymentReminderNotificationService.cs`
- [ ] T059 [US3] Implement `SendPaymentRemindersCommand` (cross-tenant iteration mirrors
  `BackfillGrowthCheckCommand`'s per-tenant-schema loop; cadence/cap logic per data-model.md) in
  `backend/ChildCare.Application/Payments/SendPaymentRemindersCommand.cs` (depends on T058)
- [ ] T060 [US3] Add the `send-payment-reminders` CLI entrypoint in
  `backend/ChildCare.Api/Cli/SendPaymentRemindersCommand.cs` (mirrors
  `MigrateTenantsCommand.cs`'s CLI wiring) (depends on T059)
- [ ] T061 [US3] Author the Cloud Scheduler + Cloud Run Job Terraform config (daily trigger of
  the `send-payment-reminders` CLI entrypoint, research.md R4) in
  `infra/gcp/payment-reminders-scheduler.tf` — **note**: authoring only; applying this to the
  real GCP project is a manual post-merge step, same convention as this codebase's other
  infra/production changes, not run autonomously by this pipeline
- [ ] T062 [US3] Add `PaymentReminderSettingsForm` fields to the existing "Invoicing" tab on
  `web/app/(app)/locations/[id]/page.tsx` / `web/components/InvoiceSettingsForm.tsx` (enabled
  toggle, delay days, cadence days) (depends on T045, T057)
- [ ] T063 [P] [US3] Web component test: the reminder-settings fields load/save on the location's
  Invoicing tab, in the existing `InvoiceSettingsForm` test file

**Checkpoint**: Reminders run automatically per location opt-in — User Story 3 is independently
demonstrable via the CLI command, without waiting on real Cloud Scheduler wiring.

---

## Phase 6: User Story 4 - Parent receives an automatic payment receipt (Priority: P2)

**Goal**: A betalingsbewijs is available to view/download the moment an invoice becomes `Paid`,
via either the online-payment webhook (US1) or 014's existing manual mark-paid action.

**Independent Test**: Mark an invoice paid via either path and confirm a receipt is generated
and reachable by the parent, independent of how the invoice became paid.

### Tests for User Story 4

- [ ] T064 [P] [US4] Integration test: `GenerateBetalingsbewijsQuery` on a `Paid` invoice returns
  a PDF containing KDV identity, child/parent name, invoice reference, amount paid, and date
  paid, in `backend/ChildCare.Api.Tests/Payments/GenerateBetalingsbewijsQueryTests.cs`
- [ ] T065 [P] [US4] Integration test: `GenerateBetalingsbewijsQuery` on a `Draft`/`Sent`
  (not-yet-paid) invoice, or an invoice belonging to a different parent, both return the
  identical not-found outcome (enumeration-resistance, mirrors
  `GenerateParentInvoicePdfQuery`'s existing precedent), in the same test file
- [ ] T066 [P] [US4] Integration test: querying the receipt twice for the same paid invoice
  returns byte-identical PDFs (FR-016's determinism requirement), in the same test file
- [ ] T067 [P] [US4] Integration test: an invoice paid via 014's existing manual mark-paid
  command triggers the same receipt notification as the webhook path (FR-015 applies to both),
  in `backend/ChildCare.Api.Tests/Invoices/MarkInvoicePaidCommandTests.cs` (extends the existing
  test file)

### Implementation for User Story 4

- [ ] T068 [US4] Implement `QuestPdfBetalingsbewijsGenerator`/`IBetalingsbewijsGenerator`
  (mirrors `QuestPdfInvoiceGenerator`'s per-locale `Labels` dictionary pattern, on-demand
  rendering per research.md R5) in
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfBetalingsbewijsGenerator.cs`
- [ ] T069 [US4] Implement `GenerateBetalingsbewijsQuery` (mirrors `GenerateInvoicePdfQuery`'s
  on-demand-render, not-found posture) in
  `backend/ChildCare.Application/Payments/GenerateBetalingsbewijsQuery.cs` (depends on T068)
- [ ] T070 [US4] Extend `PaymentReminderNotificationService`'s sibling (or add a small
  `PaymentReceiptNotificationService`, mirroring `InvoiceNotificationService`'s exact shape) to
  notify linked contacts when an invoice transitions to `Paid`, and invoke it from both
  `ProcessPaymentWebhookCommand` (US1) and 014's existing `MarkInvoicePaidCommand` in
  `backend/ChildCare.Application/Payments/PaymentReceiptNotificationService.cs` and
  `backend/ChildCare.Application/Invoices/MarkInvoicePaidCommand.cs` (depends on T069)
- [ ] T071 [US4] Wire `GET /api/invoices/{id}/betalingsbewijs` (tenant-scoped, parent
  authorization) in `backend/ChildCare.Api/Endpoints/PaymentEndpoints.cs` (depends on T069)
- [ ] T072 [US4] Regenerate and commit `parent-mobile/lib/generated/api-types.ts` against the
  new endpoint
- [ ] T073 [US4] Add a "View receipt" / download action to
  `parent-mobile/app/(app)/invoices/[id].tsx`, shown only once the invoice is `Paid` (depends on
  T072)
- [ ] T074 [P] [US4] Parent-mobile component test: the receipt action appears only on a `Paid`
  invoice and downloads successfully, in `parent-mobile/__tests__/invoiceDetail.test.tsx`

**Checkpoint**: All four user stories complete — the full 014a scope (pay, connect, remind,
receipt) is demonstrable end to end.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story validation, accessibility, and the explicit no-regression guarantee for
014.

- [ ] T075 [P] Accessibility pass: payment-connection settings section, reminder-settings fields,
  and the parent-mobile "Pay now"/receipt actions meet this codebase's existing accessibility
  baseline (touch targets, focus rings — design-system.md/platform-rules.md)
- [ ] T076 Run `quickstart.md`'s five scenarios manually/via integration tests and confirm each
  passes
- [ ] T077 Confirm SC-005 explicitly: run 014's own existing test suite unmodified and confirm
  it still passes in full — an organisation that never connects Mollie must see zero behavior
  change to generation/send/manual-mark-paid
- [ ] T078 Confirm FR-021 explicitly: diff 014's billable-day computation
  (`BillableDayCalculator`), PDF content (`QuestPdfInvoiceGenerator`), and
  `MarkInvoicePaidCommand`'s core transition logic against this branch — none of the three may
  have any behavioral change beyond the new receipt-notification call added in T070

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup only for T003-T005's i18n keys being available to
  reference; T006-T018 have no Setup dependency and can start immediately. BLOCKS all user
  stories.
- **User Stories (Phase 3-6)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3/US4 — its tests pre-seed a `PaymentProviderConnection`
    row directly rather than going through the OAuth UI (per its own Independent Test).
  - US2 (P1) has no dependency on US1 — connect/disconnect is independently testable. Combined
    with US1, it completes the P1 slice (a director can self-serve connect, then a parent can
    pay, with no test-only seeding required).
  - US3 (P2) depends on a `Sent`/overdue invoice existing (014) but not on US1/US2's payment
    machinery — reminders and online payment are independent paths to the same invoice.
  - US4 (P2) depends on US1's webhook transition and 014's existing manual mark-paid command
    both being able to reach `Paid` — its own query/UI work is otherwise independent.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- T001-T005 (Setup) can all run in parallel.
- T008 and T009 (Foundational entity extensions) can run in parallel.
- T019-T027 (US1 tests) can run in parallel.
- T035, T036 (US1 mobile tests) can run in parallel once T034 lands.
- T037-T042 (US2 tests) can run in parallel.
- T047, T048 (US2 web tests) can run in parallel once T046 lands.
- T049-T055 (US3 tests) can run in parallel.
- T064-T067 (US4 tests) can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1 (with a directly-seeded connection row for testing)
4. **STOP and VALIDATE**: run quickstart.md Scenario 2's steps 2–5 against seeded test data
5. Demo if ready — online payment works technically, even before directors can self-serve
   connect

### Incremental Delivery

1. Setup + Foundational → schema, `IPaymentProvider`, Mollie adapter ready
2. Add User Story 1 → validate independently (seeded connection) → demo (payment mechanics work)
3. Add User Story 2 → validate independently (Scenario 1 in full) → demo (directors can
   self-serve connect — combined with US1, the full parent-payment loop is real)
4. Add User Story 3 → validate independently (Scenario 4) → demo (reminders run automatically)
5. Add User Story 4 → validate independently (Scenario 2's receipt step + Scenario 5's manual
   path) → demo (receipts complete on both payment paths)
6. Polish (Phase 7) → run all five `quickstart.md` scenarios end to end, including the explicit
   014 no-regression check (T077/T078)

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T061's Terraform authoring is code, but its `apply` is explicitly a manual post-merge step —
  no CI/pipeline step in this feature applies infrastructure changes to the real GCP project
  autonomously, consistent with this codebase's existing convention for production/infra
  changes (e.g. 013h's T049 precedent).
- T077/T078 exist specifically because FR-021/SC-005's "zero behavior change to 014" guarantee
  is the highest-risk regression surface in this feature — only running the *existing* 014 test
  suite unmodified, plus an explicit diff check, actually proves it held.
- Commit after each task or logical group, per this repo's standing convention.
