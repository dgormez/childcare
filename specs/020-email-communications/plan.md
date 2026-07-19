# Implementation Plan: Email Communications

**Branch**: `020-email-communications` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/020-email-communications/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See
`.specify/templates/plan-template.md` for the execution workflow.

## Summary

Replace `EmailService`'s raw-string-literal, English-only email templates with a real,
locale-aware templating mechanism (Scriban — R1), and build the three new email capabilities the
BACKLOG describes on top of it: a director-web bulk-email compose flow (location/group scope,
one email per household, optional GCS-backed attachment), an automatic once-daily child
daily-report digest email (reusing feature 013's existing `GetDailySummaryQuery`, respecting a
per-contact, no-login unsubscribe flag), and an email channel added to the existing
closure-day (011) and announcement (013) send flows. The daily digest's scheduling reuses feature
014a's existing Cloud Scheduler → Cloud Run Job pattern (R2) rather than introducing new
job-queue infrastructure.

## Technical Context

**Language/Version**: .NET 10 / C# (backend); TypeScript, Next.js 15 App Router (director web).

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9, MailKit/MimeKit (existing
`EmailService`), Scriban (new — R1), `Google.Cloud.Storage.V1` (existing GCS signed-URL pattern,
R3), ASP.NET Core Data Protection (`IDataProtector`, first-party, new use — R5).

**Storage**: PostgreSQL 16 (schema-per-tenant) — new `BulkEmailSend`/`BulkEmailRecipient` tables,
new `Contact.DigestUnsubscribedAt` column. GCP Cloud Storage for bulk-email attachments (signed
URLs only, no public blobs).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (constitution Principle V) for
handler/integration tests; happy path plus key negative flows per spec.md's Testing
Requirements (household de-duplication, partial-failure tolerance, unsubscribe idempotency,
locale-correct rendering, tenant isolation).

**Target Platform**: Cloud Run (scale-to-zero) for the API service; a second Cloud Run Job
(`send-daily-reports`) triggered by Cloud Scheduler, mirroring feature 014a's
`send-payment-reminders` job exactly (R2).

**Project Type**: Web application — `backend/` (ASP.NET Core Minimal API monolith, five
projects per constitution Principle VII) + `web/` (Next.js director admin). No `mobile/`/
`parent-mobile/` change (spec.md Cross-Platform Impact: parents interact via email, not an app
screen; the caregiver on-demand resend is one new action on an existing screen).

