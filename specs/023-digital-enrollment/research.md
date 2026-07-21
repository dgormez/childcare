# Phase 0 Research: Digital Online Enrollment

## R1: Tenant resolution for the public (unauthenticated) endpoints

**Decision**: Public endpoints resolve their tenant schema from an `org` slug parameter,
exactly mirroring feature 020's unsubscribe/resubscribe endpoints and `ResetPasswordCommand`
(003) — never through `TenantMiddleware`'s JWT `tenant_id` claim, since there is no JWT on these
routes. Concretely: the URL is `/enroll/{orgSlug}/{locationSlug}` on the public web route and
`/api/public/enrollment/{orgSlug}/{locationSlug}` on the API; each new public endpoint is marked
`.RequireTenantExempt()` (the existing deny-by-default `TenantExemptAttribute` mechanism,
`backend/ChildCare.Api/Middleware/TenantExemptAttribute.cs`) and its handler resolves the tenant
itself via `OrganisationSlugResolver` before touching any tenant data — the same shape as
`DigestUnsubscribeLinkResolver.ResolveAsync`.

The location itself is then resolved within that schema by a new `PublicEnrollmentSlug` column
on `Location` (see data-model.md) — analogous to, but a distinct field from, `Tenant.Slug`,
since a slug unique only within one tenant's locations is sufficient (locations don't need
global uniqueness, only per-tenant).

**Rationale**: This is the only existing pattern in the codebase for a public, no-login route
that still needs tenant-schema data — reusing it exactly (rather than inventing parallel
machinery) keeps `TenantExemptAttribute`'s deny-by-default guarantee intact and avoids a second,
subtly different unauthenticated-tenant-resolution mechanism.

**Alternatives considered**:
- *A single global `Location.Slug` (no `org` segment)*: rejected — would require slugs to be
  unique across every tenant in the whole platform, a constraint no other public identifier in
  this codebase has (`Tenant.Slug` is only unique among tenants, not combined with anything
  else); a two-segment URL costs one extra path parameter and avoids a new cross-tenant
  uniqueness rule.
- *Resolve tenant via a custom header or subdomain per organisation*: rejected — no such
  infrastructure exists yet (this platform is single-domain), and it would be a bigger,
  unrelated infra change to justify for one feature.

## R2: Tour-invitation state modeling

**Decision**: Fields directly on `WaitingListEntry` (proposed date/time, invitation status, a
free-text outcome), not a new `TourInvitation` entity or a history table.

**Rationale**: Feature 022's clarification session already established, and verified by search,
that this codebase has no per-change history table pattern anywhere — the closest analog
(013h's vaccine-catalog deactivation) uses attribution fields, not a log. A waiting-list entry
has exactly one active tour invitation at a time per spec.md (the director resends/reschedules
by re-sending, not by tracking multiple parallel invitations), so a single evolving set of
fields is sufficient and consistent with this codebase's established preference.

**Alternatives considered**: A separate `TourInvitation` table keyed by `WaitingListEntryId`
with one row per invitation sent — rejected as unnecessary complexity for a field that's
functionally a status plus two timestamps and a free-text note; would only pay off if multiple
concurrent/historical invitations per entry were a real requirement, which spec.md's Assumptions
explicitly scope out.

## R3: Duplicate detection — computed vs. stored flag

**Decision**: Computed at read time in `ListWaitingListEntriesQuery` (a self-join comparing
child first/last name + date of birth within the same `LocationId`), not a stored boolean on the
entry.

**Rationale**: A stored flag would need to be recalculated every time a new entry is added or an
existing one's status changes (e.g., a second matching entry arrives after the first was already
displayed) — a class of staleness bug a computed value can't have. `GetOccupancyQuery` (012a)
already establishes the precedent of computing a derived, always-fresh value at query time
rather than maintaining a redundant stored one; this follows the same shape.

**Alternatives considered**: A stored `IsPossibleDuplicate` flag set at creation time and never
recalculated — rejected because a later `withdrawn`/`enrolled` transition on either matching
entry wouldn't be reflected without an explicit recompute step, reintroducing exactly the
staleness class the computed approach avoids for free.

