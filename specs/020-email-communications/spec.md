# Feature Specification: Email Communications

**Feature Branch**: `020-email-communications`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Build templated email delivery on top of the existing
notification infrastructure — bulk emails to parents and an emailed version of the child daily
report. Bulk parent email (director selects location + optional group, composes subject/body,
one email per family household not per child, optional PDF/image attachment via GCS signed URL).
Daily report email (the same aggregated daily summary shown in the parent app, emailed
automatically once per day at end of day to every parent/guardian contact independently,
default-on/opt-out via a no-login unsubscribe link, plus an on-demand resend unaffected by
unsubscribe state). Closure day notifications (011) and announcements (013) gain an email
channel reusing the same bulk-send mechanism. Replace the current raw-C#-string-literal email
templates with a real templating approach that renders NL/FR/EN per recipient locale."

## Product Context

### Feature Type

Mixed — API-backend capability (bulk-send endpoint, daily-digest scheduled job, unsubscribe
endpoint), User-facing UI (director web compose screen), and Background process (a second
scheduled job reusing feature 014a's Cloud Scheduler + Cloud Run Job pattern — see Technical
Requirements and Assumptions).

### Primary Consumer

Director (composes bulk emails; closure/announcement flows gain an email channel director
already triggers). Parent/guardian contact (receives the daily report email and uses the
no-login unsubscribe link). Caregiver (can trigger an on-demand daily-report resend for a single
child from the existing per-child screen, `mobile/app/(app)/child/[id].tsx`, that already shows
that child's event timeline — no caregiver-facing daily-summary *view* exists yet, only the
resend action is added here).

### Workflow Boundary

**Parent Communication** (`workflows.md` → `Workflows/communication.md`) — this feature adds an
email channel alongside the existing in-app `Notification` + Expo push channel that
`SendAnnouncementCommand` (feature 013) and `ClosureNotificationService` (feature 011) already
use, and adds a new daily-digest email built from feature 013's existing
`GetDailySummaryQuery`/`GetParentDailySummaryQuery` aggregation (itself sourced from
`child_events`, feature 009). `Workflows/communication.md` is updated as part of this feature to
document the email channel.

**Actors**: Director (composes bulk/location/group emails; both 011 and 013's existing send
flows keep their own trigger points, now fanning out to email too). Caregiver (on-demand daily
report resend for one child). Parent/guardian contact (recipient; can unsubscribe from the daily
digest without logging in). System (runs the once-daily digest job; resolves recipients per
tenant/location/group scope; tolerates and logs partial delivery failure).

**Actions**: Director selects a location (+ optional group) → composes subject/body → optionally
attaches one PDF/image via a GCS signed upload → sends → sees a delivery outcome summary
(skipped/no-email contacts, provider-reported failures). Closure publish/cancel (011) and
announcement send (013) each additionally enqueue an email per resolved recipient contact,
reusing this feature's household-collapsing and partial-failure-tolerant send path. Once daily, at a fixed
19:00 Europe/Brussels trigger time, the system emails every parent/guardian contact with an
email on file and no active digest-unsubscribe, for each of their children, independently per
contact. A contact can
unsubscribe/re-subscribe via a signed link with no login. A director or caregiver can trigger an
on-demand resend of one child's daily report to its contacts regardless of digest-unsubscribe
state.

**Data Flow**: `Location`/`Group`/`ChildGroupAssignment`/`Child`/`ChildContact`/`Contact` →
recipient resolution (tenant-scoped, household-collapsed by contact) → template rendering
(locale from `Contact.Locale`) → `IEmailSender` → provider (MailKit/SMTP, existing
configuration). Daily digest additionally reads `GetDailySummaryQuery`'s aggregation per child.
Bulk-email attachments flow through a new GCS signed-URL upload endpoint (same pattern as
`IHealthAttachmentStorage`/`IGroupActivityPhotoStorage`) before being attached to the outbound
MIME message.

**Outputs**: Delivered emails (bulk, daily digest, closure, announcement) in the recipient's
locale; a director-facing delivery outcome summary after a bulk send; a persisted
digest-unsubscribe flag per contact; an audit trail sufficient to show which contacts were
skipped or failed and why.

