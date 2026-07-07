# Phase 0 Research: Child File Management

Each decision below resolves a technical unknown from the Technical Context. Format: Decision / Rationale / Alternatives considered.

## R1. `IProfilePhotoStorage` is generalized from a staff-only path convention to `(category, subjectId)`

**Decision**: Feature 005 shipped `IProfilePhotoStorage.CreateUploadUrlAsync(Guid staffProfileId, ...)`, with `GcsProfilePhotoStorage` hardcoding the object path as `staff/{staffProfileId}/photo.jpg`. This feature changes the signature to `CreateUploadUrlAsync(string category, Guid subjectId, ...)`, producing `{category}/{subjectId}/photo.jpg` (e.g. `children/{childId}/photo.jpg`). Every existing feature-005 call site (`RequestPhotoUploadUrlCommandHandler`, `ListStaffQuery`, `GetStaffByIdQuery`, and their tests' `FakeProfilePhotoStorage`) is updated to pass `"staff"` explicitly, so no behavior changes for staff photos — only the path literal moves from hardcoded-in-the-implementation to caller-supplied.

**Rationale**: Feature 005's own shipped notes flagged this port as reusable by feature 006 rather than rebuilding it — but its concrete implementation baked in a staff-specific path. Generalizing now (rather than introducing a second, parallel `IChildPhotoStorage` port) keeps exactly one signed-URL mechanism in the codebase, matching constitution Principle VII's monolith-first / no-unnecessary-abstraction stance. Modifying an already-shipped internal port is a normal part of iterating a small in-house system — this is not a public API with external consumers.

**Alternatives considered**: A second `IChildPhotoStorage` interface duplicating `IProfilePhotoStorage`'s shape with a hardcoded `children/` prefix — rejected as needless duplication for what is structurally identical behavior (sign a URL for a deterministic per-subject object path); a category string is the minimal change that keeps one implementation.

## R2. `Group` is a new, minimal entity — not extended onto `Location`

**Decision**: `Group` is a new entity (`Id`, `LocationId` FK, `Name`) in `ChildCare.Domain/Entities/Group.cs`. It is not a field/column added to `Location` (feature 004), since a location can have multiple groups (spec.md's "a child moves groups as they grow" implies more than one group per location) and groups need their own identity for `ChildGroupAssignment` to reference.

**Rationale**: Feature 004 explicitly deferred "Group/section management within a location" to a later feature (BACKLOG.md's 004 Out of Scope). This is that later point of first need — spec.md's Assumptions already documents that full group administration (capacity, BKR configuration) is out of scope here; only what's needed to name a group and assign children to it.

**Alternatives considered**: Storing a free-text "group name" directly on `ChildGroupAssignment` with no separate `Group` entity — rejected because it would prevent two children being reported as "in the same group" reliably (free text invites typos/inconsistency) and gives feature 009/011/016 (which all reference "group" concepts per BACKLOG.md) nothing stable to join against.

## R3. `ChildContact` identity is `(ChildId, ContactId)` — one relationship per pair (revised during `/speckit-analyze`)

**Decision**: The `ChildContact` join entity's composite PK is `(ChildId, ContactId)` — a contact has exactly one `Relationship` value for a given child, mutable via update, not a second row. This reverses an earlier draft of this decision (originally `(ChildId, ContactId, Relationship)` with a surrogate `Id`, allowing two rows for the same pair with different relationships).

**Rationale**: The original surrogate-keyed design made `PUT`/`DELETE /api/children/{childId}/contacts/{contactId}` ambiguous — if the same `(childId, contactId)` pair could have two rows (one per relationship), neither route could identify which row to act on without also routing on relationship or the row's own id, adding real plumbing complexity (every caller needing to track a link-id, not just a contact-id) for a rare scenario. `CanPickup` is already an orthogonal boolean on the single row, and in practice "guardian" already implies the broadest authority a family contact can have, so the "same person needs two simultaneous relationship rows" case this was meant to support isn't load-bearing enough to justify the added routing complexity. Collapsing to one relationship per pair (mutable via `PUT`) removes the ambiguity entirely, consistent with constitution Principle VII's simplicity-first stance.

**Alternatives considered**: Keep the surrogate-keyed, multi-relationship-per-pair design and fix the routes to key on `ChildContact.Id` instead of `contactId` — rejected as solving a problem (two relationship rows for the same pair) that wasn't clearly needed in the first place, in exchange for every client having to track a third id (link id, distinct from contact id and child id) instead of just the two ids already natural to the domain.

## R4. Deactivation guard extension point mirrors features 004/005 exactly

**Decision**: `IChildDeactivationGuard.HasActiveDependentsAsync(Guid childId, ITenantDbContext db, CancellationToken ct)`, resolved as `IEnumerable<IChildDeactivationGuard>` by `DeactivateChildCommandHandler`. Zero implementations registered by this feature — feature 007 (contracts) is expected to register its own once a child can have an active contract.

**Rationale**: Directly reuses the pattern features 004 (`ILocationDeactivationGuard`) and 005 (`IStaffDeactivationGuard`) already established, for the same structural reason — a dependent that doesn't exist yet shouldn't block a feature that ships first.

**Alternatives considered**: None seriously considered beyond the established pattern.

## R5. Allergy severity is a fixed three-value enum, not free text

**Decision**: `AllergySeverity` (`Mild`, `Moderate`, `Severe`) stored alongside the free-text allergy description, consistent with spec.md's Assumptions ("a small fixed set... consistent with standard medical-record practice").

**Rationale**: A fixed severity enum is what feature 009 (attendance)/012 (parent communication) would eventually want to filter/highlight on ("show severe allergies prominently") — free text alone can't support that without fragile string matching.

**Alternatives considered**: Free-text severity — rejected, no other part of the system could reliably act on it (e.g., surfacing "severe" allergies more prominently in the caregiver quick-access view, FR-004).

## R6. Contacts are read/listed tenant-wide for linking, not scoped per-child in the list query

**Decision**: `ListContactsQuery` returns every contact in the tenant schema (no child filter), so a director adding a sibling's shared parent can search the existing contact list rather than only ever creating a new one. `LinkContactToChildCommand` then creates the `ChildContact` row referencing an existing `ContactId`.

**Rationale**: This is the mechanism that satisfies FR-006/SC-004 (no duplicated contact records for siblings) — a director must be able to find and reuse an existing contact rather than only being able to create a fresh one per child.

**Alternatives considered**: Auto-detecting duplicate contacts by matching name/phone/email on creation — rejected as unreliable (typos, remarried names, shared landlines) and more complex than giving the director an explicit "link existing contact" action.
