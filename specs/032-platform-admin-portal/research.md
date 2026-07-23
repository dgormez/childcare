# Research: Platform-Admin Portal — Invitations, Registration & Organisation Directory

No `[NEEDS CLARIFICATION]` markers remained in spec.md after the two clarification sessions
(status-model for superseded invitations; the scope-expansion questions resolved directly with
the product owner). This document instead records the technical research that shaped the plan —
decisions, rationale, and alternatives considered, per this codebase's existing `research.md`
convention (012a, 013g, 025, 026, etc.).

## R1 — Reuse the existing `Invitation` entity and token model, extend rather than replace

**Decision**: Extend `backend/ChildCare.Domain/Entities/Invitation.cs` itself with new columns
(`OrganisationNameNote`, `Locale`, `CreatedByUserId`/`CreatedByEmail`, `RevokedByUserId`/
`RevokedByEmail`/`RevokedAt`) rather than introducing a second invitation **entity** for
platform-admin use. This is an entity-level decision only — the MediatR **commands** that
operate on it are a different question, resolved separately in R16 below (a new
`CreatePlatformAdminInvitationCommand`/`ResendPlatformAdminInvitationCommand`/
`RevokePlatformAdminInvitationCommand`, not a reuse/extension of the existing
`CreateInvitationCommand`).

**Rationale**: The entity, `InvitationTokenCodec` (opaque token, SHA-256 hash-only persistence),
and the supersede-on-recreate behavior in `CreateInvitationCommandHandler` already do exactly
what spec.md's FR-002/FR-005 require. `RegisterOrganisationCommandHandler` already looks up
`db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash)` — nothing about the
acceptance path needs to change.

**Alternatives considered**: A separate `PlatformAdminInvitation` entity — rejected, since it
would fork the token/acceptance logic `RegisterOrganisationCommandHandler` already implements
correctly, for no benefit.

## R2 — Status derivation, not a stored field

**Decision**: `ListPlatformAdminInvitationsQuery` computes status per row rather than reading
a stored enum:

```text
Accepted  — a Tenant exists with Tenant.CreatedFromInvitationId == invitation.Id
Revoked   — RevokedAt is not null (covers both explicit revoke and supersede, per spec.md's
            Clarifications — the two are never distinguished)
Expired   — ExpiresAt <= now
Pending   — none of the above
```

**Rationale**: Matches the existing precedent exactly (`RegisterOrganisationCommandHandler`'s
comment: "'used' is derived... research.md R10" from feature 001). A stored status column could
drift from these facts (e.g. an `ExpiresAt` bumped forward without updating a cached status);
deriving it removes that failure mode entirely, per spec.md FR-004's own explicit requirement.

**Alternatives considered**: A stored `Status` enum updated by each command — rejected per
FR-004's explicit "never a separately-settable field" requirement, and per the precedent
`Invitation.cs`'s own comment already set for the "used" concept.

## R3 — Revoke/supersede shares one set of fields, no new status

**Decision**: One set of nullable fields (`RevokedByUserId`, `RevokedByEmail`, `RevokedAt`)
serves both an explicit platform-admin "Revoke" click and an automatic supersede (create-
duplicate-email or resend). `CreateInvitationCommandHandler`'s existing supersede loop (today:
`pending.ExpiresAt = now`) is extended to also set these three fields, attributed to the acting
platform-admin performing the create/resend.

**Rationale**: Direct implementation of spec.md's Clarifications session 2 answer (Option A) —
no fifth status, no distinction in the data model between the two triggers.

**Alternatives considered**: A `SupersededByInvitationId` self-reference distinguishing
supersede from manual revoke — rejected by the product owner's own answer; would add a status
the spec explicitly says not to have.

## R4 — Audit-attribution field shape mirrors `VaccineType` exactly

**Decision**: `RevokedByUserId` (`Guid?`, no DB-level FK), `RevokedByEmail` (`string?`,
denormalized), `RevokedAt` (`DateTime?`) — identical shape to `VaccineType.DeactivatedByUserId`/
`DeactivatedByEmail`/`DeactivatedAt` (feature 013h). Acting-user resolved at the endpoint layer
from `ClaimTypes.NameIdentifier`/`ClaimTypes.Email`, the same `ActingUserOf(HttpContext)` pattern
`PlatformAdminVaccineTypeEndpoints.cs` already uses.

**Rationale**: `Invitation` lives in the same Public schema as `VaccineType`; the acting
`TenantUser` lives in an arbitrary tenant schema either way — the "no cross-schema FK" reasoning
applies identically. Reusing an established, already-reviewed pattern rather than inventing a
new one.

