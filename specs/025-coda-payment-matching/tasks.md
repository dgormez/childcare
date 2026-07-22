# Tasks: CODA/CODABOX Payment Matching

**Input**: Design documents from `/specs/025-coda-payment-matching/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/coda-payment-matching-api.md, quickstart.md

**Tests**: Included — Constitution V requires TestContainers-backed integration/API tests for
happy path plus key negative/regulatory flows, and every prior shipped feature in this codebase
follows that convention.

**Organization**: Tasks are grouped by user story (spec.md's US1/US2/US3, priority order).

## Path Conventions

Web application per plan.md's Project Structure: `backend/ChildCare.{Domain,Application,
Infrastructure,Contracts,Api}/`, `backend/ChildCare.Api.Tests/`, `web/`.

---

## Phase 1: Setup

- [ ] T001 Add the `CodaParser` NuGet package reference (research.md R1) to
      `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model, matching abstraction, and parsing adapter every user story depends on.

- [ ] T002 [P] Create `CodaImport` entity in
      `backend/ChildCare.Domain/Entities/CodaImport.cs` (data-model.md's `coda_imports`).
- [ ] T003 [P] Create `CodaTransaction` entity with `CodaMatchType` enum in
      `backend/ChildCare.Domain/Entities/CodaTransaction.cs` and
      `backend/ChildCare.Domain/Enums/CodaMatchType.cs` (data-model.md's `coda_transactions`:
      `Ogm`, `IbanAmount`, `Unmatched`, `Duplicate`, `ClosedInvoice`, `Reversal`).
- [ ] T004 Register `CodaImports`/`CodaTransactions` `DbSet`s and entity configuration
      (the `MatchType` CHECK constraint, the `(ValueDate, AmountCents, SenderIbanLast4)`
      non-unique index) in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`.
- [ ] T005 Generate the EF Core migration for `coda_imports`/`coda_transactions` in
      `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (`dotnet ef migrations
      add AddCodaPaymentMatching --context TenantDbContext`), and extend
      `TenantMigrationRolloutTests`'/`LegacyVaccinationMigrationTests`' revert-helper for the two
      new tables per the pattern every migration-adding feature since 012a has needed (see
      BACKLOG.md's shipped-notes for 012a/013c/006a/013d/013g/013h/014/014a/015 — check FK drop
      order against the tables they reference, the exact mistake 013g's shipped-note flagged).
- [ ] T006 [P] Define `ICodaParser` abstraction in
      `backend/ChildCare.Application/Common/ICodaParser.cs` — a `Parse(Stream fileContent)`
      method returning a plain DTO list (date, amount, sender IBAN, sender name, communication,
      whether the communication was structured) so `Application` never references the
      `CodaParser` NuGet types directly (mirrors `IInvoicePdfGenerator` wrapping QuestPDF).
- [ ] T007 [P] Implement `CodaParserAdapter : ICodaParser` in
      `backend/ChildCare.Infrastructure/Coda/CodaParserAdapter.cs`, wrapping the `CodaParser`
      package's `Parser.Parse`/`ParseFile` (research.md R1) and translating a parse failure into
      a typed exception the Application layer can catch and turn into FR-002's clean error —
      never letting the library's raw exception surface to the client (Principle VI).
- [ ] T008 [P] Unit tests for `CodaParserAdapter` in
      `backend/ChildCare.Api.Tests/CodaTransactions/CodaParserAdapterTests.cs`: a well-formed
      fixture parses into the expected DTOs (structured vs free-text communication distinguished
      correctly, per research.md R1's `StructuredMessage`/`Message` split); a malformed/corrupted
      file throws the typed parse-failure exception, not a raw library exception.
- [ ] T009 Implement `CodaTransactionMatcher` (pure matching logic, FR-004/005/005a/007/008/009/
      016) in `backend/ChildCare.Application/CodaTransactions/CodaTransactionMatcher.cs`,
      unit-testable independent of the MediatR handler and EF Core (depends on T003).
- [ ] T010 [P] Unit tests for `CodaTransactionMatcher` in
      `backend/ChildCare.Api.Tests/CodaTransactions/CodaTransactionMatcherTests.cs` covering
      every branch: exact OGM match (FR-004, digits-only comparison per research.md R1),
      OGM match against an already-`Paid` invoice → `Duplicate` (FR-008), amount+IBAN unambiguous
      suggestion (FR-005), amount+IBAN with multiple candidates → `Unmatched`, not guessed
      (FR-005a), amount+sender coincidentally matching an earlier-period `Paid` invoice →
      `ClosedInvoice` (FR-009), no match at all → `Unmatched` (FR-007), negative amount →
      `Reversal`, never eligible for matching (FR-016), malformed structured reference (right
      length, bad checksum) falls through to amount+IBAN matching rather than being treated as an
      exact match (spec.md Edge Cases).

**Checkpoint**: Entities, migration, parser adapter, and matcher exist and are unit-tested —
user story implementation can begin.

---

## Phase 3: User Story 1 - Import and auto-reconcile (Priority: P1) 🎯 MVP

**Goal**: A director uploads a CODA file; exact-reference matches are applied automatically.

**Independent Test**: Upload a file with transactions whose structured communication matches
existing invoices' OGM references (quickstart.md Scenario 1) — those invoices become `Paid` with
no further action, an import summary reports the counts, and an already-`Paid` invoice hit again
is flagged `Duplicate` rather than re-applied (quickstart.md Scenario 4).

### Tests for User Story 1

- [ ] T011 [P] [US1] API test for `POST /api/coda-imports` happy path in
      `backend/ChildCare.Api.Tests/CodaTransactions/ImportCodaFileTests.cs`: upload a fixture
      with exact-OGM and unmatched transactions, assert the response summary counts, assert the
      matched invoice's `Status`/`PaidAt` (via `MarkInvoicePaidCommand`, research.md R4).
- [ ] T012 [P] [US1] API test: malformed CODA file upload returns `422
      errors.coda_import.invalid_file` with no `coda_imports`/`coda_transactions` rows persisted
      (FR-002), in the same test file as T011.
- [ ] T013 [P] [US1] API test: re-uploading a file whose transactions were already imported skips
      them (`skippedDuplicateCount` matches, no duplicate rows created) — FR-013, in the same
      test file as T011.
- [ ] T014 [P] [US1] API test: an OGM match against an invoice already `Paid` is recorded as
      `Duplicate` and does not change the invoice (FR-008, quickstart.md Scenario 4), in the same
      test file as T011.
- [ ] T015 [P] [US1] API test: an OGM match whose amount is less than the invoice's total is
      recorded as a partial payment (`Applied = false`, invoice stays `Sent`) and a second,
      later `GET /api/coda-transactions` response's `matchedInvoice.receivedCents` reflects it
      (FR-010, quickstart.md Scenario 5), in the same test file as T011.
- [ ] T016 [P] [US1] API test: a negative-amount transaction is recorded as `Reversal`, never
      matched to any invoice (FR-016), in the same test file as T011.

### Implementation for User Story 1

- [ ] T017 [US1] `CodaImportSummaryResponse`/`CodaTransactionResponse` in
      `backend/ChildCare.Contracts/Responses/` per contracts/coda-payment-matching-api.md
      (`senderIbanMasked` from `SenderIbanLast4` only — the full IBAN never serializes to the
      client, FR-014).
- [ ] T018 [US1] `ImportCodaFileCommand`/Handler in
      `backend/ChildCare.Application/CodaTransactions/ImportCodaFileCommand.cs`: calls
      `ICodaParser`, runs each transaction through `CodaTransactionMatcher` (T009), applies
      `IIbanProtector.Protect` (research.md R2, new purpose string distinct from `Contract`'s) to
      `SenderIban` before persisting, calls `MarkInvoicePaidCommand` (research.md R4) for every
      `Ogm`/confirmed match that isn't partial, persists one `CodaImport` + N `CodaTransaction`
      rows in a single transaction, returns the summary (depends on T006, T007, T009, T017).
- [ ] T019 [US1] `POST /api/coda-imports` endpoint (multipart `IFormFile`, `DirectorOnly`) in
      `backend/ChildCare.Api/Endpoints/CodaTransactionEndpoints.cs`
      (`MapCodaTransactionEndpoints`), wiring `ImportCodaFileCommand` and mapping its
      parse-failure exception to `422 errors.coda_import.invalid_file` (depends on T018).
- [ ] T020 [US1] Register `MapCodaTransactionEndpoints` and `ICodaParser → CodaParserAdapter`,
      `IIbanProtector`-style DI registration for this feature's purpose string in
      `backend/ChildCare.Api/Program.cs`.
- [ ] T021 [P] [US1] i18n keys (NL/FR/EN) for the import summary and
      `errors.coda_import.invalid_file` in `web/messages/{nl,fr,en}.json` (FR-015).
- [ ] T022 [US1] Upload UI + import-summary display in
      `web/app/(app)/invoices/reconciliation/page.tsx` (loading/error states per spec.md UX
      Requirements — spinner during upload, human-readable error on a rejected file), using the
      regenerated openapi-fetch client for `POST /api/coda-imports`.

**Checkpoint**: A director can upload a statement and see exact-reference matches applied
automatically — the feature's core value, independently testable and demoable.

---

## Phase 4: User Story 2 - Review and confirm a suggested match (Priority: P2)

**Goal**: Amount+IBAN candidates surface as director-confirmable suggestions.

**Independent Test**: Import a transaction with no structured reference whose amount+sender
uniquely matches one open invoice's contract (quickstart.md Scenario 2) — it appears as a
suggestion; confirming marks the invoice paid identically to an exact match; rejecting leaves it
unmatched.

### Tests for User Story 2

- [ ] T023 [P] [US2] API test for `GET /api/coda-transactions?matchType=IbanAmount` and
      `POST /api/coda-transactions/{id}/confirm` in
      `backend/ChildCare.Api.Tests/CodaTransactions/ConfirmCodaTransactionMatchTests.cs`:
      confirming applies `MarkInvoicePaidCommand` and flips `Applied = true` (FR-006).
- [ ] T024 [P] [US2] API test for `POST /api/coda-transactions/{id}/reject` in
      `backend/ChildCare.Api.Tests/CodaTransactions/RejectCodaTransactionMatchTests.cs`: rejects
      to `Unmatched`, invoice untouched (FR-006).
- [ ] T025 [P] [US2] API test: confirming a transaction whose target invoice was independently
      marked `Paid` through another path in the meantime returns `422
      errors.coda_transaction.not_confirmable` and the transaction is re-surfaced as `Duplicate`
      (spec.md's stale-suggestion edge case), in `ConfirmCodaTransactionMatchTests.cs`.

### Implementation for User Story 2

- [ ] T026 [US2] `ListCodaTransactionsQuery` (filters: `matchType`, `needsReview`) in
      `backend/ChildCare.Application/CodaTransactions/ListCodaTransactionsQuery.cs`, joining
      `Invoice` for `matchedInvoice.totalCents`/`receivedCents` (research.md R5's read-time sum).
- [ ] T027 [US2] `ConfirmCodaTransactionMatchCommand`/Handler in
      `backend/ChildCare.Application/CodaTransactions/ConfirmCodaTransactionMatchCommand.cs`
      (depends on T026).
- [ ] T028 [P] [US2] `RejectCodaTransactionMatchCommand`/Handler in
      `backend/ChildCare.Application/CodaTransactions/RejectCodaTransactionMatchCommand.cs`.
- [ ] T029 [US2] `GET /api/coda-transactions`, `POST /api/coda-transactions/{id}/confirm`,
      `POST /api/coda-transactions/{id}/reject` endpoints in
      `backend/ChildCare.Api/Endpoints/CodaTransactionEndpoints.cs` (depends on T026-T028).
- [ ] T030 [US2] `CodaTransactionTable.tsx` component in `web/components/invoices/` — high-
      density table per design-system.md (40px rows, 8/12px cell padding), match-type badges
      (paired icon+color per design-system.md's Status Indicators), filterable by match type,
      full-row click affordance per platform-rules.md's director-web convention.
- [ ] T031 [US2] Wire the suggested-match confirm/reject actions into
      `web/app/(app)/invoices/reconciliation/page.tsx` (depends on T030).

**Checkpoint**: US1 + US2 both work independently — auto-matching and director-confirmed
suggestions are both demoable.

---

## Phase 5: User Story 3 - Handle transactions needing manual attention (Priority: P3)

**Goal**: Unmatched, duplicate, and closed-invoice transactions are clearly labeled and
dismissible without silently disappearing or touching any invoice.

**Independent Test**: Import an unmatchable transaction, a partial payment, and a payment
against an already-`Paid` invoice from an earlier period (quickstart.md Scenario 3/4) — each is
labeled distinctly; marking one reviewed removes it from the attention queue without changing
any invoice.

### Tests for User Story 3

- [ ] T032 [P] [US3] API test for `POST /api/coda-transactions/{id}/review` in
      `backend/ChildCare.Api.Tests/CodaTransactions/ReviewCodaTransactionTests.cs`: reviewing an
      `Unmatched`/`Duplicate`/`ClosedInvoice` row sets `ReviewedAt`/`ReviewedByUserId` and removes
      it from `GET /api/coda-transactions?needsReview=true`, without touching any invoice
      (FR-012); reviewing an `Ogm`/`IbanAmount` row returns `422
      errors.coda_transaction.not_reviewable`.
- [ ] T033 [P] [US3] API test: `GET /api/coda-transactions?needsReview=true` parity — total
      transactions from an import always equal the sum of every `matchType` bucket plus
      `skippedDuplicateCount` (spec.md SC-004), in
      `backend/ChildCare.Api.Tests/CodaTransactions/ImportCodaFileTests.cs`.

### Implementation for User Story 3

- [ ] T034 [US3] `ReviewCodaTransactionCommand`/Handler in
      `backend/ChildCare.Application/CodaTransactions/ReviewCodaTransactionCommand.cs`.
- [ ] T035 [US3] `POST /api/coda-transactions/{id}/review` endpoint in
      `backend/ChildCare.Api/Endpoints/CodaTransactionEndpoints.cs` (depends on T034).
- [ ] T036 [US3] "Needs review" filter toggle + review/dismiss action in
      `web/app/(app)/invoices/reconciliation/page.tsx` and `CodaTransactionTable.tsx`, with
      distinct visual labels for `Unmatched`/`Duplicate`/`ClosedInvoice`/`Reversal` per
      design-system.md's badge+icon pairing convention (depends on T030).

**Checkpoint**: All three user stories independently functional — the review queue is complete
and trustworthy, matching spec.md's SC-004.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T037 Design-compliance pass (per the loop's own step 7): review
      `web/app/(app)/invoices/reconciliation/page.tsx` and `CodaTransactionTable.tsx` against
      design-system.md (spacing scale, no nested cards, motion under 250ms) and platform-rules.md
      (director-web keyboard navigation, focus rings).
- [ ] T038 Run `/speckit-converge` and fix every finding (standing rule — no LOW-severity items
      left as debt).
- [ ] T039 Run quickstart.md's five scenarios end-to-end against a local TestContainers-backed
      run as a final sanity check before the full suite.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (entities/migration/
  parser/matcher are shared by every story).
- **User Story 1 (Phase 3)**: Depends on Foundational. No dependency on US2/US3.
- **User Story 2 (Phase 4)**: Depends on Foundational + T017/T020 (response DTOs, DI/endpoint
  registration from US1) — reuses the same endpoint file and DI wiring US1 establishes, but the
  suggested-match matching logic itself (T009/T010) is already Foundational, so US2's *feature
  behavior* is independently testable once US1's plumbing exists.
- **User Story 3 (Phase 5)**: Same plumbing dependency as US2; independently testable.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Parallel Opportunities

- T002/T003 (entities), T006/T007/T008 (parser abstraction/adapter/tests) can run in parallel
  within Phase 2.
- T011-T016 (all US1 API tests, same file but independent test methods) can be written in
  parallel before T017 onward.
- T023-T025 (US2 tests) and T032-T033 (US3 tests) can run in parallel with each other once
  Foundational + T017/T020 land, since US2 and US3 touch disjoint command/handler files.

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1: import + auto-match).
2. **STOP and VALIDATE**: quickstart.md Scenarios 1, 4, 5 pass — a director can upload a
   statement and see exact matches applied, duplicates/partials handled correctly.
3. This alone is a demoable, valuable increment (spec.md's P1 rationale: "without this, the
   feature delivers nothing").

### Incremental Delivery

1. Foundational → US1 (MVP) → US2 (suggested-match confirmation) → US3 (review queue
   completeness) → Polish.
2. Each story's checkpoint is independently demoable per spec.md's own Independent Test
   descriptions.
