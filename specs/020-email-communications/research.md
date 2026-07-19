# Research: Email Communications

All unknowns from `plan.md`'s Technical Context are resolved below. Each decision cites the
existing codebase precedent it follows, per this project's "match existing idiom over inventing a
new one" convention (see 018/013/011's own research/plan artifacts).

## R1: Email templating mechanism

**Decision**: Scriban, rendering into a single shared HTML layout partial (header/footer,
inline-CSS for email-client compatibility) plus one Scriban content template per email kind
(bulk, daily-digest, closure, announcement). Locale-specific fixed copy (subject lines, button
labels, section headers) is supplied via a small per-kind `Labels`-keyed-by-locale dictionary —
the same idiom `SendAnnouncementCommandHandler`/`ClosureNotificationService`/
`PaymentReminderNotificationService` already use for push copy — passed into the template as
model data, not hardcoded inside the `.scriban` file. Templates live as embedded resources under
`backend/ChildCare.Infrastructure/Email/Templates/`.

**Rationale**: The constitution (Principle VII) commits this backend to Minimal APIs with no
Controllers — pulling in a Razor Class Library means standing up `IRazorViewEngine`/
`RazorViewToStringRenderer` machinery designed for an MVC pipeline this project deliberately
doesn't have, for a single email-rendering use case. Scriban is a small, sandboxed (no arbitrary
.NET reflection from template code), actively maintained templating library with no ASP.NET MVC
dependency — it plugs into a plain `string Render(model)` call from any handler or CLI command,
which is exactly this feature's actual need (render one HTML string from a model + locale).
Embedded HTML with manual placeholder substitution (the third option `plan.md`'s prompt named)
was rejected because this feature explicitly replaces that pattern (today's raw-string-literal
`EmailService`) — reintroducing a hand-rolled placeholder scheme would just be the same anti-
pattern with a different syntax, not a real templating mechanism.

**Alternatives considered**:
- Razor Class Library — rejected per Principle VII (no Controllers/MVC infra in this backend);
  disproportionate weight for one rendering use case.
- Embedded HTML + manual placeholder substitution — rejected as not materially different from
  the raw-string-literal pattern this feature exists to retire.
- Handlebars.NET — a reasonable alternative to Scriban with similar sandboxing properties; not
  chosen only because Scriban is .NET-native (no JS-runtime-inspired quirks), has no additional
  transitive dependencies, and is already a common choice in comparable .NET Minimal API projects.

## R2: Recurring daily-digest job mechanism

**Decision**: Reuse feature 014a's existing Cloud Scheduler → Cloud Run Job pattern exactly. Add
a new `send-daily-reports` CLI subcommand to `backend/ChildCare.Api/Program.cs` (same early-exit
shape as `migrate-tenants`/`backfill-growth-check`/`grant-platform-admin`/
`send-payment-reminders`), backed by a `SendDailyReportsCommand.RunAsync` in
`backend/ChildCare.Api/Cli/` that mirrors `SendPaymentRemindersCommand`'s tenant-loop structure:
iterate every `Tenant` with `ProvisioningStatus.Ready` via `ITenantDbContextResolver.ForSchema`,
isolate per-tenant failures (one organisation's failure must not block the rest — spec.md
Technical Requirements), and return a non-zero exit code if any tenant failed. `infra/gcp/main.tf`
gets a matching `google_cloud_run_v2_job` (`args = ["send-daily-reports"]`) and
`google_cloud_scheduler_job` entry with `schedule = "0 19 * * *"`, `time_zone = "Europe/Brussels"`
(spec.md's clarified 19:00 trigger time), following `send_payment_reminders_daily`'s exact
Terraform shape (same OAuth-authenticated Cloud Run Admin API `:run` invocation, same
`max_retries = 0` — a retried whole-job execution risks double-sending a day's digest to
already-succeeded tenants, matching the reminder job's own reasoning for that setting).

**Rationale**: This is not a new architectural decision — 014a already made and justified this
choice (Cloud Run's scale-to-zero makes an in-process timer/`BackgroundService` unreliable, since
nothing guarantees the container is even running at 19:00) and built the reusable shape. A second
recurring job is the exact scenario that pattern was built to generalize to; introducing a second
mechanism (Hangfire, Quartz, a different scheduler) for this feature alone would fragment the
codebase's job-scheduling story for no benefit. This corrects `spec.md`'s original assumption
(written before this precedent was found) that this feature would introduce the *first* recurring
job — it does not; 014a already did, and this feature is the second consumer of that pattern.

**Alternatives considered**:
- In-process `BackgroundService` with a `PeriodicTimer` — rejected for the same reliability reason
  014a already rejected it (scale-to-zero).
- Hangfire (persisted job queue) — rejected as a new dependency solving a problem (job
  persistence/retry/dashboarding) this feature doesn't have; a single daily trigger with
  per-tenant failure isolation and a next-day retry-by-recurrence is sufficient, matching 014a's
  reasoning.
- A distinct third-party scheduler — no candidate outperforms reusing infrastructure the project
  already pays for (Cloud Scheduler is already enabled via `google_project_service.cloudscheduler`
  from 014a).

## R3: Bulk-email attachment storage

**Decision**: A new `IBulkEmailAttachmentStorage` port (`backend/ChildCare.Application/Common/`)
and `GcsBulkEmailAttachmentStorage` implementation, following `IHealthAttachmentStorage`'s exact
shape (category + subject-id → signed upload/download URL pair via `Google.Cloud.Storage.V1
.UrlSigner`, 15-minute TTL, deterministic object path
`bulk-email-attachments/{bulkEmailSendId}/attachment.{ext}`, no public URLs). Allowed content
types: `application/pdf`, `image/jpeg`, `image/png` (same allow-list as
`CreateHealthRecordAttachmentUploadUrlCommand`). Size cap: 10MB, matching
`UploadGroupActivityPhotoCommand.MaxFileSizeBytes` — the one existing precedent in this codebase
(spec.md Assumptions) — enforced both client-side (upload progress UI) and server-side (the
handler that finalizes the send checks the uploaded object's actual size via the storage port
before attaching it to any outbound email, since a signed upload URL cannot itself cap the byte
count the client sends).

**Rationale**: A new port rather than reusing `IHealthAttachmentStorage` directly, mirroring why
013g/013h introduced dedicated ports per attachment kind rather than one generic "attachment"
port — object paths and calling contexts (tenant scope, subject entity) differ enough that a
shared interface would need conditional category logic the existing per-kind ports avoid.

**Alternatives considered**: Extending `IHealthAttachmentStorage` with a new category parameter —
rejected because that port's method signature is keyed on `healthRecordId : Guid`, semantically
wrong for a `BulkEmailSend` subject; a new port keeps each interface's contract honest about what
kind of thing it stores attachments for.

## R4: Bulk/digest email recipient resolution — no `TenantUserId` gate

**Decision**: Email recipient resolution for bulk sends, the daily digest, and the
closure/announcement email fan-out queries `ChildContact` → `Contact` filtered only on
`Contact.Email != null` (and, for the digest specifically, `DigestUnsubscribedAt == null`) — it
does **not** filter on `Contact.TenantUserId != null` the way `SendAnnouncementCommandHandler`'s
existing push/in-app fan-out does (FR-008 there explicitly bounds reach to contacts with an
active parent-app account, since in-app `Notification` rows and Expo push both require one).

**Rationale**: Email is reachable by anyone with an address on file, independent of whether that
contact ever accepted a parent-app invitation — that's the entire point of adding email as a
*fallback* channel (per 011/013's own deferred "email notification fallback" framing) rather than
just widening the existing push channel. Silently reusing the announcement/closure recipient
query as-is would under-deliver: a family that never installed the app would receive neither the
existing push nor the new email, defeating this feature's purpose. This is the one place this
feature's recipient resolution must diverge from the existing precedent it otherwise closely
follows, and is called out explicitly here so `tasks.md`/implementation doesn't silently copy the
`TenantUserId` filter along with the rest of the query shape.

## R5: Unsubscribe token AND tenant-schema resolution

**Decision**: This link must solve two problems, not one — the codebase's schema-per-tenant model
(constitution Principle I) means a `Contact.Id` alone, on a public unauthenticated route with no
JWT `tenant_id` claim, does not tell the handler which tenant schema to query. This is not a new
problem: `ResetPasswordCommand`/`VerifyEmailCommand` (feature 003) already solved it exactly the
same way, and this feature reuses that solution rather than inventing a second one (an earlier
draft of this research missed this and was corrected during `/speckit-analyze`).

The unsubscribe/re-subscribe link is `/api/email/unsubscribe?token=...&org={organisationSlug}`
(mirrors `AuthLinkBuilder.BuildResetUrl`'s exact query-string shape). `org` is the tenant's
existing public `Tenant.Slug` — not secret, and deliberately not part of the signed payload
(matching `OrganisationSlugResolver`'s existing "slugs aren't secret" reasoning). The handler:

1. Resolves the tenant via `OrganisationSlugResolver.ResolveAsync(organisationSlug)` →
   `ITenantDbContextResolver.ForSchema(tenant.SchemaName)` (identical first step to
   `ResetPasswordCommandHandler`).
2. Verifies `token` via `IDataProtector.Unprotect` against a payload of
   `{ ContactId, Purpose: "digest-unsubscribe" }` (fails closed on tampering/wrong purpose/wrong
   schema — a token generated for tenant A's contact, replayed against tenant B's `org` slug,
   fails because tenant B's schema has no `Contact` with that id, not because the token itself
   encodes a tenant check).
3. Loads `Contact` by the decoded id **within that resolved schema** and toggles
   `DigestUnsubscribedAt`.

No token is persisted — verification is a pure `Unprotect` call plus a schema-scoped id lookup.
The same token is safe to embed in every digest email indefinitely (no expiry set) because its
only possible effect is toggling `DigestUnsubscribedAt`, an operation FR-020 already requires to
be idempotent in both directions — a replayed or bookmarked link is harmless by construction, so
there is no need to invalidate it after first use (which would otherwise force a *new* signed
link into every subsequent digest just to remain useful, adding complexity for no security
benefit given the action it gates is non-destructive and reversible).

**Rationale**: Reusing `AuthLinkBuilder`/`OrganisationSlugResolver`'s exact existing pattern for
"a public link needs to resolve tenant + subject before doing anything" avoids inventing a second
mechanism for a problem this codebase already solved once — and avoids the much worse alternative
of a handler that has to search every tenant schema for a matching token, which would be both
slow (linear in tenant count on every unsubscribe click) and a bad precedent for how public,
schema-per-tenant links should work generally. `IDataProtector` still satisfies FR-007's "signed,
single-purpose... not guessable/enumerable" requirement (Technical Requirements — Security)
without introducing a new signing dependency (JWT library, custom HMAC key management) or a
database table purely to track unsubscribe tokens.

**Alternatives considered**: A persisted, single-use token row (invalidated after first use) —
rejected because "single-use" actively works against FR-007's requirement that unsubscribing
"stops receiving the daily digest until they re-subscribe" via presumably the *same* class of
link (a parent re-visiting an old digest email to re-subscribe later should not find a dead link).
A raw unsigned `contactId` in the URL — rejected outright per the "not guessable/enumerable"
requirement; a sequential or otherwise-known contact ID would let anyone unsubscribe an arbitrary
contact. Encoding the tenant schema name *inside* the signed token instead of a separate `org`
query param — rejected only because it breaks from `AuthLinkBuilder`'s established convention for
no real benefit; the org slug isn't secret either way, so there's nothing gained by hiding it
inside the protected payload instead of alongside it.

## R6: Per-recipient delivery outcome tracking

**Decision**: A `BulkEmailRecipient` row per resolved recipient of a `BulkEmailSend` (mirrors
`AnnouncementRecipient`/`ClosureNotificationDelivery`'s existing per-recipient audit-row
pattern), with a `Status` enum (`Sent`, `SkippedNoEmail`, `ProviderFailure`) and a nullable
`Error` (exception type name only, never the raw provider message, matching
`ClosureNotificationDelivery.Error`'s existing convention of not leaking provider internals). The
director's post-send summary (SC-001, FR-012) aggregates these rows by status. The daily digest
and closure/announcement fan-out reuse the same per-recipient status shape internally for logging
and partial-failure handling but do not need a director-facing summary UI for those paths (011/013
already have their own existing outcome surfaces this feature doesn't need to duplicate).

**Rationale**: Directly reuses two already-shipped, reviewed patterns
(`AnnouncementRecipient`, `ClosureNotificationDelivery`) rather than inventing a third shape for
substantially the same problem (audit which contacts got what, with which outcome).

## R7: Photo consent in the daily digest email

**Decision**: No new consent-check code — the daily digest email is rendered from
`GetDailySummaryQuery`'s existing response (feature 009/013), which already gates group-activity
photos behind `Contract.Consent.PhotosInternal` per child before the response is built. The email
template simply renders whatever the query returns; it never re-derives or overrides consent.

**Rationale**: Avoids a second, potentially-divergent consent check living in the email-rendering
path — the existing query is the single source of truth for "what is this child's daily summary,"
and the parent-app screen (feature 013) already proves that gating correct. Duplicating the logic
risks the two surfaces silently disagreeing after a future change to one but not the other.

## R8: i18n resource ownership for this feature's new director-web screen

**Decision**: The new director-web compose screen's own UI strings (labels, buttons, the
delivery-outcome summary) follow the existing frontend i18n convention exactly —
`web/i18n/locales/{nl,fr,en}.json` keys consumed via `next-intl`, no new mechanism. This is
separate from R1 (server-rendered email body templates), which is net-new because no backend
per-locale email rendering existed before this feature; the *director-web UI* i18n path already
exists and this feature is just one more consumer of it, like every prior director-web feature.