**Cross-Platform Impact**: Director web (new compose screen/flow). Backend-only for the digest
job, unsubscribe endpoint, and the closure/announcement email fan-out. No caregiver tablet
change beyond an existing daily-summary screen gaining a "resend by email" action. No parent
mobile app change — parents interact with this feature entirely through their email client, not
the app.

### User Impact

This enables a director to reach every enrolled family at a location (or group) by email for
one-off communications, and lets every parent/guardian contact receive the same daily
reassurance they'd see in the parent app directly in their inbox — resulting in fewer missed
updates and one more reassurance channel that doesn't require opening the app.

### UX Requirements

**Persona**: Director — web, desktop-first, high density, per `platform-rules.md`'s Director Web
App section — composing and sending bulk email. Parent/guardian contact — receiving email, which
must read warm and reassuring per `platform-rules.md`'s Parent Mobile App section and
`reference-products.md`'s Famly/ClassDojo tone guidance, even though email itself isn't a
in-app-rendered surface.

**Platform**: Director-web for composition (new screen, e.g. `web/app/(app)/communications/`).
Email (HTML, rendered server-side per NL/FR/EN recipient locale) as the surface parents interact
with — no parent-mobile app screen is added or changed.

**User job**: Director — "I need every family at this location (or one group within it) to see
this update/attachment, today, without hunting down each parent's contact info." Parent — "I want
to know how my child's day went without having to remember to open the app."

**Success criteria**:

- A director can compose and send a bulk email to a location or group, including an attachment,
  in under 2 minutes, and see afterward which contacts (if any) were skipped or failed.
- Every parent/guardian contact with an email on file and no active unsubscribe receives that
  day's daily report email once, automatically, by end of day — with no action required.
