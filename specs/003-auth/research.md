# Phase 0 Research: Authentication & Role-Based Authorization

Each decision below resolves a technical unknown from the Technical Context. Format: Decision / Rationale / Alternatives considered.

## R1. Organisation identifier — reuse the existing `Tenant.Slug`; require it on every email-identifying pre-auth request

**Decision**: `Login`, `Google`, `Apple`, `Refresh`, and `ForgotPassword` requests all gain a required `OrganisationSlug` field. Resolution: `publicDb.Tenants.FirstOrDefaultAsync(t => t.Slug == slug)` (slugs are generated lowercase by the existing `OrganisationSlugGenerator`, feature 001 — no case-folding needed beyond trimming), then check `ProvisioningStatus == Ready`. Both "slug not found" and "not ready" collapse into one generic rejection (`errors.auth.organisation_not_found`, 404) — see R9 for the full error-key list. `VerifyEmail`/`ResetPassword` requests instead recover the slug from a query parameter embedded in the emailed link at generation time (R2) — the token itself is already the identifying secret at that point, so requiring the user to separately know/re-enter their org slug would add friction with no security benefit. `Logout`/`DeleteAccount`/`ResendVerification` are unaffected — they are non-exempt, authenticated routes that already resolve tenant context correctly through `TenantMiddleware`/`ICurrentTenantService` (feature 002), not through this pre-auth path at all.

**Rationale**: `Tenant.Slug` already exists (feature 001), is already uniquely indexed (`PublicDbContext`'s `HasIndex(x => x.Slug).IsUnique()`), and is exactly the kind of identifier multi-tenant SaaS products already expose to users non-secretly (a workspace/organisation slug is not sensitive the way a password or even an email is — it's typically visible in a URL or typed at a "which organisation?" login step). Reusing it avoids introducing a second, parallel tenant-identifier concept. This directly implements the spec's resolved clarification: the client always supplies which organisation to authenticate against.

**Alternatives considered**: A public-schema email→tenant index returning a list of matching tenants for ambiguous resolution (the shape originally sketched in BACKLOG.md before clarification) — rejected per the resolved clarification: the client supplying the org upfront is simpler, avoids a second round-trip, and avoids ever needing to enumerate "which organisations does this email exist in" as a server response (a shape that itself risks leaking account-existence information across organisations).

## R2. Password-reset / email-verification links carry the organisation slug as a query parameter

**Decision**: `ForgotPasswordCommand`/`ResendVerificationCommand`'s link-building helpers append `&org={tenant.Slug}` to the existing deep-link URL (`{resetBase}?token={token}&org={slug}`, `{verifyBase}?token={token}&org={slug}`). `ResetPasswordRequest`/`VerifyEmailRequest` DTOs gain a required `OrganisationSlug` field, which the client reads back out of the link it received (query param) and includes in the POST body alongside the token.

**Rationale**: Satisfies FR-016's allowance for token-context-based org recovery instead of requiring the user to re-supply an org identifier they may not have typed anywhere in this flow (a password-reset request only asks for an email). The alternative — a public token→tenant index — would resurrect exactly the kind of cross-tenant lookup table the resolved clarification for login deliberately avoided, for no added benefit (the token is already a 32-byte random secret; embedding a non-secret slug alongside it in a link the server itself generated leaks nothing new).

**Alternatives considered**: A public-schema `password_reset_tokens`/`email_verification_tokens` index table mapping token → tenant — rejected as unnecessary indirection; the emailed link is entirely under this system's control, so it can simply carry the slug itself.

## R3. `Role` — new `UserRole` enum on `TenantUser`, same string-conversion + CHECK-constraint pattern as `Tenant.Plan`/`ProvisioningStatus`

