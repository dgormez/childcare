# Research: Invoice Payments Plus

## R1 — `IPaymentProvider` abstraction shape

**Decision**: A thin port in `ChildCare.Application.Common`, mirroring `IExpoPushSender`'s
existing shape:

```csharp
public interface IPaymentProvider
{
    Task<ConnectAccountResult> GetOAuthAuthorizationUrlAsync(Guid tenantId, CancellationToken ct);
    Task<ConnectAccountResult> CompleteOAuthConnectionAsync(Guid tenantId, string authorizationCode, CancellationToken ct);
    Task DisconnectAsync(Guid tenantId, CancellationToken ct);
    Task<CreatePaymentResult> CreatePaymentAsync(Guid tenantId, string paymentReference, int amountCents, string description, CancellationToken ct);
    Task<PaymentStatusResult> GetPaymentStatusAsync(Guid tenantId, string providerPaymentId, CancellationToken ct);
}
```

Implementation `MolliePaymentProvider` lives in `ChildCare.Infrastructure.Payments`, wrapping
Mollie's Connect + Payments APIs. No Mollie-specific type (its SDK's request/response shapes)
crosses into `ChildCare.Application` — only the plain result records above do.

**Rationale**: Exact precedent this codebase already established for a third-party HTTP
integration behind a narrow interface (`IExpoPushSender`/`ExpoPushSender`). Keeps the
`IPaymentProvider`-per-spec-FR-018 requirement mechanical rather than inventing a new pattern.

**Alternatives considered**: A generic `IPaymentGateway` with provider-agnostic webhook parsing
built into the interface. Rejected — webhook payload shapes are inherently provider-specific;
forcing a shared parse contract now, with only one provider implemented, is exactly the kind of
speculative generality the global engineering guidance (no designing for hypothetical future
requirements) warns against. The seam here is the five methods above; a second provider's
webhook parsing lives in its own adapter, not a shared interface method.

## R2 — Tenant/invoice resolution from the public webhook (never trust the payload)

**Decision**: When creating a Mollie payment for an invoice, generate a new opaque
`PaymentReference` (GUID) and pass Mollie a **per-payment webhook URL**
(`POST /api/webhooks/mollie/{paymentReference}`) rather than a shared endpoint reading a tenant
ID out of the JSON body. `PaymentReference` is the primary key of a new cross-tenant `Payment`
index row in `PublicDbContext` (see data-model.md) mapping `PaymentReference → (TenantId,
InvoiceId, ProviderPaymentId)`. The webhook handler:

1. Looks up `PaymentReference` (the URL path segment) in `PublicDbContext.Payments`. Not found →
   `404`, no further action (the endpoint reveals nothing else).
2. Resolves `TenantId` from that row (never from the request body) and switches the tenant
   context for the rest of the request (mirrors `TenantMiddleware`'s `search_path` switch, but
   invoked explicitly inside the handler since `TenantExempt` skips the JWT-claim path).
3. Calls Mollie's `GET /payments/{id}` directly (using the resolved organisation's connected
   account credentials) to fetch the authoritative status — the webhook body itself is treated
   only as a "something changed, go check" signal (FR-007), standard Mollie-recommended
   practice.
4. Applies the `Sent → Paid` transition only if Mollie confirms `paid`, using the invoice ID
   from step 2 — never any invoice ID the payload might claim.

**Rationale**: 014's OGM reference is unique only *within* a tenant schema (`SequenceNumber` is
a tenant-scoped identity column, per `Invoice.cs`'s own comment) — two different organisations'
invoice #1 produces the same OGM digits. The prompt block's "resolve tenant from the payment
reference, e.g. the OGM" intent is honored by generating a reference that actually is globally
unique (spec.md FR-006), rather than reusing the OGM verbatim as a cross-tenant lookup key. A
per-payment webhook URL is Mollie's own supported mechanism (no shared-secret/HMAC scheme
needed on top), and looking the reference up in a system-controlled table — rather than trusting
any tenant/organisation claim in the body — is what makes tenant-crossing structurally
impossible (Constitution Principle I), matching this feature's own edge case about a
tenant-enumeration oracle.