- A contact can unsubscribe from the daily digest in one click, with no login, and the
  unsubscribe takes effect immediately (next day's digest, not this one if already sent).
- All director-composed and system-generated email UI/copy renders in the recipient's or
  director's selected language (NL/FR/EN) with no untranslated text or key leakage.

**Main flow**: Director opens the new email-communications screen → selects a location (required)
and optionally narrows to one group → composes subject and body → optionally uploads one
attachment (progress shown) → reviews the resolved recipient count (households, not children) →
sends → sees a summary (sent count, skipped-no-email count, provider-failure count, each with the
affected contact names available on request). Closure publish/cancel and announcement send keep
their existing director-facing trigger points (011, 013) — email is an added delivery channel
behind those, not a new screen. The daily digest requires no director action once enabled. A
caregiver, from the existing per-child screen, or a director can trigger "resend by email" for a
child, seeing a simple sent/failed confirmation.

**Loading/empty/error states**: A location or group with zero currently-enrolled children
resolves to zero recipients — the compose screen shows this before send is enabled, as a plain
"no recipients in this scope" message, not a submit-time error. An attachment upload shows
progress and a clear error (with retry) if it exceeds the size cap or has a disallowed content
type. After send, a partial-failure result is a calm summary line ("42 sent, 1 skipped — no email
on file"), never a raw provider error or stack trace surfaced to the director (per `CLAUDE.md`'s
error-handling rule — the full error is logged server-side). A daily digest requested for a child
with zero events that day still sends, with template copy that clearly reads "no updates logged
today" rather than rendering empty sections.

**Accessibility**: The director-web compose screen follows `platform-rules.md`'s Director Web App
section — every control (location/group selector, subject/body fields, attachment upload, send
button) is keyboard-reachable with a visible focus ring. The unsubscribe link/page (the one
parent-facing web surface this feature adds, since it must work without app login) is a simple,
accessible, single-purpose page — no design-system chrome required beyond legible text and a
clear confirm action, since a parent may open it from any device.

**Offline behavior**: Not applicable to the director-web compose screen (assumes connectivity).
Not applicable to email itself (inherently asynchronous/store-and-forward). Not applicable to the
caregiver "resend by email" action (caregiver app is online-first for this action, unlike its
offline-tolerant event-logging flows).

### Technical Requirements

**API impact**: New endpoints — bulk-email compose/send (director-only, tenant- and
location/group-scoped), attachment upload-URL issuance (GCS signed URL, reusing the existing
storage-port pattern), daily-report on-demand resend (director/caregiver, single child), and a
public (no-auth) unsubscribe/re-subscribe endpoint accepting a signed token. `SendAnnouncementCommand`
(013) and `ClosureNotificationService` (011) are extended to additionally call the new
email-send path per resolved recipient, reusing its household-collapsing and partial-failure
handling rather than duplicating recipient resolution.

**Data-model impact**: `Contact` gains a `DigestUnsubscribedAt` (nullable timestamp — null means
subscribed) rather than a bool, so re-subscribing is auditable. A new attachment/upload record
(mirroring the existing `IHealthAttachmentStorage`/`IGroupActivityPhotoStorage` GCS-signed-URL
pattern) backs bulk-email attachments; the attachment belongs to one bulk-send record, not the
tenant's general media library. A new `BulkEmailSend` (or equivalent) record captures scope
(location/group), subject/body, attachment reference, sender, timestamp, and a per-recipient
delivery outcome, giving the director's post-send summary something to read from and providing
an audit trail. Email template content itself: the concrete mechanism is Scriban, chosen at plan
time (`research.md` R1) over a Razor Class Library or embedded-HTML placeholder substitution —
this feature is what makes real templating exist at all (today's `EmailService` is raw C# string
literals, English-only, per `IEmailSender.cs`'s doc comments that had — inaccurately — assigned
that rework to a different, later feature number; see Assumptions). Every email's surrounding
template chrome (headers, footers, section labels, the unsubscribe-link footer text) renders in
the recipient contact's locale; a director's own free-text bulk-email subject/body is sent
verbatim, in the language the director typed it in, never machine-translated — only the chrome
around it is locale-aware. A `Contact.Locale` value outside the supported NL/FR/EN set falls back
to `"nl"`, matching the fallback convention every existing `Labels.TryGetValue(...) ??
Labels["nl"]` call site in this codebase already uses (`SendAnnouncementCommandHandler`,
`ClosureNotificationService`, `PaymentReminderNotificationService`).

**Security considerations**: Every authenticated endpoint is tenant-scoped through the existing
`TenantDbContext`/`DirectorOnly` (or `StaffOrDirector` for the caregiver resend) policy; recipient
resolution never crosses tenant boundaries — this applies identically to all four send paths this
feature adds or extends (bulk, digest, on-demand resend, closure/announcement fan-out), not only
bulk send. A location/group id that does not belong to the calling director's own tenant is
treated as if it does not exist (empty result), never as a leak of another tenant's scope or an
error that reveals the id belongs elsewhere — mirroring feature 018's FR-013 convention for the
same class of cross-tenant scope parameter. `Contact.Email` (non-null) is the sole and permanent
gate for every email send path in this feature; it is independent of `Contact.TenantUserId` —
whether a contact has, has never had, or has had-and-lost a linked parent-app account never
affects email reachability, only whether they also receive in-app notifications/push (see
Technical Requirements — API impact and `research.md` R4 for why this deliberately diverges from
the existing push/in-app recipient-resolution precedent). The unsubscribe endpoint is the one
deliberately unauthenticated surface — its token must be signed, single-purpose (only capable of
toggling that one contact's digest subscription, nothing else), and not guessable/enumerable (no
sequential contact IDs in the URL). Because this is a schema-per-tenant database and this
endpoint has no JWT `tenant_id` claim to resolve a schema from, the unsubscribe/re-subscribe link
carries the tenant's public organisation slug alongside the signed token (reusing the existing
`ResetPasswordCommand`/`VerifyEmailCommand` pattern from feature 003 exactly — see
`research.md` R5), so the handler resolves the correct tenant schema before looking up the
token's contact within it, rather than searching every tenant schema or assuming a schema that
isn't there. The token never expires and is safe to reuse indefinitely (embedded in every digest
email sent while a contact remains subscribed) because both directions of the toggle it gates
(unsubscribe and re-subscribe) are idempotent (FR-020) — re-activating an already-applied token,
in either direction, succeeds silently with no error, matching the already-in-that-state outcome
a stale or reused link should produce. Attachment uploads validate
content-type (PDF/JPEG/PNG, matching the existing health/vaccine attachment allow-list) and
enforce a size cap (10MB, matching the one existing precedent in
`UploadGroupActivityPhotoCommand`) before issuing a signed upload URL. A send-path failure for one
recipient (an invalid address, a provider rejection) is attempted once per send and recorded as
that recipient's outcome — never retried automatically within the same send — since the next
scheduled digest or a director-initiated resend is already this feature's natural retry path
(FR-012); this applies to every send path (bulk, digest, closure, announcement), not only bulk.

