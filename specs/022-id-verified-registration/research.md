# Research: ID-Verified Registration

## R1 — Attribution shape: no per-change history table exists anywhere in this codebase

**Decision**: Store two attribution pairs per verifiable entity (Child, Contact) —
`FirstIdVerifiedAt`/`FirstIdVerifiedByUserId`/`FirstIdVerifiedByEmail` (set once, never
overwritten) and `IdVerifiedAt`/`IdVerifiedByUserId`/`IdVerifiedByEmail` (current, overwritten on
every correction). No separate history/audit-log table.

**Rationale**: Searched the full `backend/ChildCare.Domain/Entities` tree for any existing
history/audit-log/version entity — none exists across 40+ shipped features. The closest analog,
feature 013h's vaccine-catalog deactivation, records a single `DeactivatedByUserId`/
`DeactivatedByEmail` pair directly on `VaccineType`, with **no DB-level FK** (the row's own doc
comment explains why: it's an attribution field, not a relationship the system needs to query
through). Feature 013b's incident reports — this codebase's other identity/compliance-adjacent
feature — use a hard 24-hour immutability lock instead of a changelog. Building a dedicated
history entity + list UI for this one feature (as originally drafted in spec.md's first
Clarifications pass) would be new architecture with no other user, for a field expected to be
corrected rarely (the only concretely anticipated case is a child aging into eID eligibility at
12). The two-pair approach mirrors the `CreatedAt`/`UpdatedAt` shape already used on every entity
in this codebase and gives genuine anti-backdating signal: if `FirstIdVerifiedAt` predates
`IdVerifiedAt`, a director can see the record was corrected, by whom, without a new pattern.

**Alternatives considered**: (a) a dedicated `IdentityVerificationHistory` table with one row per
change — rejected, no precedent, disproportionate to actual correction frequency. (b) storing only
current-state fields with silent overwrite (the plainest reading of "write-once via the UI" from
the original backlog note) — rejected, loses all anti-tampering signal, which is the entire reason
this field set exists per the backlog's own framing.

**Attribution denormalization**: mirrors 013h exactly — store `...ByEmail` as a plain string
alongside `...ByUserId`, no FK, no join needed to render "verified by X" in the UI.

## R2 — No organisation-owner role exists; access control stays Director-only

**Decision**: Creating/updating a verification, and creating/updating the NRN, requires the
`DirectorOnly` policy — same as the rest of `Child`/`Contact` write access. No new role or policy.

**Rationale**: `backend/ChildCare.Domain/Enums/UserRole.cs` defines exactly three roles —
`Director`, `Staff`, `Parent` — plus a cross-tenant `IsPlatformAdmin` flag (013h) that governs
platform-wide reference data, not per-organisation authority. Nothing distinguishes one director
account from another within a tenant. The backlog note's "editable only by org owner" has no
mechanism to implement without inventing new access-control machinery this codebase doesn't
otherwise have — and the same note's own edge case ("a child turns 12... **director** can update
`id_document_type`") already assumes plain director access. R1's attribution pair satisfies the
underlying anti-tampering intent without the new role.

## R3 — NRN encryption reuses `IPaymentTokenProtector`'s ASP.NET Core Data Protection pattern

**Decision**: New `INrnProtector`/`NrnProtector` port/adapter pair in
`ChildCare.Application.Common`/`ChildCare.Infrastructure`, structurally identical to
`IPaymentTokenProtector`/`PaymentTokenProtector` (feature 014a) — wraps `IDataProtectionProvider`
with a dedicated purpose string (`"Child.NationalRegisterNumber"`). `Child` gains
`EncryptedNrn` (string?, ciphertext) and `NrnLast4` (string?, plain digits — computed once at
write time from the validated input, never derived by decrypting `EncryptedNrn`).

**Rationale**: Data Protection is already wired into this app (014a's `AddDataProtectionKeys`
migration, `PublicDbContext`) — reusing it needs no new dependency, no new key-storage setup, and
matches this codebase's one existing precedent for "encrypt a sensitive string at rest" exactly.
Storing `NrnLast4` in plaintext (rather than decrypting on every read to slice the last 4 digits)
avoids adding a decrypt round-trip to every child-file view; the last 4 digits of a Belgian NRN
(the order-number tail + checksum) don't on their own reconstruct date of birth or the full
number, so plaintext storage of just that slice doesn't undermine the encryption requirement
(FR-011/FR-012).

**Alternatives considered**: decrypting on every read to compute the masked display — rejected,
adds unnecessary decrypt calls to a hot path (every child-file GET) for no benefit over
computing the slice once at write time.

## R4 — NRN format validation: structural check only, not the full modulo-97 checksum