**Alternatives considered**: Encoding the tenant ID directly in the webhook URL
(`/webhooks/mollie/{tenantId}/{invoiceId}`). Rejected — an opaque, unguessable reference is a
strictly better boundary than a URL containing a real tenant/invoice ID an attacker could
enumerate or substitute; the opaque-reference approach costs nothing extra to implement.

## R3 — Mollie OAuth token storage

**Decision**: A new `PaymentProviderConnection` entity in `PublicDbContext` (public schema, one
row per `TenantId` — organisation-wide, matching FR-001's OAuth flow being org-level, not
per-location). Access/refresh tokens are encrypted at rest using ASP.NET Core's built-in Data
Protection API (`IDataProtector`, purpose string `"PaymentProviderConnection.MollieTokens"`) —
already part of the ASP.NET Core framework this codebase runs on, no new dependency. Only a
connected/disconnected status and the Mollie-side account label are ever returned to a client;
raw tokens never leave the server.

**Rationale**: No encryption-at-rest precedent exists yet in this codebase (confirmed by
search) because no feature before this has stored a third-party credential — Data Protection is
the standard, already-available .NET mechanism for exactly this, and keeps with Constitution
Principle VI (secrets never hardcoded, encrypted at rest) without introducing a new library.
Public schema (not tenant schema) because the webhook path (R2) needs to resolve and use these
credentials before any tenant context is established — same reasoning that already put
`vaccine_types` (013g) and `Tenant` itself in `PublicDbContext`.

**Alternatives considered**: GCP Secret Manager per-organisation secret. Rejected for this
volume/shape of data — Secret Manager (per Constitution Principle VI) is used for static,
deployment-level secrets (connection strings, API keys), not a growing set of per-tenant,
frequently-rotated OAuth tokens; Data Protection's envelope encryption is the correct scale
match and requires no Terraform/infra change.

## R4 — Reminder job: first scheduled-job infrastructure in this codebase

**Decision**: A new CLI command (`send-payment-reminders`, `backend/ChildCare.Api/Cli/`),
following the exact shape of `MigrateTenantsCommand`/`BackfillGrowthCheckCommand` — iterates
every tenant schema, finds `Sent`+overdue invoices at locations with reminders enabled, and
sends reminders per FR-013's cadence/cap rules. Triggered by a new GCP Cloud Scheduler job
(Terraform-managed, `infra/gcp/`) invoking a Cloud Run Job execution of this same container
with the CLI command as its entrypoint argument, once daily.