**Performance considerations**: Bulk sends and the daily digest are batched/queued rather than
sent synchronously within a single request/job tick — a 100+ family location must not block a
director's request or the digest job on serial SMTP round-trips. The daily digest reuses this
codebase's existing scheduled-job infrastructure: feature 014a already introduced a
Cloud Scheduler → Cloud Run Job (a `dotnet run --` CLI subcommand, e.g. `send-payment-reminders`,
early-exiting before the normal web host starts) pattern precisely because Cloud Run's
scale-to-zero makes an in-process timer/`BackgroundService` unreliable. This feature's daily
digest follows the same shape (a new CLI subcommand, e.g. `send-daily-reports`, plus a matching
`google_cloud_run_v2_job`/`google_cloud_scheduler_job` pair in `infra/gcp/main.tf`) rather than
introducing a new job-queue dependency (Hangfire, Quartz) for a second recurring job.

**Testing requirements**: Happy path per entry point (bulk send reaches every enrolled household
once, not per child; daily digest sends once per subscribed contact with correct per-locale
content; unsubscribe/re-subscribe round-trips correctly and is idempotent; closure/announcement
email fan-out reuses the same recipient/partial-failure handling as bulk send). Key negative/edge
flows: a contact with no email is skipped and logged, siblings don't produce duplicate emails, an
unsubscribed contact still receives bulk/announcement/closure emails and on-demand resends
(only the daily digest respects the flag), a zero-recipient scope is a no-op not an error, a
tenant-A director/job never reaches a tenant-B contact.

## Clarifications

