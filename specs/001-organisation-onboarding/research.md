# Phase 0 Research: Organisation Onboarding

Each decision below resolves a technical unknown from the Technical Context. Format: Decision / Rationale / Alternatives considered.

## R1. Introduce the full 5-project solution structure now, not later

**Decision**: Add `ChildCare.sln` plus `ChildCare.Domain`, `ChildCare.Application`, `ChildCare.Infrastructure`, `ChildCare.Contracts` alongside the existing `ChildCare.Api`. All new code for this feature lives in the new projects; the existing `ChildCare.Api` (AppDbContext, AuthEndpoints, HabitEndpoints, AuthService, JwtService, etc.) is left untouched.

**Rationale**: Constitution Principle VII names exactly these five projects as non-negotiable. This feature's own constraints (MediatR + FluentValidation pipeline, no business logic in endpoint handlers) are unbuildable cleanly inside the current flat `ChildCare.Api` project. Since this is the first feature built after the constitution was ratified, deferring the restructure would mean writing throwaway code now and re-doing it in feature 002.

**Alternatives considered**: Keep everything in `ChildCare.Api` a little longer and restructure in feature 002 (multi-tenancy scaffold) — rejected because MediatR/FluentValidation is an explicit requirement for *this* feature's registration command, not just 002's tenant-routing concern.

## R2. Scope boundary with existing AppDbContext / AuthEndpoints / HabitEndpoints

**Decision**: This feature does not delete, modify, or extend `AppDbContext`, `AuthEndpoints.cs`, `AuthService.cs`, or `HabitEndpoints.cs`. It only *adds* new, parallel infrastructure (`PublicDbContext`, a minimal `TenantDbContext` used solely for provisioning, new entities, a new registration endpoint group).

**Rationale**: BACKLOG.md explicitly assigns "remove AppDbContext entirely" and "delete HabitEndpoints.cs" to feature `002-multi-tenancy-scaffold`, not `001`. The constitution's Development Workflow section says the same leftovers are "to be replaced/removed as part of the tenancy work — do not extend them." Keeping 001 purely additive avoids a large, risky refactor happening in the wrong feature and keeps the existing (working) auth skeleton stable until 002/003 rework it deliberately.

**Near-miss during `/speckit-implement`**: `Program.cs`'s dev-only auto-migrate block initially resolved the new `PublicDbContext` via `GetRequiredService`, alongside the pre-existing `AppDbContext` resolution. This broke the pre-existing `ChildCareWebAppFactory`-based tests (`AuthEndpointTests`, `HabitEndpointTests` — 29 tests failing), which don't register `PublicDbContext` and, per this very decision, shouldn't need to. Fixed by resolving it via `GetService` (optional) and only migrating if non-null — `OrganisationOnboardingWebAppFactory` (this feature's own test factory) migrates `PublicDbContext` explicitly in its own setup instead. All 41 tests (29 pre-existing + 12 new) pass after the fix.

**Alternatives considered**: Do the AppDbContext removal now since it's touched anyway — rejected; it inflates this feature's blast radius and duplicates work BACKLOG already scheduled for 002.

## R3. New endpoint routes (not under `/api/auth`)

**Decision**: Two new Minimal API endpoint groups: `POST /api/admin/invitations` (operator-only) and `POST /api/organisations/register` (invitation-gated director registration). Both use the existing `/api` prefix convention already used by `/api/auth/*`.

**Rationale**: `/api/auth/*` remains untouched (R2) and still targets the legacy `AppDbContext.Users`; reusing it for org registration would conflate two different data models. The user's tech-constraint note said `POST /admin/invitations` (no `/api` prefix); this plan applied the existing `/api` prefix for consistency with the rest of the API. **Confirmed** by the user post-`/speckit-analyze` (finding F3) — `/api` prefix is correct, no change needed.

**Alternatives considered**: Reuse `/api/auth/register` and redefine its semantics — rejected, would break the existing skeleton's working registration path before 002/003 are ready to replace it.

## R4. Invitation token design

**Decision**: An opaque, cryptographically random token (64 random bytes, base64/url-safe encoded), stored **hashed** (SHA-256) in the `invitations` table — never stored or logged in plaintext. Columns: `Id (uuid)`, `Email (text, normalized lowercase)`, `TokenHash (bytea)`, `ExpiresAt (timestamptz)`, `CreatedAt (timestamptz)`. No `UsedAt` column — see R10 for why "used" is derived from the `Tenant` relationship instead of a mutable flag on `Invitation`.