**Alternatives considered**: None seriously — this is a direct, intentional repeat of R2 from
013h's own research.md.

## R5 — Organisation directory reads Public schema only, no per-tenant fan-out

**Decision**: `ListPlatformAdminOrganisationsQuery` is a single query over
`PublicDbContext.Tenants` left-joined to `PublicDbContext.Invitations` on
`Tenant.CreatedFromInvitationId == Invitation.Id`, projecting `Tenant.Name`, `Tenant.Plan`,
`Tenant.ProvisioningStatus`, `Tenant.KboNumber`, `Tenant.CreatedAt`, and `Invitation.Email` (the
registrant's email) as "registered by."

**Rationale**: Confirmed by direct inspection: `Tenant.Name` is already the organisation name
(written once, at registration, by `RegisterOrganisationCommandHandler`'s
`ClaimOrResumeTenantAsync`) — never duplicated into the tenant's own schema. `GetCurrentOrganisationQuery`
already proves a single organisation's info is fully answerable from `PublicDbContext.Tenants`
alone, with zero `TenantDbContext`/tenant-schema access. Every `Tenant` row today is created
exclusively through this one path (feature 001 has no second creation path), so the join always
resolves. This avoids the heavier `MigrateTenantsCommand`-style per-tenant-schema fan-out
(`ITenantDbContextResolver.ForSchema(...)` in a loop) entirely — not needed, since nothing this
feature's directory shows requires live per-tenant data (e.g. a live, possibly-changed director
list).

**Alternatives considered**: A live, per-tenant-schema query for "the current director(s)" of
each organisation (via `ITenantDbContextResolver`, matching `MigrateTenantsCommand`'s
enumeration pattern) — rejected: heavier (N+1 schema connections), and the product owner's own
answer said "visibility only," not a live operational dashboard. The invited registrant's email
(traced via `CreatedFromInvitationId`) is an accepted, explicitly-documented proxy for "who
registered this org," not a claim that it's still an active user of the account — noted plainly
in the contract's response field name (`registeredByEmail`, not `currentDirectorEmail`) to avoid
implying it's kept in sync with later staff changes.

## R6 — `ProvisioningStatus` surfaced as-is, not reinterpreted as active/inactive

**Decision**: The directory's status column shows `Tenant.ProvisioningStatus`
(`Provisioning`/`Ready`/`Failed`) directly, labeled honestly (e.g. "Ready" → a "live" badge,
"Failed" → a distinct failure badge) — no new field, no suspend/deactivate action anywhere in
the UI or API.