No `[NEEDS CLARIFICATION]` markers were needed. Existing precedent — 013's
`SendAnnouncementCommand` recipient resolution and `Labels`-dictionary locale idiom, 011's
`ClosureNotificationDelivery` partial-failure/audit pattern, 013g/013h's GCS-signed-URL
attachment convention, 018's read-model-not-new-schema instinct — supplied reasonable defaults
for every real ambiguity in the BACKLOG prompt. See Assumptions for the scope calls those
defaults required, including the two genuinely new pieces of design surface this feature
introduces (a templating mechanism and the system's first recurring job), both explicitly
deferred to `plan.md` by the BACKLOG prompt itself rather than decided here.

### Session 2026-07-19

- Q: The BACKLOG prompt says the daily digest sends "once per day, at end of day" but doesn't name
  a clock time — what fixed local time triggers it? → A: 19:00 Europe/Brussels, after the latest
  typical KDV closing/pickup time (~18:00–18:30), giving a buffer for same-day event corrections
  before the digest captures the day's data, while still landing in parents' inboxes at a normal
  evening hour. Self-resolved per this project's standing rule of picking the recommended default
  rather than blocking a scheduled run — no comparable precedent existed for a specific send time,
  but the choice is a low-impact, easily-changed operational parameter (a scheduler config value,
  not an architectural one).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director sends a one-off bulk email to a location or group (Priority: P1)

A director needs to reach every family at a location — or a single group/section within it —
with a one-off update (e.g. a schedule change, a reminder), optionally attaching a document like
a policy update or permission slip, without compiling a contact list by hand.

**Why this priority**: This is the feature's core new director-facing capability and the
foundation the closure/announcement email channel and the delivery-outcome UX build on — without
it, nothing else in this feature has a send path to reuse.

**Independent Test**: Can be fully tested by seeding a location with several families (including
one family with two children at the same location, and one contact with no email on file),
composing and sending a bulk email with an attachment, and confirming: one email per household
(not per child), the sibling family receives exactly one combined email, the no-email contact is
skipped and logged, and the attachment arrives intact.

**Acceptance Scenarios**:

1. **Given** a location with 5 enrolled families, one of which has two children at that location
   sharing one parent contact, **When** the director sends a bulk email to that location, **Then**
   that parent receives exactly one email, and the total distinct-household send count is 5, not 6.
2. **Given** a location with a contact who has no email on file, **When** the director sends a
   bulk email, **Then** that contact is skipped, logged, and the send still completes for every
   other contact.
3. **Given** a director composing a bulk email, **When** they upload a PDF or image attachment
   under the size cap, **Then** every recipient's email includes that attachment intact.
4. **Given** a director narrows the send to one group within a location, **When** they send,
   **Then** only contacts of children currently assigned to that group receive the email.
5. **Given** a location or group with zero currently-enrolled children, **When** the director
   selects that scope, **Then** the compose screen shows zero resolved recipients before send is
   attempted, not a submit-time error.
6. **Given** a bulk send where some addresses bounce or are rejected by the provider, **When** the
   send completes, **Then** the director sees a summary distinguishing "skipped — no email" from
   "provider failure," and the whole batch is not failed for one bad address.

---

### User Story 2 - Parent receives the daily report by email and can unsubscribe (Priority: P1)

A parent who doesn't always open the app still wants to know how their child's day went, and
wants a simple way to stop the daily email if they'd rather rely on the app alone.

**Why this priority**: This is the feature's other core capability and the BACKLOG's primary
motivating scenario ("emailed version of the child daily report") — equally foundational to User
Story 1, hence also P1.

**Independent Test**: Can be fully tested by seeding a child with two guardian contacts (mother
and father, both with email), triggering the end-of-day digest, confirming both receive
independent emails in their own locale, having one unsubscribe, confirming only that contact
stops receiving future digests while the other continues, and confirming re-subscribing restores
delivery.

**Acceptance Scenarios**:

1. **Given** a child with both a mother and father contact, each with an email on file and locale
   set (e.g. NL and FR respectively), **When** the end-of-day digest runs, **Then** both receive
   independent emails, each rendered in their own locale, not a single combined email.
2. **Given** a child with no events logged that day, **When** the digest runs for that child's
   contacts, **Then** they still receive an email whose content clearly reads "no updates logged
   today," not an empty-looking template.
3. **Given** a contact who clicks the unsubscribe link in a digest email, **When** they confirm
   (no login required), **Then** they stop receiving future daily digests, while any other contact
   for the same or a different child is unaffected.
4. **Given** an unsubscribed contact, **When** that contact is also a recipient of a bulk,
   announcement, or closure email, or is the target of an on-demand resend, **Then** they still
   receive it — the unsubscribe flag only suppresses the automatic daily digest.
5. **Given** a previously unsubscribed contact, **When** they re-subscribe via the same or an
   equivalent link, **Then** they resume receiving the daily digest starting from the next send.
6. **Given** a contact with children at two different locations, **When** they unsubscribe,
   **Then** the unsubscribe applies to that contact across both locations (the flag is per-contact,
   not per-child or per-location), since it is the same person's inbox either way.

---

### User Story 3 - Director/caregiver triggers an on-demand daily-report resend (Priority: P2)

A director or caregiver needs to resend a specific child's daily report by email on request (e.g.
a parent calls asking for it again, or the automatic send needs re-triggering after a correction),
independent of the automatic once-daily send and unaffected by a contact's digest-unsubscribe
state.

**Why this priority**: A genuinely useful escape hatch building directly on User Story 2's
rendering path, but a secondary, lower-frequency action compared to the automatic digest itself.

**Independent Test**: Can be fully tested by unsubscribing a contact from the digest, then
triggering an on-demand resend for that contact's child, and confirming the resend is delivered
despite the unsubscribe flag.

**Acceptance Scenarios**:

1. **Given** a child with contacts who have an email on file, **When** a director or caregiver
   triggers an on-demand resend for that child, **Then** each contact receives the daily report
   email immediately, independent of whether the automatic digest already ran today.
2. **Given** a contact who has unsubscribed from the automatic digest, **When** an on-demand
   resend is triggered for their child, **Then** they still receive it.

---

### User Story 4 - Closure and announcement notifications gain an email channel (Priority: P2)

A director's existing closure-day publish and announcement-send actions (features 011, 013)
should also reach parents by email, alongside the push/in-app notification they already trigger,
without the director doing anything new.

