# Tasks: Email Communications

**Input**: Design documents from `specs/020-email-communications/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests), same standard every prior feature has followed — happy path plus key
negative/regulatory flows per spec.md's Testing Requirements.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New dependency, response contracts, and i18n scaffolding shared across every story.

- [X] T001 Add the Scriban NuGet package to `backend/ChildCare.Infrastructure/ChildCare.Infrastructure.csproj` (research.md R1)
- [X] T002 [P] Add response DTOs per contracts/email-communications-api.md (`BulkEmailUploadUrlResponse`, `BulkEmailSendResultResponse`, `BulkEmailRecipientCountResponse`, `DailyReportResendResultResponse`, `UnsubscribeResultResponse`) in `backend/ChildCare.Contracts/Responses/EmailResponses.cs`
- [X] T003 [P] Add director-web `communications.*` i18n keys (location/group selector, subject/body fields, attachment upload progress/error, send button, delivery-outcome summary, zero-recipient empty state) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [X] T004 [P] Add `errors.email.*` i18n keys (subject_required, subject_too_long, body_required, body_too_long, invalid_content_type, attachment_too_large) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model, templating infrastructure, attachment storage, and the unsubscribe
token mechanism every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add `DigestUnsubscribedAt` (`DateTime?`) to `Contact` in `backend/ChildCare.Domain/Entities/Contact.cs` per data-model.md
- [X] T006 [P] Add `BulkEmailDeliveryStatus` enum (`Sent`, `SkippedNoEmail`, `ProviderFailure`) in `backend/ChildCare.Domain/Enums/BulkEmailDeliveryStatus.cs` per data-model.md
- [X] T007 [P] Add `BulkEmailSend` entity in `backend/ChildCare.Domain/Entities/BulkEmailSend.cs` per data-model.md
- [X] T008 [P] Add `BulkEmailRecipient` entity in `backend/ChildCare.Domain/Entities/BulkEmailRecipient.cs` per data-model.md (depends on T006, T007)
- [X] T009 Map `Contact.DigestUnsubscribedAt`, `BulkEmailSend`, `BulkEmailRecipient` (with an index on `BulkEmailRecipient.BulkEmailSendId`) in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T005, T007, T008)
- [X] T010 Add `ITenantDbContext` members (`Contacts` already exists; add `BulkEmailSends`, `BulkEmailRecipients`) in `backend/ChildCare.Application/Common/ITenantDbContext.cs` (depends on T009)
- [X] T011 Add tenant migration `AddEmailCommunications` (Contact column + two new tables + index) in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T009, T010)
- [X] T012 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new column/tables (the recurring pattern every migration-adding feature since 003 has needed) in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T011)
- [X] T013 [P] Add `IEmailTemplateRenderer` port (locale + template-name + model → rendered HTML string) in `backend/ChildCare.Application/Common/IEmailTemplateRenderer.cs` per research.md R1
- [X] T014 [P] Add shared HTML email layout partial (`_layout.scriban` — inline-CSS header/footer, matches design-system.md's warmth/calm tone within email-client constraints) in `backend/ChildCare.Infrastructure/Email/Templates/_layout.scriban`
- [X] T015 `ScribanEmailTemplateRenderer` implementing `IEmailTemplateRenderer` (loads embedded `.scriban` resources, renders with the shared layout) in `backend/ChildCare.Infrastructure/Email/ScribanEmailTemplateRenderer.cs` (depends on T013, T014)
- [X] T016 [P] Extend `IEmailSender` with new templated send methods (`SendBulkEmailAsync`, `SendDailyReportAsync`, `SendClosureNotificationEmailAsync`, `SendAnnouncementEmailAsync`) accepting a locale + template model, replacing the raw-string-literal signatures for these kinds only (existing auth-email methods — verify/reset/invitation — are unchanged, out of this feature's scope) in `backend/ChildCare.Application/Common/IEmailSender.cs`
- [X] T017 Implement the new `IEmailSender` methods in `EmailService` using `IEmailTemplateRenderer`, including per-email-kind MIME attachment support (bulk email only) in `backend/ChildCare.Api/Services/EmailService.cs` (depends on T015, T016)
- [X] T018 Correct the stale "feature 019 owns the templating/i18n rework" doc comments to reference this feature (020) in `backend/ChildCare.Application/Common/IEmailSender.cs` (spec.md Assumptions)
- [X] T019 [P] Add `IBulkEmailAttachmentStorage` port in `backend/ChildCare.Application/Common/IBulkEmailAttachmentStorage.cs` per research.md R3 (mirrors `IHealthAttachmentStorage`'s shape)
- [X] T020 `GcsBulkEmailAttachmentStorage` implementing `IBulkEmailAttachmentStorage` (signed upload/download URLs, `bulk-email-attachments/{bulkEmailSendId}/attachment.{ext}` object path, 15-minute TTL) in `backend/ChildCare.Infrastructure/Storage/GcsBulkEmailAttachmentStorage.cs` (depends on T019)
- [X] T021 [P] Add `IUnsubscribeTokenService` port (issue/verify a `Contact.Id` + purpose-scoped token via `IDataProtector` — the token itself carries no tenant info; schema resolution is a separate step via the link's `org` slug, per research.md R5) in `backend/ChildCare.Application/Common/IUnsubscribeTokenService.cs` per research.md R5
- [X] T022 Implement `DataProtectionUnsubscribeTokenService` in `backend/ChildCare.Infrastructure/Email/DataProtectionUnsubscribeTokenService.cs` (depends on T021)
- [X] T023 Register `IDataProtection`, `IEmailTemplateRenderer`, `IBulkEmailAttachmentStorage`, `IUnsubscribeTokenService`, and the new `IEmailSender` implementation in DI in `backend/ChildCare.Api/Program.cs` (depends on T015, T017, T020, T022)
- [X] T024 [P] Add `EmailEndpoints.cs` skeleton (route group, no handlers yet) in `backend/ChildCare.Api/Endpoints/EmailEndpoints.cs`
- [X] T025 Register `app.MapEmailEndpoints()` in `backend/ChildCare.Api/Program.cs` (depends on T024)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Director sends a one-off bulk email to a location or group (Priority: P1) 🎯 MVP

**Goal**: Director composes and sends a bulk email (subject/body, optional PDF/image attachment)
to every enrolled household at a location or group, with household de-duplication and a
post-send delivery-outcome summary.

**Independent Test**: Seed a location with 5 families (one sharing a contact across two
children, one contact with no email), send a bulk email with an attachment, and confirm the
sibling family receives exactly one email, the no-email contact is skipped/logged, and every
other contact receives the attachment intact.

### Tests for User Story 1 ⚠️

- [X] T026 [P] [US1] Integration test: `POST /api/email/bulk-send` sends exactly one email per
  household, not per child, for a contact linked to two children in scope (FR-002) in
  `backend/ChildCare.Api.Tests/Email/BulkEmailEndpointsTests.cs`
- [X] T027 [P] [US1] Integration test: a contact with no email on file is skipped, logged, and
  does not block the rest of the batch (FR-012) in
  `backend/ChildCare.Api.Tests/Email/BulkEmailEndpointsTests.cs`
- [X] T028 [P] [US1] Integration test: `GET /api/email/bulk-send/recipient-count` and
  `POST /api/email/bulk-send` both return zero for a location/group with no currently enrolled
  children, with no error (FR-016) in `backend/ChildCare.Api.Tests/Email/BulkEmailEndpointsTests.cs`
- [X] T029 [P] [US1] Integration test: `groupId` narrows recipients to only that group's
  currently-assigned children's contacts (FR-001) in
  `backend/ChildCare.Api.Tests/Email/BulkEmailEndpointsTests.cs`
- [X] T030 [P] [US1] Integration test: `POST /api/email/attachments/upload-url` rejects a
  disallowed content type (422 `errors.email.invalid_content_type`) and an uploaded object over
  10MB is rejected at send time (`errors.email.attachment_too_large`, FR-017) in
  `backend/ChildCare.Api.Tests/Email/BulkEmailAttachmentTests.cs`
- [X] T031 [P] [US1] Integration test: a director from tenant A cannot resolve or send to tenant
  B's contacts via any of this story's endpoints (FR-013) in
  `backend/ChildCare.Api.Tests/Email/BulkEmailEndpointsTests.cs`

### Implementation for User Story 1

- [X] T032 [P] [US1] `bulk-email-*.scriban` content template (director subject/body rendered
  into the shared layout, plain-text director copy escaped, not raw HTML — research.md R1 model
  note) in `backend/ChildCare.Infrastructure/Email/Templates/bulk-email.scriban`
- [X] T033 [US1] `GetBulkEmailRecipientCountQuery`/Handler (location/group scope → distinct
  household count, no `TenantUserId` gate per research.md R4) in
  `backend/ChildCare.Application/Email/GetBulkEmailRecipientCountQuery.cs`
- [X] T034 [US1] `CreateBulkEmailAttachmentUploadUrlCommand`/Handler (content-type validation,
  issues signed upload URL via `IBulkEmailAttachmentStorage`) in
  `backend/ChildCare.Application/Email/CreateBulkEmailAttachmentUploadUrlCommand.cs` (depends on T020)
- [X] T035 [US1] `SendBulkEmailCommand`/Validator/Handler — creates `BulkEmailSend`, resolves
  recipients (mirrors `SendAnnouncementCommandHandler`'s location/group child resolution, but
  filtered on `Contact.Email != null` with no `TenantUserId` gate per R4), verifies attachment
  size via the storage port, sends via `IEmailSender.SendBulkEmailAsync` per contact/locale,
  writes one `BulkEmailRecipient` per outcome, tolerates partial failure (FR-012) in
  `backend/ChildCare.Application/Email/SendBulkEmailCommand.cs` (depends on T032, T033, T034)
- [X] T036 [US1] Implement `POST /api/email/attachments/upload-url`,
  `POST /api/email/bulk-send`, `GET /api/email/bulk-send/recipient-count` (`DirectorOnly`) in
  `backend/ChildCare.Api/Endpoints/EmailEndpoints.cs` (depends on T033, T034, T035)
- [X] T037 [US1] Add `web/app/(app)/communications/page.tsx` — location/group selector, subject/
  body fields, attachment upload with progress, zero-recipient empty state (queries
  recipient-count before enabling send), delivery-outcome summary after send; every control
  keyboard-reachable with a visible focus ring per platform-rules.md (FR-019, FR-016)
- [X] T038 [P] [US1] Add `web/app/(app)/communications/` API client bindings (generated via
  `openapi-typescript`/`openapi-fetch` per constitution Technology Stack Constraints) — regenerate
  the OpenAPI client after T036 ships

**Checkpoint**: User Story 1 is independently functional and testable (MVP).

---

## Phase 4: User Story 2 - Parent receives the daily report by email and can unsubscribe (Priority: P1)

**Goal**: Every parent/guardian contact with an email on file receives an automatic, per-contact,
per-locale daily report email once per day at 19:00 Europe/Brussels, and can unsubscribe/
re-subscribe via a no-login signed link.

**Independent Test**: Seed a child with two guardian contacts in different locales, run the
`send-daily-reports` CLI command, confirm two independent locale-correct emails; unsubscribe one
contact via the link, re-run the command, confirm only that contact is skipped while the other
guardian and every other household still receive theirs.

### Tests for User Story 2 ⚠️

- [ ] T039 [P] [US2] Integration test: `send-daily-reports` sends one independent email per
  guardian contact of a child, in each contact's own locale (User Story 2 Scenario 1) in
  `backend/ChildCare.Api.Tests/Cli/SendDailyReportsCommandTests.cs`
- [ ] T040 [P] [US2] Integration test: a child with zero events that day still receives an email
  whose body clearly reads "no updates logged today" (Scenario 2) in
  `backend/ChildCare.Api.Tests/Cli/SendDailyReportsCommandTests.cs`
- [ ] T041 [P] [US2] Integration test: a digest-unsubscribed contact is skipped by
  `send-daily-reports`, while every other contact (including another guardian of the same child)
  still receives theirs (Scenario 3/6) in
  `backend/ChildCare.Api.Tests/Cli/SendDailyReportsCommandTests.cs`
- [ ] T042 [P] [US2] Integration test: `POST /api/email/unsubscribe` sets
  `Contact.DigestUnsubscribedAt`, is idempotent on a repeated call with the same token (FR-020),
  and `POST /api/email/resubscribe` clears it, resuming future digests in
  `backend/ChildCare.Api.Tests/Email/UnsubscribeEndpointsTests.cs`
- [ ] T043 [P] [US2] Integration test: an invalid/unresolvable `org` slug, and separately an
  invalid/tampered `token` against a valid `org`, both return a calm "not valid" result, never a
  raw error (FR-018); a `token` valid for tenant A's contact posted with tenant B's `org` also
  fails closed rather than resolving to tenant B's data (constitution Principle I) in
  `backend/ChildCare.Api.Tests/Email/UnsubscribeEndpointsTests.cs`
- [ ] T044 [P] [US2] Integration test: `send-daily-reports` isolates a single tenant's failure —
  one organisation's exception does not block the rest (matches
  `SendPaymentRemindersCommand`'s existing per-tenant isolation) in
  `backend/ChildCare.Api.Tests/Cli/SendDailyReportsCommandTests.cs`
- [ ] T045 [P] [US2] Integration test: a child's daily-report email respects that child's own
  `Contract.Consent.PhotosInternal` flag independently, even when one contact has children with
  differing consent states (Edge Cases) in
  `backend/ChildCare.Api.Tests/Cli/SendDailyReportsCommandTests.cs`

### Implementation for User Story 2

- [X] T046 [P] [US2] `daily-report.scriban` content template (naps/bottles/diaper/mood/
  activities from `DailySummaryResponse`, "no updates logged today" branch, unsubscribe link
  footer built with `org` slug + token per T046a) in
  `backend/ChildCare.Infrastructure/Email/Templates/daily-report.scriban`
- [X] T046a [US2] Add `BuildUnsubscribeUrl`/`BuildResubscribeUrl` to `AuthLinkBuilder`-style link
  builder (reuses the exact `?token=...&org={organisationSlug}` query-string shape
  `AuthLinkBuilder.BuildResetUrl` already uses, per research.md R5 — found and corrected during
  `/speckit-analyze`) in `backend/ChildCare.Application/Email/EmailLinkBuilder.cs` (depends on T021)
- [X] T047 [US2] `UnsubscribeDigestCommand`/`ResubscribeDigestCommand`/Handlers — each takes
  `(OrganisationSlug, Token)`, resolves the tenant schema via `OrganisationSlugResolver` →
  `ITenantDbContextResolver.ForSchema` **first** (mirrors `ResetPasswordCommandHandler`'s exact
  shape — this is not a same-request `ITenantDbContext` injection, since these are public routes
  with no `TenantMiddleware`-resolved schema), then verifies the token via
  `IUnsubscribeTokenService` and toggles `Contact.DigestUnsubscribedAt` within that schema,
  idempotently, in `backend/ChildCare.Application/Email/UnsubscribeDigestCommand.cs` and
  `backend/ChildCare.Application/Email/ResubscribeDigestCommand.cs` (depends on T022, T046a)
- [X] T048 [US2] Implement `GET /api/email/unsubscribe` (server-rendered minimal HTML page, no
  auth, dispatches a query built the same way as T047's commands to read current subscription
  state before rendering), `POST /api/email/unsubscribe`, `POST /api/email/resubscribe` — every
  handler goes through MediatR per constitution Principle III (thin endpoints; schema resolution
  plus token verification is multi-step business logic, not a "simple single-entity lookup"
  carve-out) in `backend/ChildCare.Api/Endpoints/EmailEndpoints.cs` (depends on T047)
- [ ] T049 [US2] `SendDailyReportsCommand.RunAsync` — mirrors `SendPaymentRemindersCommand`'s
  tenant-loop structure exactly (iterate `Tenant` rows with `ProvisioningStatus.Ready` via
  `ITenantDbContextResolver.ForSchema`, isolate per-tenant failures, per-tenant
  `Console.WriteLine` summary, non-zero exit on any failure); per tenant, per enrolled child, per
  contact with `Email != null` and `DigestUnsubscribedAt == null`, renders via
  `GetDailySummaryQuery` and sends via `IEmailSender.SendDailyReportAsync` in
  `backend/ChildCare.Api/Cli/SendDailyReportsCommand.cs` (depends on T046, T017)
- [ ] T050 Add `send-daily-reports` CLI subcommand branch (same early-exit shape as
  `send-payment-reminders`) in `backend/ChildCare.Api/Program.cs` (depends on T049)
- [ ] T051 [US2] Add `google_cloud_run_v2_job.send_daily_reports` and
  `google_cloud_scheduler_job.send_daily_reports_daily` (`schedule = "0 19 * * *"`,
  `time_zone = "Europe/Brussels"`, `max_retries = 0`), mirroring
  `send_payment_reminders`/`send_payment_reminders_daily` exactly, in `infra/gcp/main.tf`
  (depends on T050)

**Checkpoint**: User Stories 1 AND 2 both work independently.

---

## Phase 5: User Story 3 - Director/caregiver triggers an on-demand daily-report resend (Priority: P2)

**Goal**: A director or caregiver can resend one child's daily report email immediately,
independent of the automatic digest and unaffected by digest-unsubscribe state.

**Independent Test**: Unsubscribe a contact from the digest, then trigger an on-demand resend for
that contact's child, and confirm delivery despite the unsubscribe flag.

### Tests for User Story 3 ⚠️

- [ ] T052 [P] [US3] Integration test: `POST /api/email/daily-report/{childId}/resend` delivers
  to every contact with an email on file, including a digest-unsubscribed contact (FR-009,
  Scenario 2) in `backend/ChildCare.Api.Tests/Email/DailyReportResendEndpointTests.cs`
- [ ] T053 [P] [US3] Integration test: resend is available to both `DirectorOnly` and
  `StaffOrDirector` (caregiver) callers, and 404s for a `childId` outside the caller's tenant in
  `backend/ChildCare.Api.Tests/Email/DailyReportResendEndpointTests.cs`

### Implementation for User Story 3

- [ ] T054 [US3] `ResendDailyReportEmailCommand`/Handler (reuses `GetDailySummaryQuery` +
  `IEmailSender.SendDailyReportAsync`, no unsubscribe check) in
  `backend/ChildCare.Application/Email/ResendDailyReportEmailCommand.cs`
- [ ] T055 [US3] Implement `POST /api/email/daily-report/{childId}/resend`
  (`StaffOrDirector`) in `backend/ChildCare.Api/Endpoints/EmailEndpoints.cs` (depends on T054)
- [ ] T056 [US3] Add a "Resend daily report by email" action (sent/failed confirmation) to the
  existing per-child screen in `mobile/app/(app)/child/[id].tsx`, calling T055's endpoint via
  `mobile/services/childEvents.ts`

**Checkpoint**: User Stories 1, 2, AND 3 all work independently.

---

## Phase 6: User Story 4 - Closure and announcement notifications gain an email channel (Priority: P2)

**Goal**: Publishing a closure day (011) or sending an announcement (013) additionally emails
every resolved contact with an email on file, alongside the existing in-app/push notification,
regardless of digest-unsubscribe state and without requiring an active parent-app account.

**Independent Test**: Publish a closure day and send an announcement in a tenant with contacts
spanning subscribed/unsubscribed/no-email/no-app-account states, and confirm both actions email
every contact with an address on file.

### Tests for User Story 4 ⚠️

- [ ] T057 [P] [US4] Integration test: publishing a `KdvClosureDay` emails every resolved
  contact with an email on file, including a contact with no parent-app account
  (`TenantUserId == null`, previously unreachable by the existing push-only fan-out) and a
  digest-unsubscribed contact (FR-010) in
  `backend/ChildCare.Api.Tests/ClosureCalendar/ClosureNotificationEmailTests.cs`
- [ ] T058 [P] [US4] Integration test: `SendAnnouncementCommand` emails every resolved contact
  with an email on file under the same conditions as T057 (FR-011) in
  `backend/ChildCare.Api.Tests/Announcements/SendAnnouncementEmailTests.cs`
- [ ] T059 [P] [US4] Integration test: a bad/bounced address in either fan-out is logged and does
  not block the rest of the batch (FR-012, matches User Story 1's partial-failure behavior) in
  `backend/ChildCare.Api.Tests/ClosureCalendar/ClosureNotificationEmailTests.cs`

### Implementation for User Story 4

- [X] T060 [P] [US4] `closure-notification.scriban` content template (published/cancelled
  variants, reusing `ClosureNotificationService.Labels`' existing NL/FR/EN copy as the template
  model) in `backend/ChildCare.Infrastructure/Email/Templates/closure-notification.scriban`
- [X] T061 [P] [US4] `announcement.scriban` content template (reusing
  `SendAnnouncementCommandHandler`'s subject/body verbatim) in
  `backend/ChildCare.Infrastructure/Email/Templates/announcement.scriban`
- [ ] T062 [US4] Extend `ClosureNotificationService.NotifyAsync` to additionally call
  `IEmailSender.SendClosureNotificationEmailAsync` per resolved recipient with
  `Contact.Email != null` (no `TenantUserId` gate — research.md R4), logging
  `BulkEmailDeliveryStatus`-shaped outcomes without a new `BulkEmailSend` row (data-model.md) in
  `backend/ChildCare.Application/ClosureCalendar/ClosureNotificationService.cs` (depends on T060)
- [ ] T063 [US4] Extend `SendAnnouncementCommandHandler.Handle` to additionally resolve and email
  every contact with `Email != null` in scope (in addition to, not replacing, the existing
  `TenantUserId != null`-gated push/in-app fan-out) via
  `IEmailSender.SendAnnouncementEmailAsync` in
  `backend/ChildCare.Application/Announcements/SendAnnouncementCommand.cs` (depends on T061)

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Consistency and hardening across every story.

- [ ] T064 [P] Verify every new director-web/email string uses an i18n key with NL/FR/EN
  translations, zero hardcoded/untranslated text (FR-014, SC-004) across
  `web/app/(app)/communications/`, `backend/ChildCare.Infrastructure/Email/Templates/`
- [ ] T065 [P] Verify no raw provider/SMTP error or stack trace is ever returned in any endpoint
  response (FR-018, constitution Principle VI) across `backend/ChildCare.Api/Endpoints/EmailEndpoints.cs`
- [ ] T066 Run `quickstart.md` end-to-end against a local Mailhog/Mailpit-backed dev environment
  and fix any discrepancy found
- [ ] T067 [P] Update `Workflows/communication.md` to document the new email channel per
  spec.md's Workflow Boundary (governance rule in `workflows.md`: document what changed, why,
  which features affected)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational only — independently testable from US1
  (different endpoints, different CLI entrypoint), though it shares the templating/DI
  infrastructure US1 also uses.
- **User Story 3 (Phase 5)**: Depends on Foundational + reuses `GetDailySummaryQuery`/
  `IEmailSender.SendDailyReportAsync` introduced by US2 (T046, T017) — sequence after US2, or in
  parallel if the template exists first.
- **User Story 4 (Phase 6)**: Depends on Foundational + reuses the recipient-resolution
  no-`TenantUserId`-gate pattern established in US1 (T035) — sequence after US1, or in parallel
  with a shared understanding of R4.
- **Polish (Phase 7)**: Depends on all four user stories.

### Parallel Opportunities

- All Setup tasks (T001–T004) run in parallel.
- Foundational tasks marked [P] (T005–T008, T013–T014, T019, T021, T024) run in parallel within
  their dependency layer.
- Once Foundational completes, US1 and US2 can be staffed in parallel; US3 and US4 follow once
  their respective shared pieces (T046/T017 for US3; T035's pattern for US4) exist.
- All tests within a story marked [P] run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (blocks everything).
3. Complete Phase 3: User Story 1 — bulk email is independently demoable.
4. **STOP and VALIDATE**: run quickstart.md's bulk-send scenarios.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 (bulk email) → demo/deploy.
3. US2 (daily digest + unsubscribe) → demo/deploy — this is the BACKLOG's primary motivating
   scenario ("emailed version of the child daily report"), so don't stop at US1 alone.
4. US3 (on-demand resend) → demo/deploy.
5. US4 (closure/announcement email fan-out) → demo/deploy — closes the "email notification
   fallback" gap 011 and 013 both explicitly deferred to this feature.
