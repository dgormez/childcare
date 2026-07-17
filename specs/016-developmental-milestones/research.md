# Phase 0 Research: Developmental Milestones

## R1 — Where does the reference catalog live: public schema or per-tenant?

**Decision**: `developmental_domains` and `developmental_milestones` are **public-schema, shared,
platform-wide reference tables** — not duplicated per tenant.

**Rationale**: BACKLOG.md's original prompt block said "seeded, tenant-agnostic but stored in
tenant schema." Investigating `TenantProvisioningService.ProvisionAsync` (the only place a new
tenant schema is populated) shows it creates the schema, replays baseline migrations, and upserts
the director user — it seeds **no** reference data of any kind. No mechanism exists anywhere in
this codebase for seeding translated reference rows into each newly-provisioned tenant schema.
The closest, already-shipped precedent for "admin-maintained, tenant-agnostic reference data
referenced by tenant-scoped records" is `VaccineType` (013g) plus its platform-admin management
surface (013h): one public-schema table, seeded once via `migrationBuilder.InsertData` in the
migration itself (`AddVaccineTypeCatalog.cs`), read by every tenant through `IPublicDbContext`,
referenced by tenant-scoped `VaccineRecord.VaccineTypeId` with **no** DB-level foreign key (since
PostgreSQL cannot FK across schema boundaries). Following this precedent for
`DevelopmentalDomain`/`DevelopmentalMilestone` avoids inventing a new, redundant per-tenant seeding
mechanism solely for this feature, and avoids identical Belgian-framework content silently
drifting across tenant schemas over time.

**Alternatives considered**:
- *Seed into every tenant schema on provisioning*: rejected — would require building a new
  seeding mechanism from scratch, duplicating identical content per tenant for no benefit, and
  handling drift when the seed data is later corrected (a translation fix would need replaying
  across every tenant schema, whereas a public-schema table only needs updating once).
- *A tenant-schema table that references a public-schema table via application-level lookup
  only*: this is exactly what was chosen — described above, not a separate alternative.

## R2 — Age-band resolution: computed live or cached?

**Decision**: The age-appropriate band is computed at read time from the child's current date of
birth (already stored on `Child`), never cached or stored against a point-in-time age.

**Rationale**: Spec FR-004 requires this explicitly — a child's age crosses into the next band
between visits, and the portfolio view must reflect the present, not a stale snapshot. This is a
pure function (`ageInMonths` → milestones where `age_from_months <= ageInMonths <= age_to_months`,
inclusive both ends per spec.md's Assumptions), cheap enough to compute on every portfolio read
with no caching needed. Shared in `MilestonePortfolioBuilder` so the director query, parent query,
and PDF export all resolve age-appropriateness identically — avoiding the three call sites
drifting into subtly different age-math.

**Alternatives considered**: Storing a computed "current band" on the child record, refreshed by a
background job — rejected as needless complexity (Constitution VII) for a value that's trivial to
compute per request and must always reflect "now," not a job's last run.

## R3 — Observation immutability: soft-constraint or structural?

**Decision**: No update or delete MediatR command/endpoint exists for
`child_milestone_observations` at all — not a policy check on an existing mutation path, but the
complete absence of one.

**Rationale**: Spec FR-003 requires immutability to preserve historical progression (a regression
must be a new row, never an edit). `VaccineRecord` (013c) has an *editable* record with an update/
delete path guarded by ordinary authorization; this feature is structurally different — the
absence of any mutation endpoint is a stronger guarantee than a policy check that could be
misconfigured or bypassed. This mirrors how `child_events` treats each event as create-then-append
(no update path either), the closest existing precedent for "record what happened, don't rewrite
history."

## R4 — PDF export: stored (like fiscal attestations, 015) or on-demand (like invoices, 014)?

**Decision**: On-demand, unstored — mirrors `IInvoicePdfGenerator`'s exact shape (`GenerateAsync`
returns `byte[]`, streamed directly in the HTTP response, nothing written to GCS).

**Rationale**: A milestone portfolio is a continuously growing, live view of ongoing observations
— fundamentally different from a fiscal attestation (015), which is a point-in-time legal document
that must remain a stable, unchanging snapshot once filed with a tax authority. Storing a
milestone-portfolio PDF would immediately go stale the next time an observation is recorded, with
no compensating benefit (no legal/audit requirement to retain a specific historical rendering).
Rendering fresh each time, exactly like invoice PDFs already do, keeps the exported document
always accurate and avoids introducing a new GCS storage port for this feature.

## R5 — Should recording (or regenerating) an observation trigger a parent notification?

**Decision**: No new notification type. Parents access the portfolio by opening the app
(pull-based), the same way they already check daily reports — no push/in-app notification fires
per observation.

**Rationale**: `InvoiceNotificationService`/`FiscalAttestationNotificationService`'s "a new
document is ready" pattern fits infrequent, discrete events (one invoice a month, one attestation
a year). Milestone observations are recorded far more frequently during ordinary daily care and
would produce notification fatigue if each one pushed a parent alert — inconsistent with
`design-system.md`'s "calm, low cognitive load" product principle. This matches how `child_events`
themselves are never individually push-notified either; parents check the timeline/daily report at
their own pace. If a future feature wants a periodic ("your child hit 3 new milestones this
month") digest, that is a distinct, deliberately-batched notification design, out of scope here.

## R6 — Caregiver-tablet authorization for recording

**Decision**: `RecordMilestoneObservationCommand`'s endpoint uses the `DeviceOrStaffOrDirector`
policy, identical to `ChildEventEndpoints`' recording route.

**Rationale**: A milestone observation is exactly the same class of daily-care action as a child
event (recorded by whichever caregiver is on shift, via the kiosk device-token model) — no reason
to diverge from the already-established policy for this action shape.

## R7 — Seed content: standard Belgian developmental framework

**Decision**: Seed data is authored directly in the Phase-1 migration (mirrors
`AddVaccineTypeCatalog.cs`'s `migrationBuilder.InsertData` approach), covering the 7 domains named
in BACKLOG.md (`motor_gross`, `motor_fine`, `language`, `cognitive`, `social`, `emotional`,
`self_care`) with milestones spanning the 0–36 month daycare age range, drawn from the standard
Flemish/Belgian early-childhood developmental checkpoints (the same "groeiboekje"-style age-banded
milestones Kind & Gezin/Opgroeien's own consultation-schedule materials use as a general
reference, e.g. "rolls independently," "responds to own name," "says first words," "points to
indicate wants," "plays alongside other children," "uses a spoon independently"). This is
content, not a scope decision — exact wording is finalized during implementation and does not
block this plan. Descriptions are authored in NL first (the primary domain-content language),
then FR/EN, matching `VaccineType`'s own single-language-first precedent (that catalog's `Name`
is NL-only today; this feature's explicit NL/FR/EN requirement is a step beyond that, per spec
FR-001/FR-009).
