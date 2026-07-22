# Tasks: SEPA Direct Debit Batch Collection

**Input**: Design documents from `/specs/026-sepa-direct-debit/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/sepa-direct-debit-api.md, quickstart.md

**Tests**: Included — Constitution V requires TestContainers-backed integration/API tests for
happy path plus key negative/regulatory flows, and every prior shipped feature in this codebase
follows that convention.

**Organization**: Tasks are grouped by user story (spec.md's US1/US2/US3/US4, priority order).

## Path Conventions

Web application per plan.md's Project Structure: `backend/ChildCare.{Domain,Application,
Infrastructure,Contracts,Api}/`, `backend/ChildCare.Api.Tests/`, `web/`.

---

## Phase 1: Setup

- [ ] T001 Add the embedded-resource declaration for the official pain.008.001.02 XSD
      (research.md R2, already committed at
      `backend/ChildCare.Infrastructure/Sepa/Schemas/pain.008.001.02.xsd`) —
      `<EmbeddedResource Include="Sepa\Schemas\*.xsd" />` in
      `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj`, mirroring the existing
      `Email\Templates\*.scriban` pattern.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model, XML generation/validation abstraction, and the FRST/RCUR resolver every
user story depends on.

- [ ] T002 [P] Add `PendingDebit` to `InvoiceStatus` (between `Sent` and `Paid`) in
      `backend/ChildCare.Domain/Enums/InvoiceStatus.cs` (data-model.md).
- [ ] T003 [P] Add `SepaBatchId` (`Guid?`), `SepaMandateReferenceUsed` (`string?`, immutable
      snapshot — never cleared by a return, data-model.md), and `SepaReturnReason` (`string?`) to
      `Invoice` in `backend/ChildCare.Domain/Entities/Invoice.cs`.
- [ ] T004 [P] Add `SepaRevokedAt` (`DateTime?`) to `Contract` in
      `backend/ChildCare.Domain/Entities/Contract.cs`, next to the existing `SepaAuthorisedAt`
      field.
- [ ] T005 [P] Create `SepaBatch` entity in `backend/ChildCare.Domain/Entities/SepaBatch.cs`
      (data-model.md: `LocationId`, `ExecutionDate`, `GeneratedByUserId`, `GeneratedAt`,
      `TotalCents`, `InvoiceCount`).
- [ ] T006 Register `SepaBatches` `DbSet` and entity configuration in
      `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`, and generate the EF Core
      migration (`dotnet ef migrations add AddSepaDirectDebit --context TenantDbContext`) in
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`. Extend
      `TenantMigrationRolloutTests`'/`LegacyVaccinationMigrationTests`' revert-helper for the new
      table and the four new columns, per the pattern every migration-adding feature since 012a
      has needed (see BACKLOG.md's shipped-notes for 012a/013c/006a/013d/013g/013h/014/014a/015/
      025 — check FK drop order against the tables they reference, the exact mistake 013g's
      shipped-note flagged) (depends on T002-T005).
- [ ] T007 [P] Define `ISepaBatchXmlGenerator` abstraction in
      `backend/ChildCare.Application/Common/ISepaBatchXmlGenerator.cs` — a method taking the
      batch's creditor headers (identifier, name, IBAN) plus a list of per-invoice debit-
      instruction inputs (amount, decrypted debtor IBAN, debtor name, mandate reference, mandate
      signing date, sequence type, end-to-end ID) and returning schema-validated XML bytes, so
      `Application` never references `System.Xml.Schema` directly (mirrors `IInvoicePdfGenerator`
      wrapping QuestPDF).
- [ ] T008 [P] Implement `SepaBatchXmlGenerator : ISepaBatchXmlGenerator` in
      `backend/ChildCare.Infrastructure/Sepa/SepaBatchXmlGenerator.cs`: builds the
      `pain.008.001.02` `Document` tree via `System.Xml.Linq` (research.md R1 — `GrpHdr` with
      `NbOfTxs`/`CtrlSum` computed from the actual instruction list — never a caller-supplied
      value that could drift from it, per FR-006's control-total requirement — one `PmtInf` per
      batch with the creditor headers, one `DrctDbtTxInf` per instruction), then validates the
      result against the embedded XSD (`GetManifestResourceStream`, cached `XmlSchemaSet`,
      research.md R2) before returning it — a validation failure throws a typed exception the
      Application layer maps to `500 errors.sepa_batch.generation_failed` (Principle VI, never a
      raw `XmlSchemaException` to the client) (depends on T001).
