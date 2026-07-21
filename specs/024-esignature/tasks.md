---

description: "Task list for feature 024-esignature"
---

# Tasks: Digital Contract E-Signature

**Input**: Design documents from `/specs/024-esignature/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/esignature-api.md, quickstart.md

**Tests**: Included — constitution Principle V (Test with Real Infrastructure) requires
TestContainers-backed integration tests covering happy path plus key negative/regulatory flows;
spec.md's Testing Requirements explicitly calls out token single-use/expiry/tamper rejection,
IBAN validation/encryption, resend-invalidates-prior-token, revision-invalidates-token,
activation-independence, and locale-respecting emails.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to spec.md's User Story 1–4

## Path Conventions

Backend: `backend/ChildCare.{Domain,Application,Contracts,Infrastructure,Api}/...` (existing
five-project solution). Web: `web/{app,components,lib,i18n}/...` (existing Next.js app). Per
plan.md's Project Structure.

## Story-to-priority note

Spec.md orders stories US1 (parent signs) before US2 (director sends) — both P1 — but US1's own
Independent Test requires an invitation to already have been sent, and US2's send action has a
hard technical prerequisite on User Story 4's `SepaCreditorIdentifier` (FR-016). Tasks below are
sequenced by actual dependency (US4 → US2 → US1 → US3) rather than spec.md's listing order,
consistent with the Task Generation Rules' "most stories should be independent" guidance where a
genuine technical dependency exists; each story remains independently *testable* once its
dependencies are met, per its own Independent Test in spec.md.

---

## Phase 1: Setup

- [ ] T001 [P] Add empty `contractSigning` (signing page + emails) and `contracts` (director-web
  screen) i18n key namespaces to `web/i18n/locales/{en,nl,fr}.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — every story reads
or writes through the `Contract`/`Tenant` schema changes and/or the new token/encryption/storage
services.

- [ ] T002 [P] Add `SignatureType` enum (`Drawn`, `Typed`) in
  `backend/ChildCare.Domain/Enums/SignatureType.cs`
- [ ] T003 Add `SigningToken`, `SigningTokenExpiresAt`, `SignedAt`, `SignatureData`,
  `SignatureType`, `SignedByIp`, `SepaIbanEncrypted`, `SepaMandateReference`,
  `SepaAuthorisedAt` (all nullable) to `backend/ChildCare.Domain/Entities/Contract.cs` per
  data-model.md (depends on T002)
- [ ] T004 [P] Add `SepaCreditorIdentifier` (`string?`) to
  `backend/ChildCare.Domain/Entities/Tenant.cs`, alongside the existing `KboNumber` field
- [ ] T005 Generate the EF Core tenant-schema migration (`dotnet ef migrations add
  AddContractSigningAndSepaMandate --project backend/ChildCare.Infrastructure --context
  TenantDbContext --output-dir Persistence/Migrations/Tenant`) for T003's `Contract` columns,
  verify it applies cleanly against a fresh dev schema, and generate the manually-run SQL script
  under `specs/024-esignature/migrations/` per `.claude/CLAUDE.md` (depends on T003)
- [ ] T006 Generate the EF Core public-schema migration (`--context PublicDbContext`) for T004's
  `Tenant.SepaCreditorIdentifier` column, verify it applies cleanly, and generate its manually-run
  SQL script under `specs/024-esignature/migrations/` (depends on T004)
- [ ] T007 [P] Create `IContractSigningTokenService` (`CreateToken(Guid contractId) : string` /
  `TryParseToken(string) : Guid?`, fails closed) in
  `backend/ChildCare.Application/Common/IContractSigningTokenService.cs`, and
  `DataProtectionContractSigningTokenService` (Data Protection API, purpose
  `"Contract.Signing"`, `ToTimeLimitedDataProtector`, 72-hour lifetime — research.md R2) in
  `backend/ChildCare.Infrastructure/Email/DataProtectionContractSigningTokenService.cs`, directly
  mirroring `DataProtectionTourInvitationTokenService`'s shape
- [ ] T008 [P] Create `IIbanProtector` (`Protect`/`Unprotect`) in
  `backend/ChildCare.Application/Common/IIbanProtector.cs`, and `IbanProtector` (Data Protection
  API, purpose `"Contract.SepaIban"` — research.md R3) in
  `backend/ChildCare.Infrastructure/Contracts/IbanProtector.cs`, directly mirroring
  `NrnProtector`'s shape