**Why this priority**: Directly fulfills the "email notification fallback" explicitly deferred by
both 011 and 013 to this feature, but depends on User Story 1's send/partial-failure mechanism
existing first, and is lower-frequency than the daily digest.

**Independent Test**: Can be fully tested by publishing a closure day and sending an announcement
in a tenant with contacts spanning subscribed/unsubscribed digest state and missing-email
contacts, and confirming both actions deliver email to every eligible resolved recipient
regardless of digest-unsubscribe state, with the same partial-failure tolerance as User Story 1.

**Acceptance Scenarios**:

1. **Given** a director publishes a closure day, **When** the notification fires, **Then** every
   resolved parent/guardian contact with an email on file receives an email in addition to the
   existing in-app message and push notification, regardless of their daily-digest
   unsubscribe state.
2. **Given** a director sends an announcement to a location or group, **When** it sends, **Then**
   every resolved contact with an email on file receives an email alongside the existing in-app
   notification and push, with the same skip/log behavior for contacts with no email.

---

### Edge Cases

- A location or group has zero enrolled children at send time: the bulk-send scope resolves to
  zero recipients — a no-op with a clear message, never an error (BACKLOG constraint).
- A parent contact has no email on file: skipped and logged for that send, while the child's other
  contacts (and the rest of the batch) still receive it (BACKLOG constraint).
- A daily report is generated for a child with no events recorded yet that day: the email clearly
  states "no updates yet" rather than rendering an empty-looking template (BACKLOG constraint).
- A contact who has unsubscribed from the digest is also targeted by an on-demand resend or a
  bulk/announcement/closure email: those are separate channels from the digest flag and are still
  delivered (BACKLOG constraint).
- A large location (100+ families) triggers a bulk send or is included in the daily digest: sends
  are batched/queued rather than synchronous within one request or job tick, to tolerate provider
  rate limits (BACKLOG constraint).
- A director attempts to upload an attachment exceeding the size cap or of a disallowed content
  type: the upload is rejected before send, with a clear inline error, not a silent drop or a
  provider-side rejection discovered only after send.
- A contact belongs to two children at the same location whose contract photo-consent flags
  differ: the daily digest for each child independently respects that child's own consent flag
  (per feature 007's `Contract.Consent`) when the digest includes photos — one child's consent
  state never leaks into the other's email.
- The unsubscribe link is opened after the token has already been used to unsubscribe (double
  click, stale tab): the action is idempotent — the contact remains unsubscribed, no error shown.
- A tenant has no `Email:SmtpHost` configured (existing dev/no-op behavior of `EmailService`): the
  daily digest job and bulk sends log the no-op per the existing convention rather than failing
  loudly, consistent with how transactional email already behaves in that state today.
- The scheduled daily-digest job invocation itself doesn't run at all on a given day (a
  scheduler misfire, a platform outage) rather than an individual send within it failing: that
  day's digest is simply not sent — there is no automatic same-day or next-day makeup send. The
  next day's scheduled run proceeds normally and covers that day's data only; detecting/alerting
  on a missed job execution is infrastructure monitoring (Cloud Monitoring on the Cloud Run Job's
  execution history), not new in-app behavior this feature builds, matching the operational
  posture already accepted for feature 014a's `send-payment-reminders` job.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a director compose and send a one-off bulk email (subject + body)
  scoped to a location, optionally narrowed to a single group within it, reaching every currently
  enrolled child's contacts in that scope.
- **FR-002**: System MUST send exactly one email per distinct household (contact), never one per
  child, when a contact is linked to multiple children within the send scope.
- **FR-003**: System MUST let a director attach one document (PDF, JPEG, or PNG) to a bulk email,
  uploaded via a GCS signed URL, up to a 10MB size cap, delivered intact to every recipient.
- **FR-004**: System MUST send the daily report email automatically once per day, at a fixed
  19:00 Europe/Brussels trigger time, independently to every parent/guardian contact with an
  email on file for each enrolled child, unless that contact has an active digest-unsubscribe.
