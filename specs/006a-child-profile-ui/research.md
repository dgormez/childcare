# Research: Child Profile UI

## R1: Scope against existing feature 006 backend

**Decision**: Reuse `CreateChildCommand`/`UpdateChildCommand`, `Child` entity, `ChildResponse`,
`ChildrenEndpoints.cs`, and the `DirectorOnly`/`DeviceOrStaffOrDirector` authorization groups
exactly as they exist. This feature's only backend change is adding `PediatricianName`/
`PediatricianPhone` to that existing surface — everything else (FirstName, LastName,
DateOfBirth, Gender, Nationality, AllergiesDescription, AllergySeverity, MedicalConditions,
DietaryRestrictions, **GpName, GpPhone** — already present end-to-end today), HealthInsuranceNumber,
Kindcode) already exists on `CreateChildRequest`/`UpdateChildRequest`
(`backend/ChildCare.Contracts/Requests/ChildRequests.cs:3-31`), their FluentValidation
validators (`CreateChildCommandValidator`/`UpdateChildCommandValidator`), `ChildResponse`, and
the EF Core mapping inline in `TenantDbContext.cs:285-311`. The web/mobile UI for all of it does
not exist yet — that gap is what this feature closes.

**Rationale**: The BACKLOG prompt reads as "build the general-details tab," which could be
misread as UI-only. Research confirmed the backend for every field except pediatrician contact
already ships and is exercised only via direct API calls, seed scripts, and the 012a
waiting-list conversion flow. Rebuilding any of it would duplicate working code.

**Alternatives considered**: A parallel "profile update" command distinct from
`UpdateChildCommand` — rejected; `UpdateChildCommand` already does a full-record replace with
the exact field set this feature's create/edit form needs, and introducing a second write path
for the same entity would violate Constitution III (CQRS via MediatR, one command per
write concern) without a reason.

## R2: Pediatrician field placement

**Decision**: Add `PediatricianName` (nullable `string`, max length 200) and
`PediatricianPhone` (nullable `string`, max length 30) to `Child`, `CreateChildCommand`,
`UpdateChildCommand`, `CreateChildRequest`, `UpdateChildRequest`, `ChildResponse`, their
FluentValidation validators, and the `TenantDbContext.cs` entity mapping — mirroring
`GpName`/`GpPhone`'s exact type, nullability, and max-length pattern at every one of those
touch points.

**Rationale**: `GpName`/`GpPhone` is the closest existing precedent for "an optional named
medical contact on `Child`" — matching its shape exactly keeps the two fields visually and
structurally parallel (spec FR-006/FR-007) and avoids inventing a new validation convention for
what is functionally the same kind of field.

**Alternatives considered**: A separate `ChildMedicalContact` child table (supporting an
arbitrary number of contacts) — rejected as over-engineering; the spec requires exactly one GP
and one pediatrician per child, no history, no additional contact types. A flat scalar pair on
`Child` is the minimum structure that satisfies the requirement.

## R3: `GetChildHealthSummaryQuery` is not the caregiver read path for GP/pediatrician

**Decision**: Do not touch `GetChildHealthSummaryQuery`/`ChildHealthSummaryResponse`
(`backend/ChildCare.Application/Children/GetChildHealthSummaryQuery.cs`,
`backend/ChildCare.Contracts/Responses/ChildHealthSummaryResponse.cs:3-8`) for this feature.

**Rationale**: Initial spec drafting assumed the caregiver mobile screen's GP/pediatrician
display would need the health-summary endpoint extended, matching 013c's "caregiver read-only
quick-access to the health/allergy summary" framing. Research corrected this: the caregiver
screen (`mobile/app/(app)/child/[id].tsx:45`) reads GP-adjacent fields (allergies, medical
conditions, dietary restrictions) directly from the cached `ChildResponse` list
(`getCached<ChildResponse[]>(CHILDREN_CACHE_KEY)`, populated by the existing group/list fetch),
not from `getChildHealthSummary()`. `ChildResponse` already carries `GpName`/`GpPhone` and, once
this feature ships, `PediatricianName`/`PediatricianPhone` — no health-summary contract change
is needed to surface them. `getChildHealthSummary()`
(`mobile/services/healthSummary.ts`) remains scoped to vaccines/health-records only, unchanged.

**Alternatives considered**: Extending `ChildHealthSummaryResponse` with GP/pediatrician fields
for symmetry with 013c's naming — rejected; it would duplicate data already present on the
cached `ChildResponse` object the screen already holds, adding a second source of truth for the
same two fields with no behavioral benefit.

## R4: Mobile offline behavior is already covered by the existing children-list cache