**Rationale**: Two prior features' research (`008a-caregiver-kiosk-mode` R-stale-shifts,
`014-invoicing` R4) explicitly deferred building any scheduled-job infrastructure — both noted
"no `IHostedService`/cron infrastructure exists... revisit if a future feature needs real
scheduled jobs for other reasons, and fold this in then." A payment reminder is a genuine
side-effecting, time-based action (sending a notification) that cannot be lazily computed on
next read the way "overdue" (014) or "stale shift" (008a) could — this is that anticipated
future feature. Reusing the existing CLI-command pattern (rather than inventing an in-process
`BackgroundService`) keeps the change consistent with Constitution Principle VII
(Monolith-First) and avoids an in-process timer, which is unreliable under Cloud Run's
scale-to-zero model (the same reasoning 008a's research already recorded) — the job needs to run
whether or not an API instance happens to be warm.

**Alternatives considered**: An in-process `IHostedService` with a `PeriodicTimer`. Rejected —
Cloud Run scale-to-zero means an idle instance can be killed between ticks, so a scheduled
side-effect (unlike a lazily-computed read value) would be silently unreliable. A Cloud
Scheduler-triggered authenticated HTTP endpoint instead of a Cloud Run Job. Viable, but the CLI
route reuses an already-reviewed pattern (per-tenant iteration, manual invocation already
exists) rather than adding a new authenticated-machine-caller endpoint class this codebase has
zero precedent for.

## R5 — Betalingsbewijs rendered on demand, not stored

**Decision**: `GenerateBetalingsbewijsQuery` mirrors `GenerateInvoicePdfQuery` (014) exactly —
renders a `QuestPdfBetalingsbewijsGenerator` output from the invoice's current `Paid`-state
fields on every call, never persisted to GCS. Available (returns `Found = true`) only once
`Invoice.Status == Paid`.

**Rationale**: 014's own invoice PDF already established this pattern (research.md R1 there:
"rendered on-demand from the invoice's current stored state, never persisted to storage") for
exactly the same reason it applies here — a paid invoice's relevant fields never change after
the transition, so on-demand rendering is already deterministic (spec.md FR-016) without adding
a stored document, a GCS path, or a signed-URL concern (Constitution Principle VI is N/A, same
as 014's own Constitution Check). The original spec draft assumed a "stored/signed-URL" pattern
that turned out not to exist in 014 at all — corrected here rather than carried through.

**Alternatives considered**: Persist the rendered PDF to GCS at generation time (as 013b/013c/
013g do for uploaded attachments). Rejected — those features store PDFs a *user* uploads
(content this system doesn't control and can't regenerate); a betalingsbewijs is entirely
system-generated from data already in the database, so persisting a copy only adds a storage
path with zero benefit over regenerating it identically on each request.

## R6 — `Payment` entity and idempotent payment-link reuse

**Decision**: A tenant-scoped `Payment` entity, one row per *attempt* against an invoice
(`InvoiceId`, `ProviderPaymentId`, `PaymentReference` — matches the public-schema index row's
key from R2, `Status` [`Open`, `Paid`, `Failed`, `Cancelled`, `Expired`], `AmountCents`,
`FeeCents`, timestamps). "Pay now" (FR-004) queries for an existing `Payment` on that invoice
with `Status == Open`; if found and not expired (checked live against Mollie, since Mollie
payment links have their own TTL), redirect to its existing checkout URL instead of creating a
new one (per the 2026-07-16 clarification). Webhook processing (FR-009) is idempotent because
it's keyed by `ProviderPaymentId`/`Status` transition guard — a repeat delivery for an
already-`Paid` `Payment` (and therefore already-`Paid` `Invoice`, guarded by 014's existing
one-way transition) is a no-op.

**Rationale**: Directly implements the clarification session's payment-link-reuse decision and
014's existing `Sent → Paid` guard, without inventing new idempotency machinery — the invoice's
own status transition is already the idempotency boundary; `Payment` just needs to not create a
second row for the same in-flight attempt.

## R7 — Where the UI lives: existing screens, not a new hierarchy

**Decision**: Mollie connection (org-wide, FR-001/002/003) is a new section on the existing
`web/app/(app)/settings/page.tsx` (`OrganisationSettingsPage`) — not a new "Settings > Payments"
sub-hierarchy, since no such nested settings structure exists anywhere in `web/` today (confirmed
via `Sidebar.tsx`: `/settings` is a single flat page). Reminder-cadence settings (per-location,
FR-012) extend the existing per-location "Invoicing" tab 014 already added to
`web/app/(app)/locations/[id]/page.tsx` (`InvoiceSettingsForm.tsx`), alongside
`InvoiceDueDays` — not a separate screen.

**Rationale**: Corrects the spec's Product Context main flow, which described a generic
"Settings → Payments" navigation that doesn't match this codebase's actual (flat, single-page)
settings structure. Reusing existing screens avoids inventing new information architecture for
a feature that fits cleanly into two screens that already exist.

## R8 — Reminder cap tracking

**Decision**: `Invoice` (014) gains two nullable fields: `ReminderCount` (int, default 0) and
`LastReminderSentAt` (nullable `DateTime`). The reminder CLI command increments `ReminderCount`
and sets `LastReminderSentAt` on each send, and only sends when `ReminderCount < 3` and
`LastReminderSentAt` is null or falls before today minus the location's configured cadence.

**Rationale**: Matches this codebase's established pattern of extending an existing shipped
entity for a small, tightly-scoped new field set (`Location`'s `InvoiceDueDays`,
`ReservationNoticeHours`, etc.) rather than introducing a separate reminder-log table for what
is, at 3-reminders-max, a bounded and simple counter — avoiding the premature-abstraction trap
the global engineering guidance warns against for a feature this size.
