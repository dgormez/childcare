# Research: Closure Calendar

## R1. Lifecycle model

**Decision**: Use explicit `draft`, `published`, and `cancelled` states; only publish triggers parent notification and attendance closure generation.

**Rationale**: The backlog says notification occurs when a closure day is published, and directors need to edit planned holidays before sending parent communication. Draft state also makes retries and validation easier to test.

**Alternatives considered**: Create-as-publish was simpler but would notify parents during data entry. Hard delete for unpublished and soft delete for published was rejected as two lifecycles for one entity.

## R2. Cancellation model

**Decision**: Soft-cancel published closures with `cancelled_at`/`cancelled_by`; allow hard removal only for unpublished drafts.

**Rationale**: Published notifications need historical auditability and cancellation messaging. Drafts have no parent-facing consequence and can be removed without audit-heavy lifecycle.

**Alternatives considered**: Hard-delete everything and rely on audit logs; rejected because future invoicing and support investigation need a durable record of a published/cancelled closure.

## R3. Attendance integration

**Decision**: Publishing a closure creates/updates `AttendanceRecord` rows to `Status = Closure` for children enrolled at the location/date. Same-day existing present records require explicit confirmation and retain prior-state audit evidence.

**Rationale**: Feature 010 already blocks manual check-in against closure records and reserved bulk generation for feature 011. Confirmation protects against silent corruption when an extraordinary same-day closure collides with active attendance.

**Alternatives considered**: Only check a closure table at check-in time; rejected because feature 010 attendance views and future reporting expect actual `closure` records.

## R4. Affected-child source

**Decision**: Determine affected children from active contracts for the closure location/date, matching attendance planned-day logic and future invoicing semantics.

**Rationale**: Contracts are the existing source of enrollment and billable schedule. It deduplicates parent notifications around enrolled children rather than transient attendance only.

**Alternatives considered**: Current group assignment; rejected because group assignment does not establish billing/enrollment for a date. Present attendance only; rejected because future closures affect parents before children arrive.

## R5. Parent message storage

**Decision**: Add a minimal closure-specific one-way `ParentClosureMessage` store if no reusable parent-communication message store exists at implementation time.

**Rationale**: Feature 013 owns full messaging/conversations, but feature 011 explicitly requires an in-app message now. A closure-specific store satisfies the requirement without prematurely designing two-way messaging.

**Alternatives considered**: Push-only until feature 013; rejected by backlog. Building full messaging now; rejected as feature 013 scope.

## R6. Notification failure handling

**Decision**: Publishing remains successful when individual push sends fail; record per-recipient delivery outcomes and surface partial failure to the director.

**Rationale**: A closure day must exist even if a push token is invalid or Expo transport fails. This matches existing push alert precedent: external transport should not invalidate the domain event.

**Alternatives considered**: Transactionally fail publish on any push failure; rejected because it would leave attendance/billing inconsistent and make urgent closures fragile.

## R7. API shape

**Decision**: Use director-only REST endpoints under `/api/closures`, with list by `locationId` + `year`, create/update/publish/cancel operations, and a query endpoint for closure dates used by future invoicing.

**Rationale**: Existing backend uses Minimal API groups with MediatR commands. The location/year list is bounded and aligns with the director calendar screen.

**Alternatives considered**: Nesting under `/api/locations/{id}/closures`; rejected because existing endpoints mostly use flat groups with query parameters for director list screens.

## R8. Director web interaction

**Decision**: Build a dense year-calendar grid plus a side list/filter summary, reusing existing dialog/button/table primitives and semantic tokens.

**Rationale**: Director web is desktop-first and management-heavy; a full-year view is required while list details preserve scanability and keyboard navigation.

**Alternatives considered**: Month-only calendar; rejected because backlog requires full year. Decorative card layout; rejected by design-system density rules.