**Decision**: `SetChildNrnCommandValidator` strips non-digit characters and requires exactly 11
digits remain. No day/month range check, no modulo-97 checksum against the national-register
algorithm.

**Rationale**: Matches spec.md's own Assumptions — this feature records what a director was
shown, not authoritative national-register validity; the field's actual regulatory consumer
(Belcotax Fiche 281.86 fiscal reporting) is an explicitly out-of-scope Phase 3 feature that can
add stricter validation when it exists. Over-validating now risks false rejections for edge-case
NRN formats (e.g., the "+40 month" convention used for some historical/administrative
registrations) this feature has no need to model correctly.

## R5 — Dashboard count and per-child badge: extend `GetDataCompletenessQuery`, but with
independent child-scoping

**Decision**: Add a fifth flag type, `missing_identity_verification`, to the existing
`DataCompletenessFlagType`/`GetDataCompletenessQuery`/`DataCompletenessSection` infrastructure
(feature 018) rather than building a new, separate "dashboard badge" component. The new flag's
candidate-children query is independent of the handler's existing `childIds` variable — it selects
all `Child` rows with `DeactivatedAt == null` (optionally intersected with the location filter via
`ChildGroupAssignment`, mirroring `ListChildrenQuery`'s location-scoping shape), not the
attendance-linked set the other four flags already share.

**Rationale**: `web/app/(app)/dashboard/page.tsx` already renders a `DataCompletenessSection`
whose whole purpose — a flat, click-through list of "director, this record needs attention" gaps
— is exactly what the backlog's "Niet-geverifieerde dossiers" badge asks for, and it already
exists (feature 018). Reusing it avoids inventing a second, parallel "outstanding work" widget on
the same screen. However, `GetDataCompletenessQueryHandler`'s existing `children`/`childIds`
variables are scoped to children with **at least one `AttendanceRecord`** at a scoped location —
appropriate for its current four checks (pickup contact, vaccine, staff qualification, staff PIN),
which only matter once a child has actually attended. Identity verification is different: a
brand-new enrolment that hasn't had its first day yet is exactly the case a director most needs
reminding about, and reusing the attendance-scoped set would silently exclude it, undercounting
against spec.md's own "actively enrolled children" definition (FR-007/FR-008, matching
`ListChildrenQuery`'s existing `DeactivatedAt == null` definition of "active"). So the new flag
computes its own children set rather than intersecting with the query's existing one.

**Per-child list badge (FR-007a)**: `ChildResponse` gains `IdVerifiedAt` (already needed for the
child-file display) — the existing `/api/children` list endpoint (`ListChildrenQuery`) already
returns full `ChildResponse` objects, so the `/children` table page needs no new endpoint, just a
new column reading a field that's already there.

**Alternatives considered**: a wholly separate `GET /api/reports/unverified-children` endpoint and
new dashboard widget — rejected as a duplicate of `DataCompletenessSection`'s existing pattern for
no added benefit.

## R6 — UI placement: one new component on the existing child-detail "Profiel" tab; contact
verification lives on the existing Contacts tab

**Decision**: New `ChildIdentityVerificationSection.tsx`, rendered inside the existing `profile`
`TabsContent` on `web/app/(app)/children/[id]/page.tsx`, below `ChildProfileTab` (mirrors how
`ChildMealPreferenceForm` already sits there as a second component in the same tab). No new tab.
Houses both the "Identiteit bevestigen" action/state and the NRN entry field, since both are
identity-record concerns the backlog groups together on the same child file.

For contacts: no dedicated contact-detail screen exists anywhere in this codebase (contacts are
only ever shown as rows inside `ChildContactsTab`, a child's "Contacts" tab). Rather than build a
new contact-detail route with no other precedent or need, verification is a per-row action within
the existing `ChildContactsTab` list — a small icon button opens a
`ContactIdentityVerificationDialog` (mirrors `LinkContactDialog`'s existing modal pattern) keyed
by `contactId`.

**Rationale**: Matches this codebase's established "extend the existing screen a feature needs,
don't build a parallel one" pattern (006a's shipped-note explicitly documents doing the same for
the Profiel tab itself). Building a full contact-detail page for this one action would be
disproportionate — the existing row-level action pattern (`LinkContactDialog`, the per-row
"set primary"/"remove" buttons already in `ChildContactsTab`) already solves "act on one contact"
without a new route.

## R7 — Migration: additive, nullable-only tenant-schema migration

**Decision**: One EF Core migration on the tenant schema (`children`, `contacts` tables) adding
all-nullable columns — no backfill needed since every existing row is legitimately unverified.
Per `CLAUDE.md`/Constitution VI, the generated SQL script is reviewed and run manually against
existing tenant schemas — not auto-applied.
