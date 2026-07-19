# Research: Family Siblings

## Context

An audit of the existing codebase (see spec.md Assumptions) found that most of the "multiple
children, one parent" data model and read-side UI already exists from features 006/013/013a:
`Contact`/`ChildContact` (many-to-many, with `IsPrimary` enforcement), `GetParentChildrenQuery`,
and every parent-mobile screen except day reservations already iterate all linked children. This
research focuses only on the genuine gaps identified during specification.

## R1: Bulk day-reservation submission design

**Decision**: A new `SubmitBulkDayReservationCommand(TenantUserId, ChildIds, Type,
RequestedDate, ExchangeForDate, Reason)` that, per child, dispatches the existing
`SubmitDayReservationCommand` via `IMediator.Send` and aggregates each child's individual
success/failure into one bulk result (`IReadOnlyList<(ChildId, DayReservationResult)>`).

**Rationale**: `SubmitDayReservationCommand` (`ChildCare.Application/DayReservations/
SubmitDayReservationCommand.cs`) already encodes all per-child validation (contract link check,
013f policy resolution, notice-hours, closure-day, exchange-day-of-week) and already calls
`IMediator.Send` internally for its own auto-approval path (`MarkAbsentCommand`) — reusing it via
mediator dispatch per child is the established pattern in this codebase (mirrors
`GenerateInvoicesCommand`'s per-contract loop) and guarantees the bulk path can never drift from
single-child validation rules, satisfying spec FR-002/FR-003 (partial success, one child's
blocked rule doesn't block siblings) for free.

**Alternatives considered**: Duplicating the validation logic inline in a bulk handler — rejected,
duplicates a already-tested, non-trivial rule set (013a/013f) for no benefit and risks drift.

## R2: Sibling-discount and family-bundling eligibility computation point

**Decision**: Both the discount and the bundling grouping are computed once, at invoice
*generation* time (inside `GenerateInvoicesCommand`), and their effects (a discount line item,
a shared grouping key) are persisted onto the `Invoice` row — not recomputed at read/display
time.

**Rationale**: `Invoice.LineItems` is already an immutable-once-generated JSON snapshot
(`InvoiceLineItems`, regenerated only via the explicit `RegenerateInvoiceCommand` on a still-Sent
invoice) — the codebase's established invariant is "an invoice reflects the state of the world
at generation time, not a live join." Computing sibling eligibility live at PDF/read time would
mean an invoice's discount could silently change after being sent (e.g. a sibling's contract
ends) — an accounting integrity problem this codebase avoids everywhere else (`DueDate`,
`OgmReference` are the same "assigned once" pattern).

## R3: Sibling-discount eligibility & tie-breaker

**Decision**: For a location with a configured `SiblingDiscountPct`, when generating invoices for
a given `(LocationId, PeriodMonth)`, group the location's contracts-being-invoiced by their
child's **primary** `ChildContact` (`IsPrimary = true`). Within any group of 2+, the child whose
contract has the earliest `StartDate` at that location is full price; every other child in the
group gets a discount line item of `-(SiblingDiscountPct% × SubtotalCents)`.

**Rationale**: Matches spec.md Clarifications (bundle/discount by primary contact, not any shared
contact) and the spec's explicit tie-breaker assumption. Grouping by primary contact reuses the
single already-authoritative "who is billed for this child" field rather than a new resolution
rule.

**Alternatives considered**: Discounting the youngest/oldest child by birthdate — rejected, no
such rule exists in the backlog or spec; contract start date is the natural billing-relevant
ordering and matches common real-world sibling-discount policy (longest-enrolled is full price).

## R4: Family invoice grouping data shape

**Decision**: Add a nullable `FamilyGroupId` (`Guid?`) to `Invoice`. When bundling is enabled for
a location, every invoice generated for the same `(LocationId, PeriodMonth, primary ContactId)`
group in one `GenerateInvoicesCommand` run receives the same new `Guid` (generated once per
group per run, reused across a regenerate for the same still-Draft/Sent group so the group
identity is stable). When bundling is disabled (default), `FamilyGroupId` stays null — zero
behavior change for every existing/non-bundling location, satisfying spec.md SC-005.

**Rationale**: Extends the existing per-child `Invoice` row rather than replacing it (per
Clarifications) — `ChildId`/`ContractId` remain the FK 018's reporting and every existing invoice
query already key off. `FamilyGroupId` is purely an additive grouping key, following the same
"new nullable column, default null, zero behavior change until opted in" shape as 014a's
`ReminderCount`/`Location.PaymentRemindersEnabled`.

**Alternatives considered**: A new `FamilyInvoice` parent entity with child `Invoice` rows FK'd
to it — rejected as unnecessary schema churn (a new table, a new relationship, migration risk to
018's existing per-`Invoice` queries) for what a single grouping GUID accomplishes; revisit only
if a future feature needs richer family-invoice-level state (e.g. its own status) that a shared
GUID can't hold.

## R5: Combined family invoice PDF & payment

**Decision**: A new `GenerateFamilyInvoicePdfQuery(FamilyGroupId, Locale)` that loads every
`Invoice` sharing that `FamilyGroupId`, and a new `QuestPdfFamilyInvoiceGenerator` that renders
one document with a per-child section (reusing each invoice's existing `InvoiceLineItems`) and
one combined total — mirrors `QuestPdfInvoiceGenerator`'s existing structure/locale-label
pattern, extended to loop children instead of assuming one. `MarkInvoicePaidCommand` gains: when
the target invoice has a `FamilyGroupId`, look up every sibling invoice sharing it and transition
all of them `Sent → Paid` together in the same transaction (per Clarifications' "one payment
action covers the whole bundle"). The 014a PSP payment-link path (`CreatePaymentLinkCommand`)
follows the same rule: a payment link created for a grouped invoice covers the group's combined
`TotalCents`, and the webhook's existing paid-transition is the same cascading path
`MarkInvoicePaidCommand` uses (no separate cascade logic to maintain).

**Rationale**: Reuses 014's PDF generator pattern and 014a's existing paid-transition/webhook
plumbing rather than inventing a parallel payment concept for bundled invoices.

## R6: `ContactRelationship` enum extension

**Decision**: Add `FosterParent` and `Other` to `ChildCare.Domain.Enums.ContactRelationship`
(currently `Mother, Father, Guardian, EmergencyContact, AuthorisedPickup`), appended at the end.

**Rationale**: EF Core maps this enum to a plain integer column (no Postgres native enum type or
explicit `HasConversion` found in `TenantDbContext`) — appending new values is purely additive,
requires no data migration, and does not renumber existing stored values.

## R7: Duplicate-contact detection for the web Contacts UI

**Decision**: No backend change. `ListContactsQuery` (`ChildCare.Application/Contacts/
ListContactsQuery.cs`) already returns every tenant contact — its own doc comment already
anticipates this use ("lets a director search and reuse an existing contact when linking a
sibling"). The new web "add contact" flow fetches this list once and filters client-side for a
case-insensitive email or phone match as the director types, surfacing a "link existing contact
instead?" suggestion (spec FR-013).

**Rationale**: A KDV's total contact count (tens to low hundreds) makes a full client-side
fetch-and-filter reasonable — matches the existing pattern of small, un-paginated director-web
list endpoints (e.g. `ListContactsQuery` itself has no pagination). Avoids adding a new
search/dedup endpoint for a problem the existing endpoint already solves.

## R8: "Previous children" (deactivated siblings) view

**Decision**: A new `GetParentPreviousChildrenQuery`, mirroring `GetParentChildrenQuery` but
filtering `Child.DeactivatedAt != null` instead of `== null`, returning the same
`ParentChildResponse` shape plus an enrollment-period field. No changes needed to reach a
deactivated child's historical data: `GetParentInvoicesQuery` already has no active/deactivated
filter (only a `ChildContact` link check), and `GetParentDailySummaryQuery` is already
date-parameterized with the same link-only authorization check — both already work for a
deactivated child once the parent-mobile UI has a way to navigate to that child's id.

**Rationale**: Reuses the exact same authorization pattern (`ICurrentParentContactResolver` +
`ChildContact` link check) as every other parent query in this codebase, and confirms spec.md
FR-016 ("historical daily reports and invoices... read-only") requires no new backend read paths
— only a new entry point (the previous-children list) and marking the existing screens
read-only/hiding action buttons when the child is deactivated.