- **FR-005**: System MUST render the daily report email from the same aggregated daily-summary
  data the parent app shows (feature 013's `GetDailySummaryQuery`), including a clear "no updates
  logged today" state when a child has no events that day.
- **FR-006**: System MUST respect each child's own photo/media consent flag (feature 007's
  `Contract.Consent`) when a daily report email includes photos, evaluated independently per
  child even when one contact has children with differing consent states.
- **FR-007**: System MUST let a contact unsubscribe from and re-subscribe to the daily digest via
  a signed, single-purpose link requiring no login; the state is per-contact (not per-child, not
  per-location) and independent of every other contact's subscription state.
- **FR-008**: System MUST NOT apply the digest-unsubscribe flag to bulk emails, announcements,
  closure-day emails, or on-demand resends — those channels always deliver to every contact with
  an email on file, regardless of digest-unsubscribe state.
- **FR-009**: System MUST let a director or caregiver trigger an on-demand resend of one child's
  daily report email to its contacts at any time, independent of the automatic daily send.
- **FR-010**: System MUST extend the existing closure-day notification flow (feature 011) to send
  an email to every resolved contact with an email on file, alongside its existing in-app message
  and push notification.
- **FR-011**: System MUST extend the existing announcement flow (feature 013) to send an email to
  every resolved contact with an email on file, alongside its existing in-app notification and
  push.
- **FR-012**: System MUST tolerate partial failure on any bulk send (some addresses invalid,
  bounced, or rejected by the provider) — logging and continuing rather than failing the entire
  batch for one bad address — and MUST show the director a summary distinguishing
  skipped-no-email contacts from provider-reported failures.
- **FR-013**: System MUST scope every recipient resolution to the sending director's own tenant;
  no bulk send, digest, or closure/announcement email may ever reach a contact outside that
  tenant.
- **FR-014**: System MUST render every email this feature sends (bulk, digest, closure,
  announcement) in the recipient contact's locale (NL/FR/EN, from `Contact.Locale`), with no
  untranslated text or raw i18n keys visible.
- **FR-015**: System MUST batch or queue large sends (bulk email and the daily digest) rather than
  sending synchronously within a single request or job invocation, to tolerate provider
  rate-limiting on 100+-recipient scopes.
- **FR-016**: System MUST show a "zero recipients in this scope" state on the compose screen
  before send is attempted, for a location/group with no currently enrolled children, rather than
  allowing a submit that resolves to nothing.
- **FR-017**: System MUST validate attachment content-type (PDF/JPEG/PNG only) and the 10MB size
  cap before issuing an upload URL, surfacing a clear inline error for a rejected file rather than
  discovering the problem at send time or via a provider-side rejection.
- **FR-018**: System MUST log the full error server-side and never expose a raw provider error or
  stack trace to the director or a parent, for any failure in this feature's send paths (per
  `CLAUDE.md`'s error-handling rule).
- **FR-019**: Every interactive element the director-web compose screen introduces (location/group
  selector, subject/body fields, attachment upload, send action) MUST be reachable via keyboard
  alone with a visible focus ring, per `platform-rules.md`'s Director Web App section.
- **FR-020**: Both the unsubscribe and the re-subscribe action MUST be idempotent — activating
  either action again while the contact is already in that state (already unsubscribed, or
  already subscribed) leaves the contact in that same state with no error shown.

### Key Entities

- **Contact (extended)**: gains `DigestUnsubscribedAt` (nullable timestamp; null = subscribed) —
  per-contact, not per-child, so one guardian unsubscribing never affects another, and the state
  survives across every location/child that contact is linked to.
- **BulkEmailSend**: one record per director-initiated bulk send — scope (location, optional
  group), subject, body, optional attachment reference, sender, timestamp, and per-recipient
  delivery outcome (sent / skipped-no-email / provider-failure), backing the director's
  post-send summary and providing an audit trail.
- **BulkEmailAttachment**: a GCS-signed-URL-backed file (PDF/JPEG/PNG, ≤10MB) attached to one
  `BulkEmailSend`, following the same storage-port pattern as feature 013g/013h's health/vaccine
  attachments.
- **Daily Report Email**: not a new stored entity — rendered on demand (automatic daily send or
  on-demand resend) from the existing `GetDailySummaryQuery` read-model (feature 013), per
  recipient contact and locale.