**Performance Goals**: Bulk sends and the daily digest tolerate 100+-recipient scopes without
blocking a single request/job tick (FR-015) — batched dispatch within the handler/CLI command,
not a new queue technology (R2's rationale).

**Constraints**: No raw provider error/stack trace ever surfaces to a director or parent
(FR-018, constitution Principle VI). Every email respects tenant boundaries (FR-013,
constitution Principle I). Attachment size cap 10MB, content-type allow-list PDF/JPEG/PNG (R3).
Unsubscribe requires no login (FR-007).

**Scale/Scope**: Per-tenant recipient counts in the tens-to-low-hundreds (typical KDV location
size); the digest job iterates every `Ready` tenant schema (matches `SendPaymentRemindersCommand`'s
existing scale).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Multi-Tenant Isolation (NON-NEGOTIABLE)** — PASS. Every authenticated endpoint goes
  through `ITenantDbContext`; the `send-daily-reports` CLI command uses
  `ITenantDbContextResolver.ForSchema` per tenant exactly like `send-payment-reminders` already
  does (no ambient-context risk, since it runs outside any single request). The public
  unsubscribe/re-subscribe endpoints have no JWT `tenant_id` claim to resolve a schema from
  (found and corrected during `/speckit-analyze` — an earlier draft of this plan incorrectly
  assumed a `Contact.Id` alone was enough); they instead reuse feature 003's existing
  `ResetPasswordCommand`/`VerifyEmailCommand` pattern exactly: the link carries the tenant's
  public `Tenant.Slug` as an `org` query param, resolved via `OrganisationSlugResolver` →
  `ITenantDbContextResolver.ForSchema` *before* the signed token is verified and the `Contact` is
  looked up within that resolved schema (R5). This is not a new carve-out — it's the same
  already-reviewed mechanism this codebase uses for every other public, schema-per-tenant link,
  applied here for the first time outside `Auth`.
- **II. Regulatory Compliance by Design (NON-NEGOTIABLE)** — N/A. This feature has no regulatory
  ratio/threshold logic; it's a communication channel, not a compliance enforcement.
- **III. CQRS via MediatR & Thin Endpoints** — PASS. Bulk send, attachment upload-URL issuance,
  and on-demand resend are MediatR commands; recipient-count preview is a MediatR query.
  Endpoint files map HTTP ↔ MediatR only. The `send-daily-reports` CLI command is not a MediatR
  request (matches `SendPaymentRemindersCommand`'s existing precedent — a CLI entrypoint that
  itself calls into application-layer services, not an HTTP-triggered flow), consistent with how
  this codebase already treats its one existing scheduled job.
- **IV. Internationalization First (NON-NEGOTIABLE)** — PASS, and this feature is what makes it
  true for email specifically (R1) — today's `EmailService` is the one hardcoded-English
  exception to this principle anywhere in the codebase; this feature closes that gap rather than
  extending it. Director-web UI strings follow the existing `next-intl`/`web/i18n/locales/*.json`
  convention (R8).
- **V. Test with Real Infrastructure (NON-NEGOTIABLE)** — PASS. Handler/integration tests run
  against TestContainers PostgreSQL per existing convention; no InMemory provider introduced.
- **VI. Secure Configuration & Storage** — PASS. No new secrets hardcoded (SMTP credentials
  already externalized via `Email:*` config, unchanged). Attachments use signed, time-limited GCS
  URLs, no public blobs (R3). No raw provider errors ever surface to a user (FR-018).
- **VII. Monolith-First Simplicity** — PASS. No new deployable/service — the digest job is a
  second CLI subcommand inside the same `ChildCare.Api` container image (R2), not a new project.
  Scriban is a templating *library* dependency within `ChildCare.Infrastructure`, not a new
  service boundary.

No violations; Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/020-email-communications/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── email-communications-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── Contact.cs                      # extended: DigestUnsubscribedAt
│   │   ├── BulkEmailSend.cs                 # new
│   │   └── BulkEmailRecipient.cs            # new
│   └── Enums/
│       └── BulkEmailDeliveryStatus.cs       # new
├── ChildCare.Application/
│   ├── Common/
│   │   ├── IEmailSender.cs                  # extended: new templated send methods
│   │   ├── IBulkEmailAttachmentStorage.cs   # new (R3)
│   │   └── IEmailTemplateRenderer.cs        # new (R1) — Scriban wrapper port
│   ├── Email/
│   │   ├── SendBulkEmailCommand.cs          # new
│   │   ├── GetBulkEmailRecipientCountQuery.cs # new
│   │   ├── CreateBulkEmailAttachmentUploadUrlCommand.cs # new
│   │   ├── ResendDailyReportEmailCommand.cs # new
│   │   ├── UnsubscribeDigestCommand.cs      # new
│   │   └── ResubscribeDigestCommand.cs      # new
│   ├── Announcements/
│   │   └── SendAnnouncementCommand.cs       # extended: email fan-out (FR-011)
│   └── ClosureCalendar/
│       └── ClosureNotificationService.cs    # extended: email fan-out (FR-010)
├── ChildCare.Infrastructure/
│   ├── Email/
│   │   ├── ScribanEmailTemplateRenderer.cs  # new (R1)
│   │   └── Templates/                       # new — .scriban embedded resources
│   ├── Storage/
│   │   └── GcsBulkEmailAttachmentStorage.cs # new (R3)
│   └── Persistence/Migrations/              # new migration
├── ChildCare.Api/
│   ├── Cli/
│   │   └── SendDailyReportsCommand.cs       # new (R2, mirrors SendPaymentRemindersCommand)
│   ├── Endpoints/
│   │   └── EmailEndpoints.cs                # new
│   └── Program.cs                           # extended: send-daily-reports subcommand + DI

web/
└── app/(app)/communications/                # new director-web compose screen
    └── page.tsx

infra/gcp/
└── main.tf                                  # extended: send-daily-reports Cloud Run Job + Cloud Scheduler entry (R2)
```

**Structure Decision**: Standard web-application layout already established by every prior
feature — `backend/` (5-project ASP.NET Core monolith) + `web/` (Next.js director admin). No new
top-level directory. The new director-web screen lives under the existing `(app)` route group
alongside `dashboard`/`announcements`, matching how prior director-facing features extend the
app shell rather than introducing a new one.

## Complexity Tracking

*No Constitution Check violations — this section is not needed.*
