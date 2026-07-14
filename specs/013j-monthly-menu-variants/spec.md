# Feature Specification: Monthly Menu Variants

**Feature Branch**: `013j-monthly-menu-variants`

**Created**: 2026-07-14

**Status**: Draft

**Input**: User description: "Let a director optionally publish an alternative monthly menu per
dietary restriction (vegetarian, halal, vegan, kosher, gluten-free — the existing 013d
`DietaryType` enum), separate from the location's base menu, so a child with a matching dietary
preference sees the right variant instead of the base menu."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web and parent
mobile).

### Primary Consumer

Director (configures which dietary variants are enabled for a location and their priority order;
authors and publishes each variant's menu). Parent (sees the correct variant automatically,
resolved per child — no parent-facing choice or configuration).

### Workflow Boundary

Same two workflows 013e already occupies — no new workflow is introduced.

- **Daily Child Care** (`Workflows/dailycare.md` — Meals): variant authoring extends 013e's
  existing monthly-menu authoring ground.
- **Parent Communication** (`Workflows/communication.md`): the parent-facing resolved menu is the
  same category of parent-facing update 013e's Menu tab already is.

Actors: Director (configures `Location.MenuVariantPriorityOrder`; authors/publishes/unpublishes
each variant's `MonthlyMenu`, identical mechanics to the base menu). Parent (views their
resolved menu per child — no direct interaction with variants).

Actions: Director enables a set of `DietaryType` variants for a location and orders them by
priority. Director selects a variant (or the base menu) in the existing Menu section and authors
it exactly as today. System resolves, per child, which published menu (variant or base) to show
a parent.

Data Flow: Director-web variant selection → same `MonthlyMenuDayGrid`/CSV-import (013i) authoring
flow, now parameterized by an optional `DietaryType` → existing whole-month write path, now keyed
by `(LocationId, Year, Month, Variant)` instead of `(LocationId, Year, Month)`. Parent-facing read
→ per child, per location: read the child's `MealPreference.DietaryType` list → walk the
location's `MenuVariantPriorityOrder` → resolve to the first variant that is both a match and
published → fall back to the base (`Variant == null`) menu.

Outputs: A director-authored variant menu, selectable and independently publishable like the
base menu. A parent sees one resolved menu per child (not per location), reflecting that child's
dietary preference automatically.

Cross-Platform Impact: Director web (settings + authoring) and parent mobile (resolved read).
No caregiver-tablet impact — caregivers do not interact with the menu feature at all (013e).

### User Impact

This enables a director to serve a dietary-appropriate menu automatically to every parent whose
child needs one, without a parent ever having to ask or select anything, resulting in fewer
manual accommodations and clearer communication for KDVs serving families with dietary
restrictions.

### UX Requirements