- [ ] T009 [P] Create `ISignedContractStorage` (`UploadAsync(Guid contractId, byte[] pdfBytes) :
  Task<string ObjectPath>`, `CreateDownloadUrlAsync(string objectPath) : Task<string>`) in
  `backend/ChildCare.Application/Common/ISignedContractStorage.cs`, and
  `GcsSignedContractStorage` (deterministic path `signed-contracts/{contractId}.pdf`, V4 signed
  URLs, ADC credentials — research.md R6) in
  `backend/ChildCare.Infrastructure/Storage/GcsSignedContractStorage.cs`, directly mirroring
  `GcsFiscalAttestationStorage`'s shape
- [ ] T010 [P] Add an IBAN format+checksum (mod-97) validator helper — a static method or small
  class usable from FluentValidation — in
  `backend/ChildCare.Application/Common/IbanValidation.cs`
- [ ] T011 Register `IContractSigningTokenService` → `DataProtectionContractSigningTokenService`,
  `IIbanProtector` → `IbanProtector`, and `ISignedContractStorage` → `GcsSignedContractStorage`
  in DI in `backend/ChildCare.Api/Program.cs` (depends on T007, T008, T009)
- [ ] T012 [P] Create `ContractSigningLinkBuilder` (mirrors `EmailLinkBuilder`'s
  `BuildXxxUrl(IConfiguration, ...)` shape) with `BuildSigningUrl(token, orgSlug)` (points at the
  `web/` app's `/sign` route, per research.md R1) in
  `backend/ChildCare.Application/Contracts/ContractSigningLinkBuilder.cs`
- [ ] T013 [P] Create a derived-status helper (`ContractSigningStatus`: `NotSent`/`Pending`/
  `Expired`/`Signed`, computed per data-model.md's rule, not stored) in
  `backend/ChildCare.Application/Contracts/ContractSigningStatus.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 4 - Director configures the organisation's SEPA Creditor Identifier (Priority: P2, implemented first — hard prerequisite for User Story 2's FR-016 gate)

**Goal**: A director can set the organisation's SEPA Creditor Identifier once, in organisation
settings, alongside the existing `KboNumber` field.

**Independent Test**: Set the value via `PUT /api/organisations/me`, then `GET /api/organisations/me`
and confirm it's returned — independent of the signing flow itself (spec.md's own note that a
test environment can seed it directly).

### Tests for User Story 4

- [ ] T014 [P] [US4] Integration test: `PUT /api/organisations/me` with
  `sepaCreditorIdentifier` persists the value and `GET /api/organisations/me` returns it, in
  `backend/ChildCare.Api.Tests/Organisations/UpdateOrganisationTests.cs` (extend existing file)

### Implementation for User Story 4

- [ ] T015 [US4] Add `SepaCreditorIdentifier` to `UpdateOrganisationCommand` (alongside
  `KboNumber`) and its handler in
  `backend/ChildCare.Application/Organisations/UpdateOrganisationCommand.cs`; add it to
  `GetCurrentOrganisationQuery`'s response in the same folder (depends on T004)
- [ ] T016 [US4] Add `SepaCreditorIdentifier` to `UpdateOrganisationRequest` and
  `OrganisationResponse` in `backend/ChildCare.Contracts/Requests/OrganisationRequests.cs` and
  `backend/ChildCare.Contracts/Responses/OrganisationResponses.cs`; thread it through the
  existing `PUT`/`GET /api/organisations/me` handlers in
  `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs` (depends on T015)
- [ ] T017 [US4] [P] Add a "SEPA Creditor Identifier" field to the existing organisation-settings
  screen in `web/` (wherever `KboNumber` is currently edited), with `contracts.creditorId.*`
  i18n keys

**Checkpoint**: The creditor ID can be configured and read back — User Story 2 can now enforce
FR-016 against a real value.

---

## Phase 4: User Story 2 - Director sends a contract for signing and sees its status (Priority: P1)

**Goal**: A director sends a signing invitation from a `Draft` contract's screen, sees the
signing status update, and can eventually see the signed date and reach the signed PDF.

**Independent Test**: Send an invitation from a `Draft` contract with a contact email on file,
verify the email is sent and the derived status shows `Pending`; separately verify sending is
rejected (with a clear reason) when the contact has no email or the creditor ID isn't configured.

### Tests for User Story 2

- [ ] T018 [P] [US2] Integration test: `POST /api/contracts/{id}/signing-invitation` on a
  `Draft` contract with a primary contact email sends an email and sets `SigningToken`/
  `SigningTokenExpiresAt`, in `backend/ChildCare.Api.Tests/Contracts/ContractSigningTests.cs`
  (new file)
- [ ] T019 [P] [US2] Integration test: the same endpoint returns `422
  errors.contract_signing.no_contact_email` when the contract's child has no primary contact
  email, and `422 errors.contract_signing.creditor_id_not_configured` when
  `Tenant.SepaCreditorIdentifier` is unset, in the same test file
- [ ] T020 [P] [US2] Integration test: the same endpoint returns `409 errors.contract.not_draft`
  for an `Active`/`Ended` contract, in the same test file

### Implementation for User Story 2

- [ ] T021 [US2] Create `SendContractSigningInvitationCommand` + handler (resolves primary
  contact via the `IsPrimary`-ordered `ChildContact`→`Contact` join per research.md R9, checks
  `Status == Draft`, contact email present, `Tenant.SepaCreditorIdentifier` set; generates a
  token via `IContractSigningTokenService`, sets `SigningToken`/`SigningTokenExpiresAt = now +
  72h` — overwriting any prior outstanding token, serves both send and resend; sends the
  invitation email) in
  `backend/ChildCare.Application/Contracts/SendContractSigningInvitationCommand.cs` (depends on
  T007, T012, T013, T015)
- [ ] T022 [US2] Add `SendContractSigningInvitationAsync(toEmail, locale, childName,
  locationName, signingUrl)` to `IEmailSender`
  (`backend/ChildCare.Application/Common/IEmailSender.cs`) and `EmailService`
  (`backend/ChildCare.Api/Services/EmailService.cs`), plus a new
  `backend/ChildCare.Infrastructure/Email/Templates/contract-signing-invitation.scriban` template
  (depends on T021)
- [ ] T023 [US2] Add `POST /api/contracts/{id}/signing-invitation` to
  `backend/ChildCare.Api/Endpoints/ContractsEndpoints.cs` (`DirectorOnly`, existing
  `/api/contracts` group), mapping `SendContractSigningInvitationCommand` and its failure cases
  (depends on T021)
- [ ] T024 [US2] Add the derived signing status (`ContractSigningStatus`) and masked IBAN (last
  4 digits only, per research.md R4) to `ContractResponse` in
  `backend/ChildCare.Contracts/Responses/ContractResponses.cs`, and populate it in
  `GetContractByIdQuery`/`ListChildContractsQuery` (depends on T013)
- [ ] T025 [US2] [P] Build out `web/app/(app)/contracts/page.tsx` (replacing the
  `NotYetAvailable` stub) with a contract list showing derived signing status, a "Send for
  signature" action on `Draft` contracts with no outstanding token, and a "Resend" action on
  `Pending`/`Expired` ones (depends on T024)

**Checkpoint**: A director can send/resend invitations and see status — User Story 1 can now be
built and tested end-to-end against a real pending invitation.

---

## Phase 5: User Story 1 - Parent reviews and digitally signs the contract, authorising SEPA in the same session (Priority: P1) 🎯 MVP-completing

**Goal**: A parent opens a valid signing link, reviews the contract, signs, authorises the SEPA
mandate, and the contract records everything, generates a signed PDF, and emails both parties.

**Independent Test**: Send an invitation (User Story 2), complete the public signing flow as the
parent, and verify the contract is marked signed with a persisted PDF and SEPA mandate recorded,
while `Contract.Status` remains unaffected (per spec.md's Clarifications, FR-015).

### Tests for User Story 1

- [ ] T026 [P] [US1] Integration test: `GET /api/public/contracts/sign?org=...&token=...` with a
  valid, unused, unexpired token returns the contract-for-signing fields; with an invalid,
  expired, or already-used token returns the same generic `404
  errors.contract_signing.invalid_or_expired`, in
  `backend/ChildCare.Api.Tests/Contracts/PublicContractSigningTests.cs` (new file)
- [ ] T027 [P] [US1] Integration test: `POST` the same endpoint with a valid signature + IBAN
  records `SignedAt`/`SignatureData`/`SignedByIp`/`SepaMandateReference`/`SepaIbanEncrypted`/
  `SepaAuthorisedAt`, generates a signed PDF in storage, invalidates the token (a second `GET`/
  `POST` with the same token now 404s), and leaves `Contract.Status` as `Draft`, in the same
  test file
- [ ] T028 [P] [US1] Integration test: `POST` with an invalid-checksum IBAN returns `422
  errors.contract_signing.invalid_iban` and does not consume the token (a subsequent valid `POST`
  with the same token still succeeds), in the same test file
- [ ] T029 [P] [US1] Integration test: two concurrent `POST` requests against the same valid
  token — exactly one succeeds, the other gets `404`, and exactly one `SepaMandateReference`/
  signed PDF exists afterward, in the same test file
- [ ] T030 [P] [US1] Integration test: signed PDF is emailed to both the parent and the
  location's director(s), in the same test file

### Implementation for User Story 1

- [ ] T031 [US1] Create `GetContractForSigningQuery` (public, tenant-exempt — validates token via
  `IContractSigningTokenService` + the stored `SigningToken`/`SigningTokenExpiresAt` match,
  returns the same field set `ContractPdfModel` renders) in
  `backend/ChildCare.Application/Contracts/GetContractForSigningQuery.cs` (depends on T007)
- [ ] T032 [US1] Create `SubmitContractSigningCommand` + handler — re-validates the token
  server-side, validates the IBAN (T010), and within one transaction: sets `SignedAt`,
  `SignatureData`/`SignatureType`, `SignedByIp` (from `HttpContext.Connection.RemoteIpAddress`),
  a newly generated unique `SepaMandateReference` (8-char unambiguous alphabet, re-rolled on
  collision, per research.md R6/023's precedent), `SepaIbanEncrypted` (via `IIbanProtector`),
  `SepaAuthorisedAt`; clears `SigningToken`/`SigningTokenExpiresAt`; generates the final signed
  PDF and uploads via `ISignedContractStorage`; commits; then (fire-and-forget) emails both
  parties — in `backend/ChildCare.Application/Contracts/SubmitContractSigningCommand.cs`
  (depends on T007, T008, T009, T010)
- [ ] T033 [US1] Extend `QuestPdfContractGenerator`/`ContractPdfModel`
  (`backend/ChildCare.Infrastructure/Pdf/QuestPdfContractGenerator.cs`,
  `backend/ChildCare.Application/Common/IContractPdfGenerator.cs`) with an optional signature
  block (rendered signature image or typed name, signed date, signer IP) and SEPA mandate section
  (masked IBAN, mandate reference, creditor identifier, authorisation date) — used only when
  generating the final signed PDF, unchanged for the existing unsigned on-demand PDF (depends on
  T003)
- [ ] T034 [US1] Add `SendSignedContractAsync(toEmail, locale, childName, pdfBytes)` (or a
  signed-URL variant) to `IEmailSender`/`EmailService`, plus a new
  `backend/ChildCare.Infrastructure/Email/Templates/signed-contract-copy.scriban` template
  (depends on T032)
- [ ] T035 [US1] Create `backend/ChildCare.Api/Endpoints/PublicContractSigningEndpoints.cs` (new
  file) mapping `GET`/`POST /api/public/contracts/sign` — `AllowAnonymous()`,
  `.RequireTenantExempt()`, resolving `org` via `OrganisationSlugResolver` before dispatching
  `GetContractForSigningQuery`/`SubmitContractSigningCommand` (depends on T031, T032)
- [ ] T036 [US1] [P] Create `web/components/SignatureCapture.tsx` — a `<canvas>` + Pointer
  Events drawing surface with a typed-name fallback tab (research.md R8)
- [ ] T037 [US1] [P] Create `web/app/sign/page.tsx` (new public route, outside `(app)`/`(auth)`)
  — fetches contract-for-signing data via `web/lib/publicApiClient.ts`, renders the contract
  content with scroll-to-bottom gating on the signature controls (FR-006), embeds
  `SignatureCapture`, an IBAN field with inline validation, and submit/loading/error/completion
  states per spec.md's UX Requirements (depends on T035, T036)

**Checkpoint**: The full send → sign → activate-independently loop works end-to-end — this
completes the MVP (User Stories 1 + 2, both P1).

---

## Phase 6: User Story 3 - Signing links stay correct over time: resend on expiry, invalidate on revision (Priority: P2)

**Goal**: An expired/lost link can be resent (already the same endpoint as User Story 2's send —
no new send/resend distinction to implement); editing a `Draft` contract with an outstanding
signing invitation immediately invalidates that invitation.

**Independent Test**: Let a token expire (or force-expire it in a test), resend, and verify the
old link 404s while the new one works; separately, edit a contract with an outstanding invitation
and verify the previously issued link 404s afterward.

### Tests for User Story 3

- [ ] T038 [P] [US3] Integration test: resending (calling
  `SendContractSigningInvitationCommand` again) issues a new token and immediately invalidates
  the previous one (old token now 404s on `GET`), in
  `backend/ChildCare.Api.Tests/Contracts/ContractSigningTests.cs`
- [ ] T039 [P] [US3] Integration test: `PUT /api/contracts/{id}` on a `Draft` contract with an
  outstanding, unsigned signing token clears `SigningToken`/`SigningTokenExpiresAt` (the
  previously issued link now 404s), and does **not** auto-send a new invitation, in
  `backend/ChildCare.Api.Tests/Contracts/UpdateContractTests.cs` (extend existing file)

### Implementation for User Story 3

- [ ] T040 [US3] Add a step to `UpdateContractCommandHandler`
  (`backend/ChildCare.Application/Contracts/UpdateContractCommand.cs`): if the contract being
  updated has a non-null `SigningToken`, clear `SigningToken`/`SigningTokenExpiresAt` as part of
  the same save (FR-013) (depends on T003)

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T041 [P] Add `contractSigning.*`/`contracts.*` translated copy (not just empty
  namespaces from T001) to `web/i18n/locales/{en,nl,fr}.json` for every new string introduced by
  US1/US2/US4 (signing page, both emails, director-web contract screen, creditor-ID setting)
- [ ] T042 [P] Run `quickstart.md`'s six scenarios end-to-end against local dev and confirm each
  passes
- [ ] T043 Confirm `ContractSigningStatus`'s `Pending`/`Expired` boundary (FR-003's 72-hour
  window) is exercised by an automated test using a controllable clock rather than a real
  72-hour wait, in `backend/ChildCare.Api.Tests/Contracts/ContractSigningTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 4 (Phase 3)**: Depends on Foundational only. Implemented first despite its P2
  spec.md priority because it's a hard technical prerequisite for User Story 2 (FR-016) — see
  the Story-to-priority note above.
- **User Story 2 (Phase 4)**: Depends on Foundational + User Story 4 (needs a configured
  `SepaCreditorIdentifier` to pass its own FR-016 check in tests).
- **User Story 1 (Phase 5)**: Depends on Foundational + User Story 2 (needs a real pending
  invitation to sign against — spec.md's own Independent Test for US1 says so explicitly).
- **User Story 3 (Phase 6)**: Depends on User Story 2 (resend reuses its command) and touches
  the existing `UpdateContractCommand` (007) — can be implemented any time after Phase 4.
- **Polish (Phase 7)**: Depends on all four user stories.

### Parallel Opportunities

- All Foundational tasks marked [P] (T002, T004, T007, T008, T009, T010, T012, T013) can run in
  parallel once T001 is done, except T003/T005/T006 which serialize behind T002/T004.
- Within User Story 2, T018–T020 (tests) can run in parallel with each other before T021 exists;
  T025 (web) can proceed in parallel with T022/T023 (backend) once T024's response shape is
  settled.
- Within User Story 1, T026–T030 (tests) are parallel; T036 (signature component) is independent
  of T031–T035 (backend) and can be built in parallel, converging at T037.

---

## Implementation Strategy

### MVP First

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (User Story 4) — small, unblocks the rest.
3. Complete Phase 4 (User Story 2) — director can send invitations.
4. Complete Phase 5 (User Story 1) — parent can sign. **This is the MVP**: the full
   send → sign → signed-PDF loop works end-to-end.
5. **STOP and VALIDATE**: run quickstart.md Scenarios 0–1.

### Incremental Delivery

1. Foundation + US4 + US2 + US1 → MVP, validated via quickstart.md Scenario 1.
2. Add US3 → validated via quickstart.md Scenarios 2–3.
3. Polish phase → validated via the full quickstart.md suite (Scenarios 0–6).