**Rationale**: Confirmed no suspension/deactivation flag exists anywhere on `Tenant` today
(grepped `suspend|deactivat|isActive|disabled` — zero matches on `Tenant.cs`/`PublicDbContext.cs`'s
`Tenants` mapping). Inventing one now would silently expand scope into tenant-suspension
tooling, which BACKLOG.md's feature 002 and this feature's own Out of Scope section both
explicitly defer. `ProvisioningStatus` is semantically about provisioning completion, not
admin-driven activity — the plan is explicit about this distinction (see spec.md's Assumptions)
so a future tenant-suspension feature doesn't mistake this directory's read-only badge for an
existing control.

**Alternatives considered**: Adding a new `IsSuspended`/`IsActive` flag "since we're already
touching this area" — rejected as unrequested, out-of-scope speculative scope creep, exactly
what the constitution's carve-out reasoning elsewhere warns against inventing ahead of a
concrete need.

## R7 — The missing registration page: new web page, zero backend change

**Decision**: `web/app/register/page.tsx`, a public, unauthenticated Next.js page (outside the
`(app)`/`(auth)` route groups, mirroring `web/app/enroll/[orgSlug]/[locationSlug]/page.tsx`'s
existing precedent), calling the already-existing `POST /api/organisations/register`
(`OrganisationEndpoints.cs`, `RequireTenantExempt()`) with no backend changes at all.

**Rationale**: Confirmed via direct search: no `web/app` route consumes this endpoint today —
only the backend half of feature 001 was ever built. `RegisterOrganisationCommand`/
`RegisterOrganisationCommandHandler`/`RegisterOrganisationCommandValidator` already implement
every rule the new page needs to surface (org-name validation, `InvitationNotFound` generic
404, `EmailMismatch` 422) — the page is a pure consumer, no contract change.

**Alternatives considered**: Building a companion backend "invitation-lookup" endpoint so the
page can pre-fill/validate the token before the user starts typing — rejected as unnecessary
complexity; the existing endpoint already returns a clean, generic not-found error the page can
render directly, and pre-validating separately would be a second place the same generic-error
rule (R5 in feature 001's own research.md) needs to be re-implemented correctly.

## R8 — Registration page link format: token only, no organisation slug

**Decision**: `OrganisationInvitationLinkBuilder.BuildRegisterUrl(config, token)` produces
`{App:OrganisationRegisterBaseUrl}?token={token}` — no `org=` slug parameter, unlike
`StaffLinkBuilder`/`ParentLinkBuilder`'s existing `?token=...&org=...` shape.

**Rationale**: Staff/parent invitation links need the org slug because `TenantMiddleware` must
resolve a schema before an authenticated-adjacent tenant-exempt endpoint can look anything up.
An organisation invitation is different in kind: no tenant/schema exists yet — `Invitation`
lives in the Public schema and is looked up by token hash alone
(`RegisterOrganisationCommandHandler`), exactly like `EnrollmentLinkBuilder.BuildPublicEnrollmentUrl`'s
simpler org/location-slug-only shape (also no token) demonstrates a link format is tailored to
what its target actually needs to resolve, not a fixed template. Config key follows the same
`App:XxxBaseUrl` convention as `EnrollmentLinkBuilder`'s `App:PublicEnrollmentBaseUrl`, defaulting
to `http://localhost:3000/register` for local dev.

**Alternatives considered**: Reusing `AuthLinkBuilder`'s deep-link (`childcare://...`) scheme —
rejected; this is a browser-based web page (director-web), not a mobile app deep link, so it
needs an `http(s)://` URL like `EnrollmentLinkBuilder`'s, not a custom URI scheme.

## R9 — Invitation email is a new, locale-aware send path (Principle IV)

**Decision**: Add `IEmailSender.SendOrganisationInvitationAsync(string toEmail, string locale,
string? organisationNameNote, string registerUrl)` — locale-aware from day one, not reusing the
older English-only `SendStaffInvitationAsync`/`SendParentInvitationAsync` pattern.

**Rationale**: `IEmailSender.cs`'s own doc comments are explicit that the English-only gap on
`SendStaffInvitationAsync`/`SendWaitingListOfferedAsync`/`SendParentInvitationAsync` is an
"accepted, known gap left out of scope by feature 020... owns the templating/i18n rework only
for the new send paths below" — meaning post-020 additions are expected to be properly
localized. This is a brand-new send path added well after 020 (2026-07-23), so it follows the
newer convention already established by `SendTourInvitationAsync`/`SendContractSigningInvitationAsync`
(both take an explicit `locale` parameter). Since there's no existing contact/locale record for
a brand-new prospective director (unlike those two, which resolve locale from an existing
contact), the platform-admin picks the language explicitly at invitation-creation time
(spec.md FR-001), defaulting to Dutch — this codebase's primary market (matches
`SendPublicEnrollmentPage`'s own Dutch default).

**Alternatives considered**: Reusing the English-only `SendStaffInvitationAsync` shape for
speed — rejected: Principle IV is NON-NEGOTIABLE, and per `IEmailSender.cs`'s own comments this
specific gap was already scoped as closed for anything built after feature 020.

## R10 — Shared platform-admin shell: extract, don't rebuild 013h's screen

**Decision**: Add `web/app/(app)/platform-admin/layout.tsx` rendering a small section-local nav
(Invitations / Organisations / Vaccine Types) around `{children}`; move `PLATFORM_ADMIN_NAV`
in `web/components/Sidebar.tsx` from a single object to an array of three entries, each still
gated by the existing `session.user.isPlatformAdmin` check and rendered in the same bordered
"whole platform" section 013h already established. `vaccine-types/page.tsx`'s own content is
untouched — it simply now renders inside the new shared layout instead of being the only thing
on the page.

**Rationale**: Direct implementation of spec.md's FR-015/User Story 5 and the constitution's
monolith-first principle — a shell, not a rebuild. 013h's screen already works correctly; this
feature's job is only to stop it being a special case.

**Alternatives considered**: A dedicated `usePlatformAdminNav()` hook duplicating the array
inside each page instead of a shared layout — rejected, a Next.js layout is the idiomatic,
lower-duplication mechanism for "shared chrome around a route group," and this app already uses
route-group layouts elsewhere (`(app)/layout.tsx`).

## R11 — Endpoint/response naming conventions

**Decision**:

- `GET|POST /api/platform-admin/invitations`, `POST /api/platform-admin/invitations/{id}/resend`,
  `POST /api/platform-admin/invitations/{id}/revoke` — mirrors
  `PlatformAdminVaccineTypeEndpoints.cs`'s existing `/api/platform-admin/vaccine-types` shape
  (resource-scoped action suffixes, not verbs in the resource path).
- `GET /api/platform-admin/organisations` — same prefix convention, read-only (no POST/PATCH).
- Response DTOs: `PlatformAdminInvitationResponse`, `PlatformAdminOrganisationResponse` —
  matches the existing `PlatformAdminVaccineTypeResponse` naming pattern exactly.

**Rationale**: Consistency with the one existing precedent in this exact namespace avoids two
different conventions for "a platform-admin-only resource" three months apart.

## R12 — Invitation creation gets its own attribution fields, distinct from revoke

**Decision**: Add `CreatedByUserId (Guid?)` / `CreatedByEmail (string?)` to `Invitation`, populated
at creation time from the acting platform-admin's claims — the same `ActingUserOf(HttpContext)`
pattern used for revoke, applied a second time for create.

**Rationale**: Found during `/speckit-checklist`'s security/audit-integrity pass (CHK001):
spec.md's FR-008 explicitly promises attribution for create, not just resend/revoke, but the
first draft of data-model.md only had revoke-side fields — a real, fixable gap, not deferred.
Nullable (not required) specifically so the migration needs no backfill for feature 001's
pre-existing invitation rows created before this column existed.

**Alternatives considered**: Making the fields `NOT NULL` and backfilling existing rows with a
placeholder ("system"/"unknown") — rejected as a fabricated audit record is worse than an
honestly-absent one; a null creator on a pre-feature row is accurate, not a data quality defect.

## R13 — Registration endpoint needs rate limiting for the first time

**Decision**: Add a `RequireRateLimiting("organisation-register")` policy to
`POST /api/organisations/register`, mirroring `PublicEnrollmentEndpoints.cs`'s existing
`RequireRateLimiting("public-enrollment")` policy and `RateLimiterPolicies`'s established
options-extraction pattern (a standalone options class so a unit test can exercise the real
`SlidingWindowRateLimiter` directly, per feature 023's own precedent for testing rate limiting
without touching the codebase-wide `AddRateLimiter` Testing-environment disablement).

**Rationale**: Found during `/speckit-checklist`'s security pass (CHK002): this endpoint has
existed since feature 001 with zero rate limiting, because it was never actually reachable by
real traffic — no page anywhere called it. This feature is what changes that. Leaving it
unprotected the moment it becomes genuinely public would be a real regression in security
posture introduced by this feature, not a pre-existing, unrelated gap — the same reasoning
`PublicEnrollmentEndpoints.cs` already applied to an equivalent newly-public write path.

**Alternatives considered**: Relying solely on the token's cryptographic strength (64 random
bytes, research.md/data-model.md's entropy note) as sufficient defense — rejected; rate limiting
defends against a different threat (generic volumetric abuse/spam submissions, not just token
guessing), and the codebase already has an established, cheap-to-apply pattern for exactly this
class of endpoint.

## R16 — New MediatR commands for the platform-admin invitation path, not an extension of `CreateInvitationCommand`

**Decision**: `CreatePlatformAdminInvitationCommand`/`ResendPlatformAdminInvitationCommand`/
`RevokePlatformAdminInvitationCommand` are new command classes in
`backend/ChildCare.Application/Invitations/`, distinct from the existing `CreateInvitationCommand`
(unchanged).

**Rationale**: Found during `/speckit-analyze`'s cross-artifact pass — an earlier plan.md draft
said to "extend" the existing `CreateInvitationCommand`, which would have been wrong: that
command is the one `POST /api/admin/invitations` (`AdminEndpoints.cs`) uses, gated by the
separate `SuperAdmin` static-API-key scheme — an explicitly out-of-scope mechanism this feature
must not touch (BACKLOG.md's 032 block: "Changing... the SuperAdmin ops-key scheme used
elsewhere in AdminEndpoints.cs" is Out of Scope). Adding `OrganisationNameNote`/`Locale`/
platform-admin-specific attribution handling into that shared command would have leaked new
behavior into an unrelated, unchanged endpoint. The `Invitation` **entity** is still shared
(R1) — only the entity, not the command, is reused.

**Alternatives considered**: Extending `CreateInvitationCommand`/`CreateInvitationCommandHandler`
directly, gating the new fields behind an optional parameter — rejected: the ops-key endpoint
has no reason to ever set `Locale`/`OrganisationNameNote`/attribution, and giving it the
capability anyway (even unused) blurs a boundary BACKLOG.md deliberately drew.