**Rationale**: This mirrors the refresh-token pattern already proven in `AuthService`/`JwtService` (`GenerateRefreshToken()` + hashed storage) — same crypto primitives, same team familiarity, no new dependency. A self-contained signed JWT was considered but rejected: every invitation must be checked against the DB for "already used" regardless (FR-004), which erases the main benefit of a stateless JWT (avoiding a DB round-trip) while adding key-management complexity for a short-lived, low-volume, DB-backed record.

**Alternatives considered**: JWT-signed invitation (self-contained, statelessly verifiable) — rejected per above.

## R5. Email-lock enforcement (FR-018)

**Decision**: The invitation is looked up by its token; the handler then compares the invitation's stored `Email` to the registration request's submitted email using a case-insensitive exact match. Mismatch → reject before touching any organisation/workspace data (mirrors FR-005's ordering).

**Rationale**: Directly implements the clarified decision that invitations are locked to one specific email address, independent of the password.

**Resolved (post-`/speckit-analyze`, finding F2)**: rejection status codes are now decided rather than deferred. An unresolvable token (not found, expired, or already used) returns **404** with a single generic error key (`errors.invitation.not_found`) for all three cases — deliberately not distinguishing them, so a caller can't enumerate which tokens have ever existed, expired, or been claimed. An email mismatch on a token that *does* resolve returns **422** — this is not folded into the 404 case because the caller already possesses a real, valid invitation at that point, so a specific "wrong email" message doesn't leak anything about other tokens. See contracts/register-organisation.md for the finalized response shapes.

## R6. Dynamic schema provisioning & baseline migration mechanism

**Decision**: `TenantDbContext` is *not* wired into the ASP.NET Core request pipeline in this feature (that's 002's `TenantMiddleware`). It exists purely as a provisioning tool: constructed per-invocation with a specific schema name, using a custom `IModelCacheKeyFactory` (EF Core normally caches one compiled model per `DbContext` type; the default schema must be part of the cache key so each tenant's schema name doesn't reuse another tenant's cached model) and `modelBuilder.HasDefaultSchema(schemaName)` in `OnModelCreating`. Provisioning flow: `CREATE SCHEMA IF NOT EXISTS "<schema_name>"` via raw SQL, then `context.Database.Migrate()` against that schema (which also creates that schema's own `__EFMigrationsHistory` row, since the history table itself falls under the default schema). The set of migrations applied is a small "baseline" set (e.g., an initial `users` table) authored and reviewed like any other EF Core migration.

**Rationale**: This is the standard, minimal-dependency technique for schema-per-tenant with EF Core, and it satisfies BACKLOG's explicit requirement ("Baseline EF Core migrations applied to that new schema") without introducing a third-party multi-tenancy package.

**Alternatives considered**: Finbuckle.MultiTenant (popular multi-tenancy NuGet package) — rejected for Phase 1; its tenant-resolution-strategy surface area is aimed at request-time routing (002's problem, not 001's), and Constitution Principle VII (monolith-first, avoid premature abstraction) favors a small hand-rolled provisioning path we fully control, especially given the hard `search_path` / non-pooled-connection constraint. Can be reconsidered later if the hand-rolled approach proves fragile.

## R7. Constitution Principle VI tension: "migrations MUST NOT auto-apply in production"

**Decision**: Distinguish *authoring* migrations from *applying* them to a brand-new, empty schema. The baseline migration's SQL is authored via `dotnet ef migrations add` and is reviewable in the PR diff like any other code change (satisfying the spirit of "generated and reviewed"). What happens automatically at registration time is applying that already-reviewed SQL to a schema that has **zero existing data and zero other tenants' blast radius** — structurally different from Principle VI's target risk (an unreviewed, blind auto-migrate against a shared production schema with live data). Rolling an already-reviewed migration out to *existing* tenant schemas remains a separate, deliberate operation (feature 002's cross-tenant migration mechanism), not something 001 does.

**This is flagged in Complexity Tracking below for explicit user sign-off** — it is a real, load-bearing reading of a NON-NEGOTIABLE-adjacent principle, not a wording nitpick, and deserves confirmation rather than being silently assumed.