**Decision**: Add `ChildCare.Domain.Enums.UserRole { Director, Staff, Parent }`. Add `TenantUser.Role` (non-nullable). In `TenantDbContext.OnModelCreating`, configure it exactly like `PublicDbContext` already configures `Tenant.Plan`/`ProvisioningStatus`: `.HasConversion(v => v.ToString().ToLowerInvariant(), v => (UserRole)Enum.Parse(...))`, `.HasMaxLength(20)`, `.IsRequired()`, plus a table-level `CHECK ("Role" IN ('director','staff','parent'))`. The new tenant-schema migration adds the column `NOT NULL DEFAULT 'director'` (backfills every existing row as Director, per spec.md's Assumption — every `TenantUser` row that exists today was created by organisation onboarding, which only ever creates directors), then the code-level default reverts to requiring an explicit value on every future insert (the SQL-level `DEFAULT` only matters for the one-time backfill of pre-existing rows; new inserts always specify `Role` explicitly). `TenantProvisioningService`'s raw-SQL director upsert (`INSERT INTO users (...)`) is updated to include `"Role"` = `'director'` explicitly, so it no longer depends on the migration's now-legacy default for *new* tenants either.

**Rationale**: Mirrors an established, working pattern in this exact codebase (`PublicDbContext`'s `Plan`/`ProvisioningStatus` — see feature 001) rather than inventing a new enum-storage convention. A CHECK constraint gives defense-in-depth against a bad value ever landing in the column even if application-level validation is bypassed (e.g. a future direct SQL script).

**Alternatives considered**: A separate `roles`/`user_roles` join table (supporting multiple roles per account) — rejected; spec.md FR-012 is explicit that every account has *exactly one* role, and BACKLOG.md's own edge case ("a director is also a staff member... the role field allows both roles on the same account" — feature 005) refers to a *person* potentially having two separate `TenantUser` accounts/assignments, not one account with two roles simultaneously; a single-column enum is the correct, simpler shape for what's actually being modeled here.

## R4. JWT role claim — extend the existing `IAccessTokenIssuer` port rather than adding a parallel one

**Decision**: `IAccessTokenIssuer.IssueAccessToken(Guid userId, string email, Guid tenantId, string role)` gains a `role` parameter (breaking change to the one existing call site, `RegisterOrganisationCommandHandler`, updated to pass `"director"`). `JwtService.GenerateAccessToken` adds a `new Claim(ClaimTypes.Role, role)` claim — using `ClaimTypes.Role` specifically (not a custom `"role"` claim name) because ASP.NET Core's policy builder `RequireRole(...)` and `User.IsInRole(...)` both read `ClaimTypes.Role` by convention; using the standard claim type means R5's policies need no custom claim-matching logic. The port also gains `string IssueRefreshToken()` and `int RefreshTokenExpiryDays`, moved from direct `JwtService` access in the old `AuthService` — every one of the new MediatR command handlers (R8) needs both access- and refresh-token issuance, and depending on one port instead of two (`IAccessTokenIssuer` + a hypothetical separate `IRefreshTokenIssuer`) avoids proliferating near-identical single-method interfaces for what is really one cohesive "mint this session's tokens" responsibility.

**Rationale**: `IAccessTokenIssuer` already exists specifically to let `ChildCare.Application` mint tokens without depending on `ChildCare.Api`'s concrete `JwtService` (research.md R8, feature 001) — extending it is strictly additive to an established seam, not a new architectural decision.

**Alternatives considered**: A custom `"role"` claim type — rejected, it would require every future feature's policy/authorization code to know to check a non-standard claim name instead of using the framework's built-in `RequireRole`/`IsInRole` support.

## R5. Authorization policies — `RequireRole`, registered alongside the existing `SuperAdmin` policy

**Decision**: In `Program.cs`'s existing `AddAuthorization` block:

```csharp
options.AddPolicy("DirectorOnly",     p => p.RequireRole("director"));
options.AddPolicy("StaffOrDirector",  p => p.RequireRole("staff", "director"));
options.AddPolicy("ParentOnly",       p => p.RequireRole("parent"));
```

No custom `IAuthorizationRequirement`/handler is needed — `RequireRole` already fails closed (a user with no matching role claim, or no role claim at all, is refused) and already returns 403 (not 401) for an authenticated-but-unauthorized request, exactly matching FR-014's requirement.

**Rationale**: The simplest mechanism that satisfies FR-013/FR-014 exactly, using framework-native building blocks rather than a bespoke requirement/handler pair that would need its own tests to prove the same fail-closed behavior `RequireRole` already guarantees.

**Alternatives considered**: A custom `IAuthorizationHandler` reading a raw string claim — rejected as unnecessary; `ClaimTypes.Role` (R4) is exactly what `RequireRole` expects.

## R6. `IEmailSender` port — `EmailService` implements it directly, no adapter class needed

**Decision**: Add `ChildCare.Application.Common.IEmailSender` with `SendEmailVerificationAsync(string toEmail, string verifyLink)` and `SendPasswordResetAsync(string toEmail, string resetLink)`. `ChildCare.Api.Services.EmailService` implements the interface directly (it already has exactly this shape and no dependency that would prevent it — only `IConfiguration`/`ILogger`, both available anywhere). Registered as `AddScoped<IEmailSender, EmailService>()`.

**Rationale**: `EmailService` doesn't need an adapter the way `JwtService` did (`JwtAccessTokenIssuer`) — its two public methods already match the shape the new `ForgotPasswordCommand`/`ResendVerificationCommand` handlers need, so implementing the interface directly is simpler than wrapping it.

**Alternatives considered**: Moving `EmailService` itself into `ChildCare.Infrastructure` (PROJECT-BRIEF.md's "external service clients" layer) — considered more architecturally consistent, but rejected for this feature's scope: it would pull the `MailKit`/`MimeKit` package references into `ChildCare.Infrastructure` for no behavioral change, a pure relocation better bundled with a future feature that touches `EmailService` for its own reasons, not forced into this one's diff.

## R7. Google/Apple token validation — extracted into `ChildCare.Infrastructure` behind two new ports

**Decision**: Add `IGoogleTokenValidator.ValidateAsync(string idToken) : Task<GoogleIdentity?>` and `IAppleTokenValidator.ValidateAsync(string identityToken, string bundleId) : Task<AppleIdentity?>` to `ChildCare.Application.Common` (simple result records: `GoogleIdentity(string Sub, string Email)`, `AppleIdentity(string Sub, string? Email)` — `null` return means "invalid token", matching the existing methods' current null-return-on-failure behavior). The concrete implementations (`GoogleTokenValidator`, `AppleTokenValidator` in `ChildCare.Infrastructure/Auth/`) are moved verbatim from `AuthService`'s existing `GoogleSignInAsync`/`VerifyAppleTokenAsync` logic (Google tokeninfo HTTP call via `IHttpClientFactory`; Apple JWKS fetch + `JsonWebTokenHandler` validation) — no behavioral change to the validation logic itself, only its location.

**Rationale**: PROJECT-BRIEF.md's Solution Structure explicitly assigns "external service clients" to `ChildCare.Infrastructure`; Google/Apple token validation is exactly that — an outbound call to a third-party identity provider. Moving it out of `AuthService` (deleted, R8) and behind a port keeps the new `GoogleSignInCommand`/`AppleSignInCommand` handlers in `ChildCare.Application` free of `IHttpClientFactory`/JWKS-specific dependencies, consistent with how `RegisterOrganisationCommandHandler` already depends only on ports (`IPublicDbContext`, `ITenantProvisioningService`, `IAccessTokenIssuer`), never concrete `ChildCare.Api`/`ChildCare.Infrastructure` types.

**Alternatives considered**: Leaving Google/Apple validation inline inside the new Application-layer command handlers (taking `IHttpClientFactory` directly) — rejected; it would be the first Application-layer code with a direct outbound-HTTP dependency, breaking the established "Application depends on ports, Infrastructure implements them" boundary the rest of the codebase (and PROJECT-BRIEF.md itself) follows.

## R8. `AuthService` deleted; its logic becomes ten MediatR commands under `ChildCare.Application/Auth/`

**Decision**: One command + FluentValidation validator + handler per flow, following the exact shape of `RegisterOrganisationCommand`/`...Validator`/`...Handler` (feature 001): `LoginCommand`, `RefreshTokenCommand`, `GoogleSignInCommand`, `AppleSignInCommand`, `LogoutCommand`, `DeleteAccountCommand`, `ResendVerificationCommand`, `VerifyEmailCommand`, `ForgotPasswordCommand`, `ResetPasswordCommand`. `AuthEndpoints.cs` handlers shrink to `ISender.Send(command)` plus HTTP-result mapping only — no business logic, closing the Principle III gap feature 002 explicitly deferred here.

Two different schema-resolution patterns apply, matching which routes are `[TenantExempt]` today (feature 002 research.md R3) and remain so:

- **Exempt-route handlers** (`Login`, `Refresh`, `GoogleSignIn`, `AppleSignIn`, `ForgotPassword`, `ResetPassword`, `VerifyEmail`) depend on `IPublicDbContext` (org-slug lookup, R1) + `ITenantDbContextResolver` (explicit `.ForSchema(...)` call once the tenant is resolved, exactly like today's shim, just with a real slug-based lookup instead of "earliest Ready tenant") + `IAccessTokenIssuer` (R4) + `IGoogleTokenValidator`/`IAppleTokenValidator` (R7, only the two OAuth commands) + `IEmailSender` (R6, only forgot-password/verification-related commands).
- **Non-exempt-route handlers** (`Logout`, `DeleteAccount`, `ResendVerification`) depend on the ordinary DI-registered, Scoped `ITenantDbContext` (already correctly resolved by `TenantMiddleware` for these routes, feature 002) — no explicit schema resolution needed, same as any other post-login feature's handlers will look.

**Rationale**: This is FR-010, following the one established precedent in the codebase (`RegisterOrganisationCommand`) rather than inventing a new command-shape convention. Splitting into ten single-purpose commands (rather than, say, one large `AuthCommand` with a discriminator) matches MediatR's intended usage and keeps each FluentValidation validator focused on exactly the fields one HTTP request actually sends.

**Alternatives considered**: Keeping `AuthService` as a thin facade that itself calls into MediatR internally — rejected; it would just relocate the Principle III violation one level down instead of removing it, and endpoint handlers would still not be the ones sending MediatR requests.

## R9. i18n error keys for the new/changed auth failure paths

**Decision**: Following `backend/ERROR_KEYS.md`'s established `errors.<area>.<reason>` convention:

| Key | HTTP Status | Trigger |
|---|---|---|
| `errors.auth.organisation_not_found` | 404 | `OrganisationSlug` on Login/Google/Apple/Refresh/ForgotPassword matches no tenant, or matches one whose `ProvisioningStatus != Ready` — deliberately collapsed (R1); slugs are not secret, so this is a distinguishable-from-credentials failure, unlike SC-005's email-existence concern |
| `errors.auth.invalid_credentials` | 401 | Email/password mismatch, unknown email within a correctly-resolved organisation, or a Google/Apple token that is valid but matches no existing account (R7/FR-009) — all collapsed into one generic key per SC-005 |
| `errors.auth.method_not_allowed_for_role` | 403 | FR-017 — a sign-in method not permitted for the resolved account's role (e.g. Google sign-in against a Staff-role account) |
| `errors.auth.token_invalid_or_expired` | 400 | Reset-password / email-verification token invalid, expired, or already used (replaces the current hardcoded English strings — Constitution Principle IV fix, see plan.md's Constitution Check) |

**Rationale**: Constitution Principle IV (NON-NEGOTIABLE). Two of today's `AuthEndpoints.cs` responses use raw English text instead of an `errorKey` — a pre-existing violation this feature's rewrite fixes as a side effect of touching this code anyway, rather than leaving it in place while everything around it is rewritten.

**Alternatives considered**: Reusing feature 002's `errors.tenant.*` keys for organisation-not-found here too — rejected; those keys are specifically for the post-login `TenantMiddleware` path (403, JWT-claim-based), while this is a pre-login, slug-based, 404-shaped failure — different HTTP semantics, different area prefix (`auth` vs `tenant`) keeps them distinguishable for whichever frontend eventually renders them.

## R10. `/api/auth/register` — deleted outright, not adapted

**Decision**: `AuthEndpoints.MapPost("/register", ...)`, `AuthService.RegisterAsync`, and the `RegisterRequest` DTO are all deleted. No replacement endpoint is added by this feature.

**Rationale**: FR-009 (spec.md Assumption, confirmed during this plan's codebase review): nothing in the organisation-onboarding flow (`RegisterOrganisationCommandHandler` → `TenantProvisioningService`, feature 001) ever calls this endpoint — it creates director accounts via its own raw-SQL upsert entirely independently. The endpoint is dead, open-registration-shaped code left over from the pre-tenancy walking skeleton.

**Alternatives considered**: Gating it behind an invitation token (mirroring organisation onboarding's pattern) to serve some future staff/parent self-completion step — rejected as out of scope; spec.md's own Assumption defers staff/parent account provisioning to features 005/006/012, which have not yet decided their own invitation UX. Building a speculative gated version of this endpoint now, before those features exist to call it, would guess at a shape that might not match what they actually need.

## R11. Connection mode, migration-rollout mechanism — unchanged from feature 002

**Decision**: No new decision needed. Continue using Neon's direct (non-pooled) connection string (constitution Principle I); the new tenant-schema migration (R3) rolls out to already-provisioned tenant schemas via feature 002's existing `migrate-tenants` CLI subcommand, unchanged.

**Rationale**: Restated for completeness of the Technical Context only — this feature introduces no new infrastructure surface.