## R4: Tour-invitation accept/decline delivery surface

**Decision**: The accept/decline link renders a server-rendered HTML page directly from the
backend (`GET /api/public/enrollment/tour-response`, `Results.Content(html, "text/html")`),
mirroring the existing unsubscribe/resubscribe page pattern in `EmailEndpoints.cs`
(`RenderUnsubscribePage`) — not a Next.js route in `web/`.

**Rationale**: `EmailLinkBuilder`'s own doc comment already states the precedent explicitly:
these email links point at the API's own server-rendered page (`App:ApiBaseUrl`), not a client
app. A one-click accept/decline confirmation is exactly the same shape of interaction as
unsubscribe/resubscribe (single action, minimal UI, reached only via an emailed link) — reusing
the same delivery surface avoids standing up a second, parallel "public unauthenticated page"
mechanism in `web/` for a near-identical need. The richer, multi-field **enrollment form**
itself is a genuinely different case (needs real client-side validation, a language toggle, and
the polished UX `platform-rules.md`/`reference-products.md` require for a parent-facing form) and
is therefore a full `web/` Next.js route instead — see the Project Structure in plan.md.

**Alternatives considered**: A dedicated `web/app/enroll/tour-response/page.tsx` Next.js route —
rejected; it would need its own unauthenticated-fetch plumbing for a single GET-then-confirm
interaction that the existing backend-rendered-HTML pattern already solves with zero new client
infrastructure.

## R5: Reference-code and signed-token generation

**Decision**: The reference code (FR-008, resolved in spec.md's Clarifications to an 8-character
human-legible alphanumeric string) is generated server-side at entry creation using a
cryptographically random source, filtered to an unambiguous character set (excludes `0/O`,
`1/I/l`), and re-rolled on the rare collision (checked via the existing per-tenant uniqueness
constraint on the new column). The tour-invitation token is a signed, purpose-scoped token via a
new `ITourInvitationTokenService`, directly mirroring `IUnsubscribeTokenService`'s exact shape
(`CreateToken(Guid entryId)` / `TryParseToken(string token) : Guid?`, fails closed on any
tampering) — a separate interface, not a generalized/shared one, matching how `IUnsubscribeTokenService`
itself is scoped to one link purpose rather than built as a generic multi-purpose signer.

**Rationale**: Reusing `IUnsubscribeTokenService`'s proven shape for a second, unrelated
link-purpose keeps the security properties (server-side-only signing key, fails closed) and
review pattern consistent without introducing a new abstraction this codebase doesn't otherwise
have (a shared generic token service would be new machinery for a two-consumer need).

**Alternatives considered**: A generic `ISignedTokenService<TPurpose>` shared by both unsubscribe
and tour-invitation — rejected as premature generalization for two call sites with no third on
the horizon; matches this project's stated preference (`.claude/CLAUDE.md`) for avoiding
speculative abstraction.

## R6: Rate limiting

**Decision**: A new named policy `public-enrollment` added to the existing `AddRateLimiter`
configuration in `Program.cs` (`RateLimitPartition.GetSlidingWindowLimiter`, partitioned by
`RemoteIpAddress`, `PermitLimit = 3`, `Window = TimeSpan.FromHours(1)`), applied via
`.RequireRateLimiting("public-enrollment")` on the submission endpoint only (not the read-only
location-info lookup or the tour-response endpoint, neither of which are spam vectors in the
same way).

**Rationale**: Directly mirrors the existing `auth-strict`/`auth-refresh` policy shape already
established for other unauthenticated, abuse-prone endpoints — same partitioning strategy
(IP-based, no user context available), just a different limit/window matching FR-006's explicit
3-per-hour requirement.

**Alternatives considered**: A bespoke in-memory rate limiter — rejected; ASP.NET Core's
built-in `Microsoft.AspNetCore.RateLimiting` middleware is already wired into this project for
exactly this purpose, and a second mechanism would be duplicate infrastructure.
