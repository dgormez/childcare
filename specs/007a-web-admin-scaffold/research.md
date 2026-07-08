# Research: Web Admin Scaffold

## R1 — API client migration: openapi-fetch over the existing `lib/api.ts`

**Decision**: Replace `web/lib/api.ts` (hand-written `fetch` wrapper) with an `openapi-fetch`
client generated from the backend's OpenAPI document, mirroring `mobile/services/apiClient.ts`
exactly (same 401-refresh-and-retry middleware shape, same generated-`paths`-typed client).

**Rationale**: The constitution and this feature's own constraints require reusing feature 008's
established client-generation pattern rather than a second bespoke wrapper. `web/`'s existing
`lib/api.ts` predates that decision (it's Habits-template legacy). A generated client also
type-checks every request path/body/query against the live backend contract, catching drift at
build time instead of runtime.

**Alternatives considered**:
- Keep `lib/api.ts` and hand-add new Staff/Devices methods — rejected: perpetuates an
  un-typed, manually-maintained client the constitution's tooling section (`openapi-typescript +
  openapi-fetch — no NSwag`) already picked a replacement for; feature 008 already proved the
  pattern works for a client app, this feature is the natural point to bring `web/` in line.
- NSwag-generated client — explicitly excluded by the constitution's Technology Stack
  Constraints.

**Web-specific difference from mobile**: mobile's client rewrites every request from a
placeholder origin to a runtime-configurable base URL (Expo env vars can't be baked in at build
time the same way). Next.js's `NEXT_PUBLIC_API_BASE_URL` is a build-time env var already used by
the existing `lib/api.ts`, so the web client can pass `baseUrl: API_BASE` directly to
`createClient` at construction time — no placeholder-origin rewrite middleware is needed. The
401-refresh-and-retry middleware itself (calling the existing `/api/refresh` BFF route, then
retrying once) is otherwise a direct port.

## R2 — Refresh token storage: keep the existing httpOnly-cookie BFF pattern

**Decision**: Keep `web/app/api/set-refresh-token`, `/api/refresh`, `/api/logout`,
`/api/clear-refresh-token` route handlers exactly as they exist today (Habits-template era, but
architecturally sound) — they already implement "refresh token in an httpOnly cookie, access
token in-memory," which is exactly this feature's FR-003 constraint.

**Rationale**: These routes are not Habits-specific; they're generic BFF (backend-for-frontend)
session plumbing that happens to have been built during the Habits-template phase. Rebuilding
them would be pure churn. The only change is what calls into them (`lib/auth.ts`, ported onto
the new `apiClient.ts`).

**Alternatives considered**: Move refresh-token handling client-side (`localStorage`) — rejected
outright, contradicts FR-003 and constitution Principle VI.

## R3 — Devices list: add the missing read endpoint rather than defer the screen

**Decision**: Add `GET /api/devices` (DirectorOnly, tenant-scoped) returning a summary DTO per
paired device (id, location, group, paired-by name, paired-at, revoked status). Backing query
(`ListDevicesQuery`) mirrors `ListStaffQuery`'s shape: query `DevicePairing`, batch-resolve
`Location`/`Group`/`TenantUser` names by id, assemble DTOs — same N+1-avoidance pattern already
used there.

**Rationale**: Feature 008a built `POST /api/devices/pair`, `POST /api/devices/{id}/revoke`, and
`POST /api/devices/exit-room-mode`, but no list — it had no UI consumer yet. This feature is that
consumer, and the Devices screen (User Story 3) cannot be built at all without a way to fetch the
paired-devices list. Deferring the whole screen would contradict the BACKLOG.md prompt's explicit
"Devices screen" requirement; adding one minimal, read-only, already-conventional endpoint is
lower-risk than dropping a spec'd user story.

**Alternatives considered**:
- Defer Devices screen to a future feature — rejected: the prompt and spec both call for it
  explicitly, and feature 008a's backend is otherwise complete for it; deferring would repeat the
  exact "referenced but never built" pattern BACKLOG.md's post-shipping note on 007a already
  calls out as a recurring mistake to avoid.
- Have the frontend reconstruct the list from `room-shifts/roster`-style per-device calls —
  rejected: no endpoint enumerates device ids either, and even if one existed, N+1 client-side
  calls is worse than one list query.

## R4 — Exposing organisation name and director name

**Decision**: Two additive changes:
1. Add `Name` to `AuthenticatedUser` (`AuthSessionResponse.cs`), populated from the existing
   `TenantUser.Name` column in all four auth handlers (`LoginCommandHandler`,
   `RefreshTokenCommandHandler`, `GoogleSignInCommandHandler`, `AppleSignInCommandHandler`).
2. Add `GET /api/organisations/me` (DirectorOnly, tenant-scoped) returning `{ Name }` from the
   current tenant, resolved via a new `GetCurrentOrganisationQuery` that reads `PublicDbContext`
   (the only place `Tenant.Name` lives) filtered by `ICurrentTenantService.TenantId`.

**Rationale**: FR-005 (sidebar shows organisation name + director name) has no existing data
source: `AuthenticatedUser` only carries `Id`/`Email`/`EmailVerified`/`Role`, and no endpoint
anywhere returns `Tenant.Name` (only `TenantSlug` is exposed, via JWT/`ICurrentTenantService`,
and never to the client). Both additions are read-only, additive (no field removed, no behavior
changed for existing callers), and follow existing patterns exactly.

**Why not `GET /api/staff/me` for the director's name instead of `AuthenticatedUser.Name`**:
`GET /api/staff/me` (feature 008) requires the director to have an attached Staff Profile, which
feature 005 made explicitly optional for directors (a director covering shifts). A director with
no Staff Profile would get a 404 from `/api/staff/me`, so it cannot be the sole source for a
name that must always render in the shell. `TenantUser.Name` already exists on every account
(collected at registration/invitation, feature 001/005) regardless of Staff Profile — it is the
correct universal source.

**Why a dedicated organisation-name endpoint instead of reusing `POST /api/organisations/register`**:
That endpoint is anonymous/tenant-exempt (a pre-session flow) and only handles registration; a
signed-in director's read of their own tenant's name needs a `DirectorOnly`, tenant-scoped `GET`,
which does not yet exist under `OrganisationEndpoints.cs`.

**Alternatives considered**:
- Embed the organisation name as a JWT claim, decoded client-side — rejected: the constitution's
  existing JWT claims are auth/tenant-resolution primitives (`tenant_id`, `sub`, `role`); adding
  display data to the token couples token size/content to UI concerns and would need
  re-issuance any time an org renamed. A cheap `GET` avoids both problems.
- Return organisation name from `/api/staff/me` instead — rejected for the reason above (not
  universally available for directors without a Staff Profile).

## R5 — Sidebar navigation shell structure

**Decision**: A left-hand collapsible sidebar (design-decisions.md's existing "director web uses
sidebar navigation" decision), built as a new `components/Sidebar.tsx`, replacing the inline
sidebar markup currently embedded in `app/(app)/layout.tsx`. Nav items: Staff (real), Devices
(real), Locations/Contracts/Children (placeholder, `disabled`, marked with a "soon" badge, per
spec Assumptions).

**Rationale**: Extracting a dedicated `Sidebar.tsx` (rather than leaving markup inline in
`layout.tsx`, as the Habits template did) makes it straightforward for the next feature to add a
real nav entry without touching layout/auth-guard logic. Collapsible per platform-rules.md's
"director web should feel like Linear/Notion/Airtable" reference, all three of which use a
collapsible sidebar.

**Alternatives considered**: Top nav bar — rejected, contradicts the already-recorded
design-decisions.md entry ("director web uses sidebar navigation"); revisiting that decision is
out of this feature's scope.

## R6 — Design token reuse: TypeScript port of `mobile/theme/colors.js`

**Decision**: Create `web/theme/colors.ts` as a TypeScript re-expression of the same token
values in `mobile/theme/colors.js` (not a shared cross-package import — `mobile/` and `web/` are
separate npm projects with no current monorepo tooling for cross-package source imports), and
wire it into `tailwind.config.ts` the same way `mobile/tailwind.config.js` consumes
`theme/colors.js` (kebab-case token names, `-dark` suffixed dark variants).

**Rationale**: design-decisions.md already flags this exact moment: "when `web/` (director)
exists, it needs its own consumer of the same token values... rather than a third hardcoded
copy." A duplicated-but-synchronized TS file is the pragmatic middle ground given there's no
existing shared-package/workspace tooling in this repo to justify introducing one just for a
color token object — that infrastructure decision is out of scope for a scaffold feature.

**Alternatives considered**:
- CSS custom properties shared via a static file served to both apps — rejected: no existing
  static-asset-sharing mechanism between `mobile/` and `web/`; would require new infrastructure
  disproportionate to a handful of color tokens.
- Set up an npm workspace to share `theme/colors` as a real package — rejected as out of scope:
  a build-tooling change of that size deserves its own decision, not a side effect of a UI
  scaffold feature. Noted as a natural follow-up if a third consumer ever needs these tokens.

## R7 — shadcn/ui component adoption scope

**Decision**: Install shadcn/ui's `table`, `button`, `input`, `dialog`, `badge`, and `dropdown-menu`
primitives (via its CLI, vendored into `web/components/ui/`) as the base for `StaffTable.tsx`,
`DevicesTable.tsx`, `ConfirmDialog.tsx`. Restyle their default Tailwind classes to consume
`theme/colors.ts` tokens (matching design-system.md's semantic-token-only rule) rather than
shadcn's default slate/zinc palette.

**Rationale**: The constitution pins `Tailwind, shadcn/ui` for web admin explicitly. shadcn's
copy-into-your-repo model (not an npm dependency) is exactly right for this project's
"reference products, don't reinvent, but don't add a black-box dependency" posture from
reference-products.md.

**Alternatives considered**: Hand-built table/dialog components — rejected, duplicates work
shadcn already solves well and the constitution already names it as the intended library.