- [ ] T009 [P] Unit tests for `SepaBatchXmlGenerator` in
      `backend/ChildCare.Api.Tests/SepaBatches/SepaBatchXmlGeneratorTests.cs`: a valid input set
      produces XML that passes schema validation with the expected element values (amount,
      IBAN, mandate reference, `SeqTp`, `EndToEndId`) and whose `NbOfTxs`/`CtrlSum` match the
      input instruction count/sum exactly (FR-006); an input that would produce schema-invalid
      XML (e.g., an over-length field) throws the typed exception rather than returning an
      invalid document (depends on T008).
- [ ] T010 [P] Implement `SepaSequenceTypeResolver` (FR-002a / research.md R3) in
      `backend/ChildCare.Application/SepaBatches/SepaSequenceTypeResolver.cs`: given a contract's
      current `SepaMandateReference`, queries whether any `Invoice` for that contract has
      `SepaMandateReferenceUsed` equal to it (the immutable snapshot column, data-model.md — not
      the live, clearable `SepaBatchId`, so a returned debit still correctly counts as prior use
      and a revoke-and-resign's new reference correctly does not match any old invoice) — returns
      `FRST` if none found, `RCUR` otherwise; unit-testable independent of the MediatR handler and
      EF Core.
- [ ] T011 [P] Unit tests for `SepaSequenceTypeResolver` in
      `backend/ChildCare.Api.Tests/SepaBatches/SepaSequenceTypeResolverTests.cs`: no prior batch
      under the current reference → `FRST`; a prior batch under the current reference → `RCUR`;
      a prior batch under a since-revoked-and-replaced reference → `FRST` again (depends on T010).

**Checkpoint**: Entities, migration, XML generator, and sequence-type resolver exist and are
unit-tested — user story implementation can begin.

---

## Phase 3: User Story 1 - Generate and download a SEPA batch (Priority: P1) 🎯 MVP

**Goal**: A director reviews eligible/excluded invoices for a location/month and generates a
schema-valid pain.008 batch.

**Independent Test**: Generate a batch for a location with a mix of eligible/ineligible invoices
and a valid execution date (quickstart.md Scenario 1) — the downloaded file is schema-valid with
one instruction per eligible invoice, and those invoices become `PendingDebit`.

### Tests for User Story 1

- [ ] T012 [P] [US1] API test for `GET /api/locations/{locationId}/sepa-batch-eligibility` in
      `backend/ChildCare.Api.Tests/SepaBatches/SepaBatchEligibilityTests.cs`: a signed/non-revoked
      mandate → eligible; no mandate → excluded `NoMandate`; revoked mandate → excluded
      `MandateRevoked`; a location missing its creditor identifier or bank account →
      `creditorConfigured: false` (FR-001, FR-004, data-model.md's priority order).
- [ ] T013 [P] [US1] API test for `POST /api/locations/{locationId}/sepa-batches` happy path in
      `backend/ChildCare.Api.Tests/SepaBatches/GenerateSepaBatchTests.cs`: generates a
      schema-valid pain.008 file (validated in the test against the same embedded XSD), one
      `DrctDbtTxInf` per selected invoice with the correct amount/IBAN/mandate
      reference/signing date/`SeqTp`/`EndToEndId`, all selected invoices become `PendingDebit`
      with `SepaBatchId` set, and a `SepaBatch` row is persisted (FR-002, FR-002a, FR-003, FR-007).
- [ ] T014 [P] [US1] API test: generation against a location missing its creditor identifier or
      bank account returns `422 errors.sepa_batch.creditor_not_configured` with no invoice status
      change (FR-004), in `GenerateSepaBatchTests.cs`.
- [ ] T015 [P] [US1] API test: an execution date less than one business day out (including a
      same-day and a next-day-is-Saturday case) returns `422
      errors.sepa_batch.execution_date_too_soon` with no invoice status change (FR-005), in
      `GenerateSepaBatchTests.cs`.
- [ ] T016 [P] [US1] API test: submitting an invoice ID that is no longer eligible (already
      `PendingDebit` from a concurrent/prior batch) returns `422
      errors.sepa_batch.invoice_not_eligible` and changes nothing (FR-013), in
      `GenerateSepaBatchTests.cs`.
- [ ] T016a [P] [US1] API test: two concurrent `POST .../sepa-batches` requests both including the
      same invoice — exactly one succeeds and includes it; the other either fails outright
      (`errors.sepa_batch.invoice_not_eligible`) or succeeds without that invoice, but the invoice
      is never claimed by both resulting batches (FR-013/CHK003, data-model.md's Concurrency-safe
      eligibility claim), in `GenerateSepaBatchTests.cs`.
- [ ] T016b [P] [US1] API test: a contract whose `SepaIbanEncrypted` cannot be decrypted (a
      corrupted/invalid ciphertext fixture) fails the entire batch generation with no invoice
      status changes and no `SepaBatch` row persisted, even when other selected invoices in the
      same request are otherwise valid (FR-006a), in `GenerateSepaBatchTests.cs`.
- [ ] T017 [P] [US1] API test: an empty `invoiceIds` selection returns `422
      errors.sepa_batch.no_invoices_selected`, in `GenerateSepaBatchTests.cs`.
- [ ] T018 [P] [US1] API test for `GET /api/locations/{locationId}/sepa-batches` in
      `backend/ChildCare.Api.Tests/SepaBatches/ListSepaBatchesTests.cs`: a just-generated batch
      appears with its execution date, invoice count, and total amount (FR-008).

### Implementation for User Story 1

- [ ] T019 [US1] `SepaBatchEligibilityResponse`/`SepaBatchResponse` in
      `backend/ChildCare.Contracts/Responses/` per contracts/sepa-direct-debit-api.md.
- [ ] T020 [US1] `GetSepaBatchEligibilityQuery`/Handler in
      `backend/ChildCare.Application/SepaBatches/GetSepaBatchEligibilityQuery.cs` implementing
      data-model.md's eligibility rule and exclusion-reason priority order (depends on T019).
- [ ] T021 [US1] `GenerateSepaBatchCommand`/Handler in
      `backend/ChildCare.Application/SepaBatches/GenerateSepaBatchCommand.cs`: re-validates
      eligibility and execution date server-side (never trusts the client's prior read),
      resolves creditor headers from `Tenant.SepaCreditorIdentifier`/`Tenant.Name`/
      `Location.BankAccountNumber` (research.md R5); decrypts each debtor IBAN via
      `IIbanProtector` (logging the access per FR-014/research.md R4) — if any decryption fails,
      aborts the whole command before any persistence (FR-006a, no partial batch); resolves the
      debtor name via the primary-contact join (research.md R6); resolves `SeqTp` via
      `SepaSequenceTypeResolver` (T010); calls `ISepaBatchXmlGenerator` (T008); and — only on
      successful generation — claims each invoice via a conditional update (`Status = 'Sent' →
      'PendingDebit'` guarded by the current status in the same statement, not read-then-write;
      data-model.md's Concurrency-safe eligibility claim, FR-013/CHK003) and persists the
      `SepaBatch` row, all in one database transaction (FR-007's all-or-nothing guarantee) — a
      losing claim on any invoice (lost a concurrent race) fails the whole command with FR-002's
      invoice-not-eligible error rather than generating a batch for a partial set (depends on T005,
      T008, T010, T019, T020).
- [ ] T022 [US1] `ListSepaBatchesQuery`/Handler in
      `backend/ChildCare.Application/SepaBatches/ListSepaBatchesQuery.cs` (depends on T019).
- [ ] T023 [US1] `SepaBatchEndpoints.cs` in `backend/ChildCare.Api/Endpoints/` —
      `MapSepaBatchEndpoints`, `DirectorOnly` group: `GET .../sepa-batch-eligibility`,
      `POST .../sepa-batches` (returns the XML as `application/xml` attachment on success, maps
      `GenerateSepaBatchCommand`'s typed failures to their documented status codes), `GET
      .../sepa-batches` (depends on T020-T022).
- [ ] T024 [US1] Register `MapSepaBatchEndpoints` and `ISepaBatchXmlGenerator →
      SepaBatchXmlGenerator` DI registration in `backend/ChildCare.Api/Program.cs`.
- [ ] T025 [P] [US1] i18n keys (NL/FR/EN) for the eligibility/exclusion-reason labels, batch
      screen copy, and `errors.sepa_batch.*` messages in `web/messages/{nl,fr,en}.json` (FR-015).
- [ ] T026 [US1] SEPA Batches screen in `web/app/(app)/invoices/sepa-batches/page.tsx`: location +
      month picker, eligible/excluded invoice list with per-row exclusion reason, execution-date
      picker (client-side minimum-date validation per spec.md UX Requirements), Generate action
      triggering the file download, loading/empty/error states (no eligible invoices; creditor
      not configured; generation failure), using the regenerated openapi-fetch client (depends on
      T023).

**Checkpoint**: A director can review eligibility and generate/download a batch — the feature's
core value, independently testable and demoable.

---

## Phase 4: User Story 2 - Collection confirmed automatically via bank statement (Priority: P2)

**Goal**: A `PendingDebit` invoice reaches `Paid` through feature 025's existing CODA import,
with no SEPA-specific reconciliation step.

**Independent Test**: Generate a batch (one invoice now `PendingDebit`); import a CODA statement
whose transaction matches that invoice's OGM reference — it becomes `Paid` exactly as a `Sent`
invoice would (quickstart.md Scenario 3).

### Tests for User Story 2

- [ ] T027 [P] [US2] API test: a CODA import (feature 025) whose transaction exactly matches a
      `PendingDebit` invoice's OGM reference marks it `Paid` (FR-009), in
      `backend/ChildCare.Api.Tests/CodaTransactions/ImportCodaFileTests.cs` (extends the existing
      test file rather than duplicating fixtures).
- [ ] T028 [P] [US2] API test: a `PendingDebit` invoice with no exact reference match is offered
      through 025's existing amount+IBAN suggested-match path exactly as a `Sent` invoice would be
      (FR-009), in `ImportCodaFileTests.cs`.
- [ ] T029 [P] [US2] API test: `MarkInvoicePaidCommand` accepts a `PendingDebit` invoice (not just
      `Sent`) and rejects any other status, in
      `backend/ChildCare.Api.Tests/Invoices/MarkInvoicePaidCommandTests.cs`.

### Implementation for User Story 2

- [ ] T030 [US2] Extend `MarkInvoicePaidCommand`'s status guard in
      `backend/ChildCare.Application/Invoices/MarkInvoicePaidCommand.cs` to accept
      `Sent OR PendingDebit` (FR-009) — the sibling-cascade and receipt-notification logic (030/
      014a) is unchanged, since it already keys off `FamilyGroupId`, not the invoice's prior
      status.
- [ ] T031 [US2] Extend `ImportCodaFileCommand`'s open-invoice candidate queries in
      `backend/ChildCare.Application/CodaTransactions/ImportCodaFileCommand.cs` (both the exact-
      OGM lookup and the `FindAmountIbanCandidatesAsync(..., InvoiceStatus.Sent, ...)` calls) to
      include `PendingDebit` alongside `Sent` (depends on T030).

**Checkpoint**: US1 + US2 both work independently — generating a batch and having it reconcile
automatically are both demoable.

---

## Phase 5: User Story 3 - Handle a returned debit (Priority: P2)

**Goal**: A director can move a `PendingDebit` invoice back to `Sent` with a reason when the bank
returns it.

**Independent Test**: Put an invoice into `PendingDebit` via a batch; mark it returned with a
reason — it reverts to `Sent`, visible for normal follow-up (quickstart.md Scenario 4).

### Tests for User Story 3

- [ ] T032 [P] [US3] API test for `POST /api/invoices/{id}/mark-sepa-returned` in
      `backend/ChildCare.Api.Tests/Invoices/MarkInvoiceSepaReturnedTests.cs`: happy path reverts
      `PendingDebit → Sent`, clears `SepaBatchId`, records the reason (FR-010).
- [ ] T033 [P] [US3] API test: the action is rejected with `422 errors.invoice.not_pending_debit`
      against a `Sent`, `Draft`, or `Paid` invoice (FR-010's `Paid`-immutability exclusion,
      spec.md User Story 3/Acceptance Scenario 2), in `MarkInvoiceSepaReturnedTests.cs`.
- [ ] T034 [P] [US3] API test: an empty/missing reason returns `422
      errors.sepa_batch.reason_required`, in `MarkInvoiceSepaReturnedTests.cs`.
- [ ] T035 [P] [US3] API test: a returned invoice is eligible for a later batch exactly like any
      other `Sent` invoice — no special "previously returned" exclusion (spec.md User Story 3/
      Acceptance Scenario 3), in `backend/ChildCare.Api.Tests/SepaBatches/GenerateSepaBatchTests.cs`.

### Implementation for User Story 3

- [ ] T036 [US3] `MarkInvoiceSepaReturnedCommand`/Handler in
      `backend/ChildCare.Application/Invoices/MarkInvoiceSepaReturnedCommand.cs`: guards on
      `Status == PendingDebit`, requires a non-empty reason, sets `Status = Sent`, `SepaBatchId =
      null`, `SepaReturnReason = reason`.
- [ ] T037 [US3] `POST /api/invoices/{id}/mark-sepa-returned` endpoint in
      `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T036).
- [ ] T038 [P] [US3] i18n keys (NL/FR/EN) for the "mark returned" action, reason prompt, and
      `errors.invoice.not_pending_debit`/`errors.sepa_batch.reason_required` in
      `web/messages/{nl,fr,en}.json`.
- [ ] T039 [US3] "Mark returned" action + reason display on `PendingDebit` invoices in the
      existing director-web invoice list/detail (`web/app/(app)/invoices/`), following the same
      row-action convention as the existing send/regenerate/mark-paid actions (depends on T037).

**Checkpoint**: US1 + US2 + US3 all independently functional — a returned debit never leaves an
invoice stranded.

---

## Phase 6: User Story 4 - Exclude invoices without a valid mandate, and revoke one on request (Priority: P3)

**Goal**: A director can revoke a family's SEPA mandate, immediately excluding it from every
future batch, and re-invite signing through the existing 024 flow.

**Independent Test**: Revoke a mandate; generate a batch — the invoice is excluded with a
"mandate revoked" reason distinct from "no mandate" (quickstart.md Scenario 5).

### Tests for User Story 4

- [ ] T040 [P] [US4] API test for `POST /api/contracts/{id}/revoke-sepa-mandate` in
      `backend/ChildCare.Api.Tests/Contracts/RevokeSepaMandateTests.cs`: happy path sets
      `SepaRevokedAt`, response's `mandateStatus == "revoked"` (FR-011).
- [ ] T041 [P] [US4] API test: revoking a contract with no signed mandate, or one already revoked,
      returns `422 errors.contract.mandate_not_revocable`, in `RevokeSepaMandateTests.cs`.
- [ ] T042 [P] [US4] API test: after revocation, feature 024's existing
      `POST /api/contracts/{id}/signing-invitation` succeeds identically to a never-signed
      contract (FR-012), in `RevokeSepaMandateTests.cs`.
- [ ] T042a [P] [US4] API test: revoking a contract's mandate has no effect on an invoice already
      `PendingDebit` from a batch generated before the revocation — its status/`SepaBatchId` are
      unchanged, and it still reconciles via FR-009 or FR-010 exactly as before (FR-011's
      no-retroactive-effect clarification), in `RevokeSepaMandateTests.cs`.
- [ ] T043 [P] [US4] API test: after a revoke-and-resign cycle (new `SepaMandateReference`), a
      subsequent batch computes `SeqTp = FRST` again, not `RCUR` (research.md R3), in
      `backend/ChildCare.Api.Tests/SepaBatches/SepaSequenceTypeResolverTests.cs` (integration-level
      companion to T011's unit test).

### Implementation for User Story 4

- [ ] T044 [US4] `RevokeSepaMandateCommand`/Handler in
      `backend/ChildCare.Application/Contracts/RevokeSepaMandateCommand.cs`.
- [ ] T045 [US4] `POST /api/contracts/{id}/revoke-sepa-mandate` endpoint in
      `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` (depends on T044).
- [ ] T046 [US4] Extend `ContractResponse`/`ContractMapper` in
      `backend/ChildCare.Contracts/Responses/ContractResponse.cs` and
      `backend/ChildCare.Application/Contracts/ContractMapper.cs` with a derived `MandateStatus`
      (`none`/`signed`/`revoked`, mirrors the existing `SigningStatus` derivation) plus
      `SepaRevokedAt`.
- [ ] T047 [P] [US4] i18n keys (NL/FR/EN) for the revoke action/confirmation and
      `errors.contract.mandate_not_revocable` in `web/messages/{nl,fr,en}.json`.
- [ ] T048 [US4] "Revoke SEPA mandate" action on the contract's existing signing-status UI
      (wherever feature 024 surfaces `SigningStatus`/`SepaMandateReference` in director-web),
      gated on `mandateStatus == "signed"` (depends on T045, T046).

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T049 Design-compliance pass (per the loop's own step 7): review
      `web/app/(app)/invoices/sepa-batches/page.tsx` and any new/changed components against
      design-system.md (spacing scale, no nested cards, motion under 250ms) and platform-rules.md
      (director-web keyboard navigation, focus rings, high-density table conventions).
- [ ] T050 Self-assessed convergence pass and fix every finding (standing rule — no LOW-severity
      items left as debt): confirm `Workflows/billing.md`'s SEPA section (already added at
      specify-time) accurately describes the shipped flow, and re-check FR-001 through FR-015 each
      have a corresponding passing test.
- [ ] T051 Run quickstart.md's six scenarios end-to-end against a local TestContainers-backed run
      as a final sanity check before the full suite.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (entities/migration/XML
  generator/sequence resolver are shared by every story).
- **User Story 1 (Phase 3)**: Depends on Foundational. No dependency on US2/US3/US4.
- **User Story 2 (Phase 4)**: Depends on Foundational + US1's invoices actually reaching
  `PendingDebit` to have something to reconcile against in an end-to-end test, but the code
  changes themselves (T030/T031) are independent edits to existing 014/025 files — no dependency
  on US1's new endpoint code.
- **User Story 3 (Phase 5)**: Depends on Foundational (the `PendingDebit` status, T002) and, for
  its end-to-end test, an invoice reaching `PendingDebit` (US1's generation path) — the command/
  endpoint code itself has no dependency on US2.
- **User Story 4 (Phase 6)**: Depends on Foundational (`SepaRevokedAt`, T004) and, for T043,
  US1's `SepaSequenceTypeResolver`/generation path. Independently testable otherwise (T040-T042
  don't require a batch to exist).
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- T002-T005 (entity/enum changes), T007/T008/T009 (XML generator abstraction/impl/tests), T010/
  T011 (sequence resolver/tests) can run in parallel within Phase 2 (different files).
- T012-T018 (all US1 tests, independent test files/methods) can be written in parallel before
  T019 onward.
- T027-T029 (US2 tests), T032-T035 (US3 tests), and T040-T043 (US4 tests) can run in parallel with
  each other once Foundational + US1's core plumbing (T021/T023) land, since US2/US3/US4 touch
  largely disjoint command/handler files.

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1: generate and download a batch).
2. **STOP and VALIDATE**: quickstart.md Scenarios 1, 2, 6 pass — a director can review eligibility
   and generate a schema-valid batch.
3. This alone is a demoable, valuable increment (spec.md's P1 rationale: "without this, nothing
   ships").

### Incremental Delivery

1. Foundational → US1 (MVP) → US2 (automatic reconciliation) → US3 (returned-debit handling) →
   US4 (exclusion/revocation UI) → Polish.
2. Each story's checkpoint is independently demoable per spec.md's own Independent Test
   descriptions.