- **Email Template**: the rendering mechanism (concrete choice deferred to `plan.md`) that
  replaces today's raw C# string-literal, English-only templates in `EmailService`, producing
  NL/FR/EN output for every email this feature and the extended 011/013 flows send.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can compose and send a bulk email (with an optional attachment) to a
  location or group in under 2 minutes, seeing an accurate delivery outcome summary afterward.
- **SC-002**: 100% of parent/guardian contacts with an email on file and no active
  digest-unsubscribe receive that day's daily report email by end of day, with zero duplicate
  emails to a contact linked to multiple children at the same location.
- **SC-003**: A contact can unsubscribe from the daily digest in one click with no login, and the
  unsubscribe takes effect starting with the next scheduled send.
- **SC-004**: Zero untranslated strings or raw i18n keys appear in any email this feature sends,
  across all three supported languages.
- **SC-005**: A single bad or bounced address never causes a bulk send or the daily digest to fail
  for any other recipient in the same batch.
- **SC-006**: Closure-day and announcement emails reach 100% of the same recipients their existing
  in-app/push notification already reaches (contacts with an email on file), with no additional
  director action required beyond the existing publish/send action.

## Assumptions

- **The templating/i18n rework is this feature's to own, not feature 019's.** `IEmailSender.cs`'s
  existing doc comments attribute the deferred templating/i18n work to "feature 019," but 019 is
  IKT subsidy/Opgroeien API integration (per BACKLOG.md) and has no connection to email
  templating. This spec treats that comment as stale and assigns the templating rework to this
  feature, per the BACKLOG.md 020 prompt block's explicit instruction to "decide the concrete
  templating mechanism... at plan time." `plan.md` should correct or remove the stale comment
  when implementing.
- **The daily digest reuses feature 014a's existing Cloud Scheduler + Cloud Run Job pattern**,
  not a new mechanism. 014a's `send-payment-reminders` CLI subcommand
  (`backend/ChildCare.Api/Cli/SendPaymentRemindersCommand.cs`) plus its
  `infra/gcp/main.tf` `google_cloud_run_v2_job`/`google_cloud_scheduler_job` pair is this
  codebase's first scheduled-job infrastructure, built specifically because Cloud Run's
  scale-to-zero makes an in-process timer/`BackgroundService` unreliable. This spec's earlier
  framing (an earlier draft treated the digest as introducing the first recurring job) was
  inaccurate; `plan.md` follows 014a's exact shape — a new `send-daily-reports` subcommand
  looping every `Ready` tenant schema via `ITenantDbContextResolver`, isolating per-tenant
  failures, plus a matching Cloud Scheduler entry at 19:00 Europe/Brussels — rather than
  introducing a new job-queue dependency (Hangfire, Quartz).
- **The attachment size cap is 10MB**, matching the one existing precedent in this codebase
  (`UploadGroupActivityPhotoCommand`'s `MaxFileSizeBytes`), rather than inventing a new value —
  reasonable for typical menu/policy/permission-slip documents and consistent with the only
  existing convention to draw from.
- **`DigestUnsubscribedAt` is a nullable timestamp, not a bool**, so the system can show "when
  did this contact unsubscribe" and support re-subscription as a distinct, auditable event rather
  than losing that history on toggle.
- **Bulk-email attachments are scoped to one `BulkEmailSend`, not added to a general tenant media
  library.** No existing feature has a shared director-uploaded-document library; introducing one
  is out of this feature's scope per the BACKLOG's "what to build" list, which describes the
  attachment as part of composing one email, not a reusable document repository.
- **Closure (011) and announcement (013) email fan-out reuses this feature's recipient-resolution
  and partial-failure-tolerant send path rather than each maintaining its own.** Both features'
  specs already name this feature as owning the deferred email channel; duplicating send logic in
  three places would contradict the single bulk-send mechanism the BACKLOG prompt describes.
- **SMS/WhatsApp, open/click tracking, and a full multi-channel notification preference centre are
  out of scope**, per BACKLOG.md's explicit "Out of scope" list. The digest-unsubscribe flag added
  here is a single, narrow opt-out, not the broader per-notification-type preference system
  planned for Phase 3.