**Alternatives considered**: Generate a SQL script per registration and require an operator to run it manually — rejected outright; directly contradicts FR-008/FR-011 (registration must complete synchronously with zero manual operator steps).

## R8. JWT issuance at the end of registration (auto-login)

**Decision**: `RegisterOrganisationCommand`'s handler mints an access token directly in its response, using a **new** overload `JwtService.GenerateAccessToken(Guid userId, string email, Guid tenantId)` that takes primitive claim values instead of the legacy `ChildCare.Api.Models.User` type, and includes a `tenant_id` claim. This overload is additive; the existing `User`-typed overload used by `/api/auth/*` is untouched. To keep dependency direction correct (Domain/Application must not reference the legacy `ChildCare.Api.Services` types), `ChildCare.Application` defines a small port `IAccessTokenIssuer`; `ChildCare.Api` provides the adapter implementation that delegates to `JwtService`.

**Rationale**: FR-011/SC-002 require reaching a *working, logged-in* dashboard in one synchronous flow — but the general login endpoint (looking up an existing user's tenant by email) is explicitly feature 003's scope, and it solves a different problem (find the tenant for a user who already exists somewhere unknown). Registration already knows exactly which tenant/user it just created, so minting the token immediately is simpler than requiring a second round-trip to a login endpoint that doesn't exist yet.

**Alternatives considered**: Have the director call a (not-yet-built) login endpoint after registering — rejected; breaks FR-011's "no additional step" requirement and creates a hard dependency on feature 003 before 001 can be considered done.

## R9. Plan tier & provisioning status storage

**Decision**: `Plan` and `ProvisioningStatus` are stored as `text` columns with a Postgres `CHECK` constraint enumerating allowed values (`trial|starter|pro` and `provisioning|ready|failed` respectively), mapped in C# via `.HasConversion<string>()` on a normal enum. `ProvisioningStatus` gains a `failed`/`provisioning` intermediate value beyond the spec's `ready` to support FR-014's retry detection.

**Rationale**: A native Postgres enum type adds migration friction (`ALTER TYPE ... ADD VALUE` has transaction restrictions) for a 3–4 value field that will rarely change; `text` + `CHECK` is simpler to evolve and just as safe.

**Alternatives considered**: Native Postgres `ENUM` type — rejected per above.

## R10. Concurrency control for FR-015, reconciled with FR-014's retry requirement

**Decision (superseding an earlier draft of this research)**: "Used" is **not** a flag mutated on `Invitation` at the start of an attempt — that would permanently burn the invitation the moment a request begins, even if provisioning then fails, breaking FR-014's "safely retried to completion" guarantee. Instead:

- `Tenant` gains a `CreatedFromInvitationId (uuid, UNIQUE, FK → invitations.Id)` column (this supersedes data-model.md's earlier note that this FK could be omitted "per YAGNI" — it turns out to be load-bearing for correctness, not just traceability).
- The registration handler attempts an atomic, conditional insert:

  ```sql
  INSERT INTO tenants (..., created_from_invitation_id, provisioning_status, ...)
  VALUES (..., @invitationId, 'provisioning', ...)
  ON CONFLICT (created_from_invitation_id) DO NOTHING
  RETURNING *;
  ```

- **Row returned** → this request won the race; proceed to provisioning (schema create + migrate + director user) against this new `Tenant` row.
- **No row returned** (unique conflict) → another attempt already exists for this invitation. Re-select the existing `Tenant` by `created_from_invitation_id`:
  - `ProvisioningStatus = 'ready'` → reject: invitation already used to complete a registration (FR-004).
  - `ProvisioningStatus IN ('provisioning', 'failed')` → this is a legitimate retry of an incomplete attempt (FR-014): resume provisioning against the **existing** `Tenant` row (schema creation is `CREATE SCHEMA IF NOT EXISTS`, EF Core migration application is naturally idempotent/resumable, and the director-user insert is an upsert-by-email-within-schema) — never create a second `Tenant`.

**Rationale**: The Postgres `UNIQUE` constraint on `created_from_invitation_id` is the sole arbiter of "who won," enforced atomically by the database regardless of read-then-write race windows or how many Cloud Run instances are running concurrently — this satisfies FR-015 exactly. Deriving "used" from "a `ready` `Tenant` exists for this invitation" (rather than a separately-mutated timestamp) eliminates the class of bug where the two facts (invitation marked used vs. registration actually completed) could drift apart, which is exactly what broke FR-014 in the original draft of this decision.

**Alternatives considered**: A conditional `UPDATE invitations SET used_at = now() WHERE used_at IS NULL` claimed *before* provisioning begins — rejected: correctly solves FR-015 in isolation but permanently burns the invitation on any transient provisioning failure, violating FR-014. Distributed locks / in-process synchronization — rejected as unnecessary; a single unique constraint is simpler and works correctly across multiple Cloud Run instances without any additional coordination mechanism.

## R11. Operator credential for invitation issuance (FR-002, FR-017)

**Decision**: `POST /api/admin/invitations` is gated by a static shared-secret header (e.g., `X-Superadmin-Key`), compared using a constant-time comparison against a configured value (`SuperAdmin:ApiKey`, env var `SuperAdmin__ApiKey`), sourced from GCP Secret Manager in deployed environments. Terraform changes: a new `google_secret_manager_secret` + `google_secret_manager_secret_version` resource, an IAM binding granting the Cloud Run service's runtime service account `roles/secretmanager.secretAccessor`, and the Cloud Run service's env var block switched from a plain `value` to a `value_source.secret_key_ref` for this variable (mirroring the existing `Jwt__Secret`/`Stripe__SecretKey` pattern, but via Secret Manager instead of a plain Terraform variable — the existing secrets are *not* migrated to Secret Manager as part of this feature, to keep scope bounded).

**Rationale**: Directly implements the clarified decision (restricted internal capability, credential in a secrets manager, explicitly temporary Phase 1 measure pending real super-admin auth in Phase 2).

**Alternatives considered**: Reusing the existing JWT bearer auth with a new policy — rejected for Phase 1; there is no "operator" user/role concept yet (roles are 003's scope), and introducing one just for this single endpoint would be more machinery than the temporary measure needs.

## R12. Password policy

**Decision**: Reuse the existing convention already enforced on `/api/auth/register` — minimum 8 characters — as a FluentValidation rule on `RegisterOrganisationCommand`, hashed with `BCrypt.Net-Next` (already a dependency), consistent with the existing `AuthService` pattern.

**Rationale**: Consistency with the one password rule that already exists in the codebase; no new requirement was raised in spec.md or BACKLOG.md that would justify a stricter policy for directors specifically.

## R13. Documented deviation: EF Core version

**Note (not a decision requiring action)**: PROJECT-BRIEF.md states "Entity Framework Core 9," but the walking skeleton already targets `net10.0` with `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2` / `Microsoft.EntityFrameworkCore.Design 10.0.8`. This plan continues on the already-installed EF Core 10 rather than downgrading a working skeleton; flagged here so it's a documented, deliberate continuation rather than an oversight.

## R14. Slug generation & collision handling

**Decision**: `Slug` is derived from the organisation name (lowercased, ASCII-normalized, non-alphanumerics replaced with `-`). On a uniqueness collision, append a short random suffix (4 base32 chars) automatically and retry once; do not surface a "pick another org name" step to the director.

**Rationale**: FR-011 requires registration to complete without extra manual steps; auto-resolving a rare slug collision keeps that guarantee. Organisation names are not required to be globally unique by the spec — only the derived slug (used as part of the schema name) needs to be.

## R15. Implementation-time correction: EF Core migrations don't respect runtime `HasDefaultSchema()`

**Discovered during `/speckit-implement`**: R6's original plan assumed a single set of `TenantDbContext` migrations could be "replayed" per tenant by constructing the context with a different runtime schema name each time. This is wrong — EF Core bakes the schema name into a migration's generated `Up()`/`Down()` C# source as a literal string at `dotnet ef migrations add` time; changing `HasDefaultSchema()` on a later `DbContext` instance does not retroactively change that already-generated file.

**Decision**: `TenantProvisioningService` generates the baseline migration SQL at runtime via `IMigrator.GenerateScript()` against the "tenant_template" placeholder schema (the same one `TenantDbContextFactory` scaffolds against), then does a literal string substitution of `"tenant_template"` → the real tenant schema name before executing the result — this is the standard, documented technique for EF Core schema-per-tenant with a single shared migration source. The migrations-history table is explicitly configured (`MigrationsHistoryTable("__EFMigrationsHistory", schemaName)`) so it lands inside each tenant's own schema too, not the `public` schema — this keeps per-tenant migration-pending detection working correctly for feature 002's future rollout mechanism.

**Rationale**: Generating the script from the compiled migration classes at runtime (rather than checking in a duplicate static `.sql` file) keeps the C# migration files as the single source of truth — no risk of the two drifting apart if a future migration is added.

## R16. Implementation-time correction: `IPublicDbContext` / `ITenantProvisioningService` ports

**Discovered during `/speckit-implement`**: plan.md's Project Structure had `CreateInvitationCommandHandler` (in `ChildCare.Application`) depend on `PublicDbContext` directly — but `PublicDbContext` lives in `ChildCare.Infrastructure`, which already depends on `ChildCare.Application` for command/handler types. That would be a circular project reference, which .NET rejects outright.

**Decision**: Added two ports to `ChildCare.Application/Common/` — `IPublicDbContext` (exposing the `Tenants`/`Invitations` `DbSet<T>`s and `SaveChangesAsync`) and `ITenantProvisioningService` (mirroring `TenantProvisioningService`'s public method) — mirroring the `IAccessTokenIssuer` pattern already planned for R8. `ChildCare.Infrastructure`'s `PublicDbContext` and `TenantProvisioningService` implement these interfaces; handlers in `ChildCare.Application` depend only on the interfaces. This requires `ChildCare.Application` to reference the `Microsoft.EntityFrameworkCore` abstractions package (for the `DbSet<T>` type in the port), but not `Npgsql.EntityFrameworkCore.PostgreSQL` or any concrete provider.

**Rationale**: This is the standard Clean Architecture resolution for this exact shape of problem (see e.g. the common `IApplicationDbContext` pattern) — Application defines what it needs, Infrastructure provides it, and the dependency arrow only ever points inward.

## R17. Implementation-time correction: director-row upsert must return the *actual* persisted Id

**Discovered during `/speckit-implement`**, while writing the FR-015 concurrency test: under a genuine simultaneous race, the losing `RegisterOrganisationCommandHandler` call resumes provisioning against the winner's `Tenant` row (research.md R10) but still generates its *own* candidate `directorUserId` locally. The director-row upsert was `ON CONFLICT ("Email") DO NOTHING` — so the loser's candidate Id would never be persisted, yet its response would still hand back an access token containing that phantom, never-stored Id.

**Decision**: Changed the upsert to `ON CONFLICT ("Email") DO UPDATE SET "Email" = EXCLUDED."Email" RETURNING "Id"` (a no-op update that still triggers `RETURNING`) so `TenantProvisioningService.ProvisionAsync` returns the *actual* persisted director Id — which callers now use for the access token and response, discarding their own candidate Id if it lost the race. `ITenantProvisioningService.ProvisionAsync` signature changed from `Task` to `Task<Guid>` accordingly.

**Rationale**: FR-015 requires "at most one... director account" — true either way — but a response containing a token for a user that was never actually written to the tenant's `users` table would be a real, if narrow, correctness bug for whichever request lost the race. Verified by `OrganisationOnboardingResilienceTests.Register_WithConcurrentAttempts_CreatesExactlyOneTenant`.

## R18. Flagged during manual quickstart.md validation: MediatR requires a commercial license for production use

**Observed running the app locally (T055)**: startup logs a warning — *"You do not have a valid license key for the Lucky Penny software MediatR. This is allowed for development and testing scenarios. If you are running in production you are required to have a licensed version."* MediatR (the package BACKLOG.md and this plan both name explicitly) moved to a dual-license model in recent major versions; this repo picked up MediatR 14.2.0 (T004), which requires a paid license for production deployment.

**Not a decision — flagging for the user**: this needs a real choice before feature 001 (or any MediatR-dependent feature) reaches production: (a) purchase a MediatR license (see lucky­pennysoftware.com), or (b) migrate to a free/MIT alternative (e.g., `Mediator` by martinothamar, a source-generator-based drop-in with a very similar API) before the first production deploy. Development/testing/CI are unaffected either way — the warning is explicitly non-blocking there. Not resolved here since it's a licensing/budget decision, not an engineering one.
