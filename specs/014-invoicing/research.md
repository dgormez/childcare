# Research: Invoicing

## R1 — Invoice PDF is rendered on-demand, never stored

**Decision**: Invoice PDFs are generated fresh from the `Invoice`'s current `LineItems` on every
download request — there is no stored PDF blob in GCS or anywhere else.

**Rationale**: This exactly mirrors the existing `GenerateContractPdfQuery`/
`IContractPdfGenerator`/`QuestPdfContractGenerator` pattern (contract PDFs), which reads the
current `Contract` state and calls `pdfGenerator.GenerateAsync(...)` per request rather than
persisting a file. It also resolves spec.md's "the old PDF must be replaced" language for free:
since nothing is ever stored, "regenerating" an invoice (FR-011) simply means recomputing and
persisting new `LineItems`/`SubtotalCents`/`TotalCents` — the very next PDF download reflects
them automatically. No GCS signed-URL plumbing, no stale-file cleanup, no extra storage cost.

**Alternatives considered**: Storing a rendered PDF per invoice (or per regeneration) in GCS,
mirroring `IProfilePhotoStorage`/`IHealthAttachmentStorage`. Rejected — those exist because their
source content (a photo, an uploaded attachment) has no other representation to regenerate from.
An invoice's PDF is 100% derived from data already in the database; storing a redundant copy adds
storage cost and a cache-invalidation problem (exactly what "must be replaced" would otherwise
require) with no benefit.

## R2 — Billable-day computation reads `AttendanceRecord` alone, not `DayReservation` directly

**Decision**: The billable-day calculator queries `AttendanceRecord` only. A day counts as
billable when `Status == Present`, or `Status == Absent && AbsenceJustified == false`. A day
with `Status == Closure` (a distinct third enum value, not a flag alongside `Present`/`Absent`)
is always excluded, regardless of any other field. A day is also excluded when it falls outside
`[Contract.StartDate, Contract.EndDate ?? +∞]` intersected with the requested month.

**Rationale**: `AttendanceRecord.AbsenceJustified` is already the single source of truth for
justified/unjustified — `ApproveDayReservationCommand` (013a) writes to this exact field when a
director approves a day-reservation absence request, and `MarkAbsentCommand` (010) writes it
directly for an unplanned absence. There is no separate "billing-relevant" justification signal
living only in `DayReservation` — by the time a month is being invoiced, `AttendanceRecord` has
already absorbed whatever `DayReservation` approval happened. Similarly, closure days are
written as `Status == Closure` rows directly onto `AttendanceRecord` by feature 011's
`ClosureAttendanceService` — no separate `ClosureCalendarReader` query is needed at invoicing
time; excluding `Status == Closure` is sufficient and exhaustive.

**Alternatives considered**: Re-deriving closure/justification from `DayReservation` and
`ClosureCalendarReader` directly, as spec.md's Technical Requirements originally described (two
separate data sources). Superseded by this research — `AttendanceRecord` alone is both simpler
and more correct (it's the actual system of record these other tables feed into), so
`BillableDayCalculator` has exactly one dependency, not three.

**Open question carried into implementation**: a contracted day with *no* `AttendanceRecord` row
at all (nobody checked the child in or marked them absent) is neither `Present` nor
`Absent`-and-unjustified by this rule, so it is not billed. This matches the literal spec.md
FR-002 wording and is left as-is — flagging/backfilling missing attendance records is an
operational concern of feature 010, not this feature's job to second-guess.

## R3 — OGM structured reference: format, checksum, and uniqueness source

**Decision**: A Belgian OGM ("gestructureerde mededeling") reference is a 12-digit number,
displayed as `+++XXX/XXXX/XXXXX+++`. The first 10 digits are a base number; the last 2 digits are
`base mod 97`, except when that remainder is `0`, in which case the check digits are `97` (a
base number is never allowed to produce a `00` check).

The 10-digit base number comes from a new `Invoice.SequenceNumber` — a tenant-schema-scoped
`bigint` identity column (EF Core `.UseIdentityColumn()` / `ValueGeneratedOnAdd()`), zero-padded
to 10 digits. Schema-per-tenant isolation (constitution Principle I) means this sequence is
naturally scoped per organisation already — no cross-tenant coordination needed, and no
collision-retry logic is required since a database identity column is race-condition-free under
concurrent inserts by construction.

**Rationale**: A sequence-backed base number is the standard real-world mechanism for OGM
generation (accounting systems always use a monotonic counter, never a random/hashed value) and
sidesteps collision handling entirely. This is the first identity/sequence column in this
codebase (every other entity uses a GUID `Id`) — a deliberate, narrowly-scoped exception: `Id`
remains the GUID primary key for every relation/lookup as usual; `SequenceNumber` exists solely
to feed the OGM algorithm, is never used as a foreign key or exposed as "the" invoice identifier
anywhere else.

**Alternatives considered**: Deriving the base number from a hash of the `Invoice.Id` GUID
reduced mod 10^10. Rejected — needs an explicit uniqueness check + retry loop (a GUID-derived
hash can collide), where a native identity column guarantees uniqueness for free.

## R4 — "Overdue" is a computed view, not a stored/scheduled status

**Decision**: `Invoice.Status` only ever stores `draft`, `sent`, or `paid`. "Overdue" is computed
at query time as `Status == sent && DueDate < today` — never written to the database, never
flipped by a background job.

**Rationale**: No `IHostedService`/`BackgroundService`/cron infrastructure exists anywhere in
this codebase yet (confirmed by search). Introducing one for the sole purpose of flipping a
single status field on a schedule would be exactly the kind of premature infrastructure
constitution Principle VII (Monolith-First Simplicity) warns against. A computed predicate in the
list/detail queries (and equivalently in the web/parent-mobile display layer) is sufficient —
`sent`+overdue and `sent`+not-yet-overdue are the same underlying status, differing only in how
they're labeled and filtered, which a `WHERE`/`CASE` clause handles natively.

**Alternatives considered**: A daily background job that flips `sent` → `overdue`. Rejected per
the reasoning above — no infrastructure exists for it, and it would only ever produce a value
already fully derivable from `DueDate` and `Status`, i.e. a job that could go stale (running late,
failing silently) in a way a computed value structurally cannot.

## R5 — Extra-charge line items are appended, not templated

**Decision**: `LineItems.extra_charges` is a plain array of `{ label, amount_cents }`, entered
free-form by the director per invoice. No catalog/preset-charge entity is introduced.

**Rationale**: spec.md's Assumptions already settle this — matches the feature's own prompt
language exactly (`extra_charges (array: {label, amount_cents})`), and a preset-charge catalog is
speculative scope with no requirement calling for it yet (same reasoning as constitution
Principle I's provisioning carve-out: don't build a thing before a feature needs it).

## R6 — `Location`/`Tenant` field placement (KBO vs. erkenningsnummer/bank account)

**Decision**: Confirmed from spec.md's Clarifications — `Tenant.KboNumber` (public schema,
nullable) is org-wide; `Location.Erkenningsnummer`/`Location.BankAccountNumber` (nullable) are
per-location, alongside the existing `NaamLocatie`/`Dossiernummer`/`Verantwoordelijke` Opgroeien
fields already on `Location`.

**Rationale**: See spec.md's Clarifications session for the full reasoning — restated here since
research.md is where implementation reads for "what field goes where" during data-model.md/
migration authoring.