**Decision**: No new offline-cache-fallback service or cache key is introduced for GP/
pediatrician display. The existing `CHILDREN_CACHE_KEY` cache (mobile 008a/009c-era
infrastructure, `services/readCache.ts`) already caches the full `ChildResponse` list the
caregiver screen reads from; once `ChildResponse`'s TypeScript type
(`mobile/types.ts`, regenerated from the backend OpenAPI contract) includes the two new fields,
they flow through that existing cache automatically.

**Rationale**: Spec FR-012 requires offline fallback for the caregiver summary. Feature 013c's
shipped-notes describe a *different* gap — `healthSummary.ts`'s own cache-fallback path lacked a
test — which does not apply here since GP/pediatrician never goes through that service (R3).
Testing this feature's offline behavior means asserting the *existing* `CHILDREN_CACHE_KEY`
cache-read path renders GP/pediatrician correctly when offline, not building new
cache-fallback logic.

**Alternatives considered**: None — this is a correction of an assumption made before this
research, not a design choice between alternatives.

## R5: Web create/edit UI pattern

**Decision**: Use a Radix `Dialog` modal (`web/components/ui/dialog.tsx`) for child creation,
mirroring `web/components/InviteParentDialog.tsx`'s structure (plain `useState` form state, no
react-hook-form — confirmed zero usage of react-hook-form anywhere in `web/`;
`apiClient.POST`/`apiClient.PUT` from the generated openapi-fetch client; `errorKey` from
`ApiErrorBody` mapped to a translated message). Editing happens inline on the "Profiel" tab via
the same form component reused in an "edit" mode, not a second modal.

**Rationale**: `web/` has zero existing full-page create/edit routes (no `/staff/new`,
`/locations/new`, etc.) — every write flow to date (`InviteParentDialog`, the waiting-list
"create new child" dialog, PIN reset) is a modal over an existing list/detail screen. Matching
that precedent keeps this feature's UI pattern consistent with the rest of `web/` rather than
introducing a new navigation paradigm for one screen.

**Alternatives considered**: A dedicated `/children/new` route — rejected for lack of precedent
and no requirement (spec's UX Requirements do not call for a distinct route); a react-hook-form-based
form — rejected since it would be the first use of that library in `web/`, adding a new
dependency for a ~12-field form that plain `useState` handles adequately at this codebase's
current form complexity (consistent with every prior web form in this repo).

## R6: Tab component is new shared UI

**Decision**: Add a shadcn/ui `Tabs` primitive (`web/components/ui/tabs.tsx`, Radix
`@radix-ui/react-tabs`) as a new shared component, since none exists yet
(`web/components/ui/` currently has no `tabs.tsx` — confirmed by directory listing). Use it to
present "Profiel" and the existing 013c health content as two tabs on
`web/app/(app)/children/[id]/page.tsx`, replacing that page's current single-section layout
without altering the Gezondheid tab's existing behavior.

**Rationale**: Feature 013c's own doc comment on this file explicitly flags it as "no other tab
(profile, contracts, contacts) exists yet; a future feature building the full child file adds
them alongside this one" — this is that feature. A shadcn primitive matches the existing
component-sourcing convention (`badge.tsx`, `button.tsx`, `dialog.tsx`, `input.tsx`, `table.tsx`,
`textarea.tsx` are all shadcn-sourced) rather than a bespoke tab implementation.

**Alternatives considered**: Two separate routes (`/children/[id]/profile`,
`/children/[id]/health`) with router-driven navigation — rejected; a same-page tab switch avoids
a full page reload (spec SC-005) and matches the "Profiel"/"Gezondheid" framing as views of one
record, not two separate pages.

## R7: EF Core migration

**Decision**: Add one EF Core migration (name pattern `AddPediatricianContactToChild`, timestamp
after the latest existing migration `20260712203216_AddVaccineAndHealthRecords`) adding
`PediatricianName varchar(200) NULL` and `PediatricianPhone varchar(30) NULL` to the tenant
`children` table. Generate the SQL script (`dotnet ef migrations script`) for manual review/run
against each tenant schema per this repo's CLAUDE.md convention — no auto-apply.

**Rationale**: Matches every prior additive, nullable-column migration in this codebase (e.g.
013c's own vaccine/health-records migration). No backfill is needed since both new columns are
nullable with no default-value requirement.

**Alternatives considered**: None — this is the established, only pattern for schema changes in
this codebase.

## R8: OpenAPI client regeneration

**Decision**: After the backend contract change ships, regenerate `web/lib/generated/api-types.ts`
(`npm run generate-api-client` in `web/`, hits the running API's `/openapi/v1.json`) and the
equivalent mobile generated types, so `PediatricianName`/`PediatricianPhone` appear in both
clients' generated `ChildResponse`/`CreateChildRequest`/`UpdateChildRequest` types.

**Rationale**: `GpName`/`GpPhone` are already present in the generated types today
(`web/lib/generated/api-types.ts:5198-5199,5511-5512`), confirming this is a mechanical,
already-established step, not a new integration point.

**Alternatives considered**: None.