**Persona**: Director (desktop web, per `platform-rules.md`'s Director Web section) for
authoring/configuration. Parent (mobile, per `platform-rules.md`'s Parent Mobile App section) for
the resolved read — warm, reassuring, no raw enum names.

**Platform**: Web (director) and parent-mobile (parent). No caregiver-tablet surface.

**User job (director)**: "Publish a separate menu for my vegetarian/halal/vegan/kosher/
gluten-free children, without duplicating effort for children who don't need it."

**User job (parent)**: "See exactly what my child will eat, correctly reflecting their dietary
needs, without having to ask or configure anything."

**Success criteria**:

- A director can enable a variant, author it, and publish it using the exact same interaction
  pattern as the base menu — no new authoring paradigm to learn.
- A parent with a child who has a matching dietary preference sees that variant automatically,
  with zero setup on their end.
- A location that never configures any variant behaves identically to before this feature
  shipped — zero behavior change for the common case.

**Main flow (director)**: Director opens the location's settings, enables one or more
`DietaryType` variants, and orders them by priority → returns to the Menu section, selects a
variant from a selector alongside the existing location/month pickers → the familiar day-grid
(and CSV import) loads for that variant → director fills it in and publishes, identical to the
base menu flow.

**Main flow (parent)**: Parent opens the Menu tab → sees one menu section per child (when they
have more than one child, rather than one section per location as today) → each section shows
whichever menu resolves for that specific child, labeled in plain language (e.g. "Vegetarisch
menu voor Emma"), with no indication a resolution process happened.

**Loading/empty/error states**: Same as 013e's existing Menu section and Menu tab — a variant
with no published menu for the month behaves exactly like an unpublished base menu did before
(falls through to the next option in the resolution order).

**Accessibility**: The director-web variant selector and priority-order control follow the same
keyboard-operable, focus-visible patterns as every other director-web control in this codebase
(e.g. 013f's `ReservationSettingsForm`). No new accessibility pattern is introduced.

**Offline behavior**: Unchanged from 013e — parent-mobile has no persistent offline store; the
existing in-memory fetch-then-cache-fallback behavior (`parent-mobile/services/menu.ts`) is
extended to the new per-child response shape, not replaced.

### Technical Requirements

**API impact**: Extends the existing `GET/PUT/POST(publish)/POST(unpublish) /api/locations/
{locationId}/monthly-menus/{year}/{month}` family with an optional `variant` query parameter.
Extends `GET /api/parent/monthly-menu` to return one entry per (location, child) pair instead of
one per location. Adds a way for a director to read/update a location's
`MenuVariantPriorityOrder` (likely as part of the existing location-settings endpoint(s), 013f's
precedent).

**Data-model impact**: `MonthlyMenu` gains a nullable `Variant DietaryType?` column; its unique
constraint changes from `(LocationId, Year, Month)` to `(LocationId, Year, Month, Variant)`.
`Location` gains `MenuVariantPriorityOrder List<DietaryType>` (ordered, stored the same way
`MealPreference.DietaryType` already stores a `List<DietaryType>`). No changes to
`MonthlyMenuDay`.

**Security considerations**: No new authorization boundary — variant endpoints reuse the
existing `DirectorOnly` policy (authoring) and existing parent-contact-resolution authorization
(reading), both already tenant-scoped. A variant not present in a location's
`MenuVariantPriorityOrder` must be rejected server-side on write, not just hidden client-side.

**Performance considerations**: At most 6 `MonthlyMenu` rows per location/month (1 base + up to 5
variants, bounded by the fixed `DietaryType` enum) — negligible query cost. Parent-facing
resolution is O(children × locations × variants), all small bounded numbers.

**Testing requirements**: Backend integration tests (real PostgreSQL, constitution Principle V)
for: the new unique constraint, variant-not-enabled rejection, priority-order resolution
(including a child with multiple matching types), fallback to base menu, and confirmation that
the base-menu path is byte-for-byte unchanged for a location with no variants configured.
Director-web component tests for the variant selector and priority-order settings control.
Parent-mobile component tests for per-child resolved rendering.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director configures which variants a location offers (Priority: P1)

A director whose KDV serves several vegetarian and halal children wants to offer separate menus
for those diets, so they enable those two variants for their location and decide vegetarian
should take priority if a child qualifies for both.

**Why this priority**: Without this, no variant can ever be authored or resolved — it's the
on/off switch and the tie-breaker for everything else in this feature.

**Independent Test**: In the location's settings, enable "Vegetarian" and "Halal" and order them
with Vegetarian first; confirm the setting persists and that only these two variants become
selectable in the Menu section for this location, in this order.

**Acceptance Scenarios**:

1. **Given** a location with no variants configured, **When** the director opens its settings,
   **Then** every `DietaryType` is available to enable, none are enabled by default, and the
   location's menu behaves exactly as it did before this feature (base menu only).
2. **Given** the director enables Vegetarian and Halal for a location, **When** they save,
   **Then** the Menu section's variant selector offers exactly those two options (plus the base
   menu), in the order the director set.
3. **Given** variants are already enabled, **When** the director removes one, **Then** it is no
   longer selectable for future authoring, but any `MonthlyMenu` rows already published for it
   are not deleted and can be restored by re-enabling it later.

---

### User Story 2 - Director authors and publishes a variant menu (Priority: P1)

A director who has enabled the Vegetarian variant fills in that month's vegetarian menu using
the same day-grid (and optionally CSV import) they already use for the base menu, then publishes
it independently.

**Why this priority**: This is the actual authoring work — without it, an enabled variant has
nothing for a parent to ever see.

**Independent Test**: Select the Vegetarian variant in the Menu section for a location where it's
enabled; fill in a full month via the day-grid; publish; confirm the base menu for that same
location/month is completely unaffected by this action.

**Acceptance Scenarios**:

1. **Given** a location has Vegetarian enabled, **When** the director selects it in the Menu
   section, **Then** the same day-grid used for the base menu loads, empty, ready for
   independent authoring.
2. **Given** the director fills in and saves a draft of the Vegetarian variant, **When** they
   check the base menu for the same location/month, **Then** the base menu is completely
   unchanged.
3. **Given** the director publishes the Vegetarian variant, **When** they later un-publish it,
   **Then** only that variant's publish state changes — the base menu and any other variant's
   publish state are unaffected.
4. **Given** a `DietaryType` is not enabled for a location, **When** any attempt is made to
   author or publish a menu for it (including a direct API call), **Then** the system rejects it.
5. **Given** a director imports a CSV (013i) while a variant is selected, **When** the import
   completes, **Then** it fills in that variant's grid exactly as it would the base menu's — no
   variant-specific behavior in the import path.

---

### User Story 3 - Parent automatically sees the right menu per child (Priority: P1)

A parent with two children at the same KDV — one with a vegetarian preference, one with no
dietary preference recorded — opens the Menu tab and sees each child's correctly resolved menu,
without doing anything to select it.

**Why this priority**: This is the entire point of the feature from a family's perspective —
without automatic per-child resolution, enabling and authoring variants delivers no value.

**Independent Test**: With a location's Vegetarian variant published and a child whose
`MealPreference.DietaryType` includes Vegetarian, load that parent's Menu tab and confirm the
child's section shows the vegetarian menu, clearly labeled, while a sibling with no matching
preference shows the base menu.

**Acceptance Scenarios**:

1. **Given** a child has no `DietaryType` set, **When** their parent views the Menu tab, **Then**
   that child's section shows the base menu, exactly as before this feature.
2. **Given** a child's `DietaryType` includes Vegetarian and the location's published Vegetarian
   variant exists for the month, **When** the parent views the Menu tab, **Then** that child's
   section shows the Vegetarian variant, labeled in plain language.
3. **Given** a child's `DietaryType` includes both Vegetarian and Halal, and the location's
   `MenuVariantPriorityOrder` ranks Halal above Vegetarian, **When** the parent views the Menu
   tab, **Then** that child's section shows the Halal variant (the higher-priority match).
4. **Given** a child qualifies for a variant but that variant's menu is still a draft (not
   published) for the month, **When** the parent views the Menu tab, **Then** that child's
   section falls back to the base menu (or the next-lower-priority matching variant that IS
   published), never showing draft content.
5. **Given** a parent has two children enrolled at the same location with different dietary
   preferences, **When** they view the Menu tab, **Then** they see one section per child (not
   one merged section per location), each independently resolved.

---

### Edge Cases

- A location has variants enabled but a director never publishes one for a given month: every
  child at that location falls back to the base menu, same as if variants didn't exist.
- A child qualifies for a variant whose menu for the month is still a draft: treated as "no
  published variant" for that child, falling back per the resolution order (US3/AC4).
- A director removes a `DietaryType` from `MenuVariantPriorityOrder` after menus already exist
  for it: those rows are retained, not deleted, and become selectable again if re-enabled.
- A child has zero `DietaryType`s recorded: always resolves to the base menu.
- A director attempts to author or publish a variant not currently enabled for that location
  (including via direct API call, not just the UI): rejected server-side.
- A parent has multiple children at the same location, each qualifying for a different variant:
  each child's section resolves independently; one child's variant assignment never affects
  another's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let a director configure, per location, which `DietaryType` values
  (Halal, Kosher, Vegetarian, Vegan, GlutenFree) are enabled as menu variants. No variant is
  enabled by default for any location.
- **FR-002**: The system MUST let a director set and change the priority order of a location's
  enabled variants.
- **FR-003**: The system MUST let a director author a variant's monthly menu using the exact same
  day-by-day grid interaction the base menu already uses (013e), including CSV import (013i),
  with no variant-specific authoring UI beyond selecting which variant is being edited.
- **FR-004**: The system MUST let a director publish and un-publish each variant's menu
  independently of the base menu and of every other variant, for the same location and month.
- **FR-005**: The system MUST persist a variant's authored content (draft or published)
  completely independently of the base menu's content — editing or publishing one MUST NOT alter
  the other.
- **FR-006**: The system MUST reject any attempt (through the web UI or a direct API call) to
  author, publish, or unpublish a menu for a `DietaryType` that is not currently enabled in that
  location's variant configuration.
- **FR-007**: The system MUST retain a variant's previously-authored `MonthlyMenu` rows when a
  director removes that `DietaryType` from the location's enabled variants — data is preserved,
  only future selectability is affected. Re-enabling the type MUST make the prior content
  selectable again.
- **FR-008**: For each parent-visible child at a location, the system MUST resolve which menu
  (variant or base) to show by reading that child's `MealPreference.DietaryType` list and
  checking the location's `MenuVariantPriorityOrder` in order, selecting the first
  `DietaryType` that both (a) the child has and (b) has a *published* `MonthlyMenu` for the
  requested month.
- **FR-009**: If no enabled variant satisfies FR-008 for a given child (no matching type, no
  published menu for any matching type, or no `DietaryType` recorded at all), the system MUST
  fall back to that location's base menu (`Variant == null`), following its own existing
  published/draft visibility rule (013e).
- **FR-010**: The parent-facing monthly menu read MUST return one resolved entry per (location,
  child) pair for every child the parent has an active contract for, rather than one entry per
  location — a parent with multiple children at the same location MUST see each child's
  independently resolved menu.
- **FR-011**: The parent-facing UI MUST clearly label which variant (if any) is being shown for
  each child, in natural language rather than a raw enum/technical name, and MUST NOT expose that
  a resolution process occurred (no visible "fallback" messaging).
- **FR-012**: A location with no variants enabled MUST behave identically, in every respect
  (authoring, publishing, and parent-facing reads), to how the base-menu-only system behaved
  before this feature — zero observable behavior change for the common case.
- **FR-013**: All new director-facing and parent-facing strings introduced by this feature MUST
  be provided through the existing i18n systems (NL/FR/EN on web, NL/FR/EN on parent-mobile),
  matching every other user-facing string in these feature areas.

### Key Entities

- **`MonthlyMenu`** (extended, tenant schema): gains a nullable `Variant` field (one of the
  existing `DietaryType` values, or null for the base menu). Uniquely identified by
  `(LocationId, Year, Month, Variant)` instead of `(LocationId, Year, Month)`. No changes to its
  relationship with `MonthlyMenuDay`.
- **`Location`** (extended): gains an ordered list of enabled `DietaryType` variants
  (`MenuVariantPriorityOrder`), representing both which variants are offered and their priority
  for multi-match resolution.
- **`MealPreference`** (existing, 013d, unchanged): its `DietaryType` list is the read-only input
  this feature's resolution logic consults per child — this feature adds no new field to it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can enable a variant, author a full month for it, and publish it using
  no more steps than the base menu already requires (same grid, same Save/Publish actions) — the
  only added step is selecting which variant is being edited.
- **SC-002**: 100% of children with a `DietaryType` matching a published, enabled variant see
  that variant (or the correct higher-priority match, when multiple qualify) — never the base
  menu when a matching published variant exists.
- **SC-003**: 100% of locations that have never configured any variant show zero behavioral
  difference from before this feature shipped, for both directors and parents.
- **SC-004**: A parent with multiple children at the same location sees each child's menu
  resolved independently, with zero cross-contamination between siblings' dietary results.

## Assumptions

- The five existing `DietaryType` enum values (013d) are the complete set of variant dimensions
  for this feature — allergen-based variants are explicitly out of scope (confirmed with the
  product owner; allergy information continues to be surfaced the way 013e/013c already do,
  outside this feature).
- A child can match more than one enabled variant simultaneously; the location's director-
  configured priority order is the sole, deterministic tie-breaker — there is no per-child
  override and no parent-facing choice (confirmed with the product owner).
- A variant menu is authored completely independently of the base menu (no inherited/overridden
  days) — confirmed with the product owner as the authoring model, accepting that a director may
  retype shared dishes across variants (013i's CSV import exists specifically to ease this).
- `MenuVariantPriorityOrder` is a per-location setting, not per-organisation — a multi-location
  KDV can offer different variants (or a different priority order) at each of its locations,
  consistent with every other per-location setting in this codebase (013f's precedent).
- No parent-facing changelog or notification exists for "your child's menu variant changed" —
  matches 013e's existing behavior for base-menu corrections (parent sees the current state on
  next app open, nothing more).
