# ChildCare Design System

## Product Principles

- Calm and trustworthy.
- Low cognitive load.
- Optimized for busy caregivers.
- Optimized for stressed parents.
- Information before decoration.
- One coherent product across all three surfaces (caregiver tablet, parent mobile, director
  web) — same company, same platform. Surfaces differ in density and interaction pattern
  (see Platform Rules), never in core brand identity.

## Visual Style

- Warmth is real, not just tonal: a cream/tan neutral scale and a muted steel-blue primary
  (see Color below) — this is a deliberate departure from the vivid blue shipped in features
  001–008; see `design-decisions.md` for what that costs to adopt.
- Minimal gradients.
- Corner radius scale: `8px` for inputs/small controls, `12px` for cards, `full round` only
  for avatars and pill-shaped badges/chips. Nothing else gets rounded — a rounded rectangle on
  every container is exactly the generic-AI-UI look this system exists to avoid.
- Typography is primary hierarchy: lean on size/weight/spacing to establish structure before
  reaching for color or a container.
- Shadows are reserved for genuinely elevated/floating elements only (a modal, a menu) — not
  a default card treatment. Use `surface` vs `surface-soft` (a background shift) to distinguish
  grouped content instead.
- Empty states: an icon (from the lucide set below) + one short human sentence. No
  illustration library — avoids introducing a second visual language just for empty states.

## Color

Full palette with usage examples, light/dark values, typography specimen, and applied UI
mocks: `https://claude.ai/code/artifact/2000ab08-00a7-4abb-a43f-b4332d94e825`

**Implementation**: all values below live in exactly one file,
`mobile/theme/colors.js` — imported by both `tailwind.config.js` (className usage: `bg-primary
dark:bg-primary-dark`) and `hooks/useColors.ts` (the few call sites needing a raw hex: status
bar tint, native `Image` tint). Changing a color platform-wide is a one-line edit in that file,
nowhere else. Never hardcode a hex value or a raw Tailwind color (`bg-gray-100`, `text-red-500`,
etc.) in a component — always the semantic token.

### Neutral & primary (v2 — 2026-07-07, supersedes the blue/gray set shipped in 001–008)

| Token         | Light     | Dark                     |
|---------------|-----------|--------------------------|
| background    | `#FAF9F6` | `#1C1917`                |
| surface       | `#FFFFFF` | `#262220`                |
| surface-soft  | `#F5F3EE` | `#302B27`                |
| border        | `#E7E2D8` | `#47403A`                |
| text          | `#1F2937` | `#F5F1EA`                |
| text-soft     | `#6B7280` | `#A39B8F`                |
| primary       | `#4F7CAC` | `#7CA0C7`                |
| primary-hover | `#3D638A` | `#93B2D3`                |
| primary-soft  | `#E8EEF4` | `primary @ 15% opacity`  |

`surface-soft` sits between `surface` and `border` — use it for secondary emphasis within a
surface (a nested row, a subtly-different card) without a full border. Dark-mode hover goes
*lighter*, not darker, since darkening further loses contrast against a dark ground.
`primary-soft` is a pale tint for selected-but-not-filled states (a selected chip, an active
tab background) — new in this revision, not yet used anywhere in shipped code.

**`primary` is fill-only.** White text on `primary` measures ~4.36:1 contrast — passes WCAG AA
for large/bold text (buttons, 3:1 threshold) but falls short of the 4.5:1 needed for regular
text. Never use `primary` as a text color on a light background. If primary-hued text is
needed (a link, an active tab label), use `primary-hover` instead — verified ~5.9:1 against
`background`, clears AA comfortably.

### Theming

All three surfaces always follow the device/system light-dark setting — never force a theme,
and never pick one based on surface, lighting assumptions, or time of day. Users choose their
own OS-level preference for a reason; the app respects it uniformly, caregiver tablet included.

### Semantic (formalizing what was chosen ad hoc in feature 008 — unchanged by the v2 neutral/primary revision)

- **danger** — allergy alerts, incidents, form errors.
  Light: `#B91C1C` on `#FEF2F2`. Dark: `#FCA5A5` on `rgba(127,29,29,.3)`.
- **warning** — offline banner, pending-sync count.
  Light: `#F59E0B` solid banner, dark text (`#1C1917`, fixed — same value both themes; white
  text on amber reads poorly). Dark: `#D97706` solid banner, same fixed dark text.
- **success** — synced state, present/checked-in.
  Light: `#15803D` on `#F0FDF4`. Dark: `#86EFAC` on `rgba(20,83,45,.3)`.
- **info** — sync-in-progress status.
  Light: `#0EA5E9` solid banner, white text. Dark: `#0284C7` solid banner, white text.

`info` is a distinct hue from `primary` on purpose — a status banner should never read as a
tappable button. `warning` is amber, not the raw `yellow-500` feature 008 shipped with — same
family, warmer, less hazard-tape. Semantic colors are **locked platform-wide** — never reused
for branding or per-surface identity (see Per-Surface Accents below for why that matters).

**Never convey a semantic state by color alone** (WCAG 1.4.1). Every badge and banner pairs its
color with an icon (from the Icons scale below) — a colorblind caregiver, or anyone glancing
fast enough that hue doesn't register, still needs to tell `danger` from `warning` from `info`
without reading a color. See Status indicators under Components for the paired icon per state.

### Per-surface accents (chrome-only — reviewed 2026-07-07)

A second agent's proposal suggested a distinct brand accent per surface for non-semantic
identity: deep green for caregiver, coral for parent, indigo for director. Reviewed and
adjusted before adoption:

- **Director → indigo.** `#5B5FC7` light / `#8B8EE0` dark. Kept as proposed — distinct from
  primary and all four semantics.
- **Parent → dusty rose, not coral.** `#B8768C` light / `#D49CB0` dark. Coral sits close
  enough to danger-red (`#B91C1C`) to risk reading as an alert at a glance; rose reads clearly
  pink, not red.
- **Caregiver → none.** Dropped entirely, not just recolored. Green was proposed for
  "completed actions, positive operational feedback" — that's `success`'s job already; a
  second color for the same meaning adds confusion, not information, on the one surface where
  glanceability matters most (see Platform Rules). Reconsider only if a real screen needs pure
  wayfinding color with zero status meaning.

**Usage rule**: these are chrome only — a nav highlight, a selected-tab underline, a chart's
non-semantic data series. Never a badge, banner, or checkmark, which is exactly where a color
gets read as status. Not used in any shipped screen yet (`mobile/` is caregiver-only and gets
no accent by design).

## Typography

**Public Sans**, one family across the full weight range (300–800), used for everything —
headings, body, labels, captions. Not Inter, not Space Grotesk — both are the instantly
recognizable "safe default," which defeats the point of choosing deliberately. Public Sans is
the U.S. Web Design System's typeface: humanist (some warmth, not geometric-cold), built for
screen legibility at small sizes (matters here — dense director tables, small caregiver-card
metadata), free, and still uncommon enough not to read as a template choice.

One family rather than a display/body pairing deliberately: this is an app with a lot of small
utility text (labels, table cells, timestamps) where cross-scale consistency matters more than
typographic flourish — a display/body pairing is an editorial-page decision, not an app one.

Pair with **`ui-monospace`** (system stack, no extra font needed) plus
`font-variant-numeric: tabular-nums` for anything tabular — attendance counts, invoice amounts,
timestamps — so digit columns actually align.

Live specimen (heading/body/label/caption scale, weight strip, tabular-nums demo), rendered,
not described: see the Color artifact link above.

**Avoid**: a serif display face paired with the cream background — that combination (warm
cream + serif) is one of the most recognizable AI-generated-design signatures, even without
the terracotta accent that usually completes it.

## Icons

**`lucide-react-native`** — a single, consistent stroke-based icon set (matches the
Linear/Notion references cited for director web). Not yet installed in `mobile/`; add it as a
dependency as part of whichever feature next touches iconography (008a is a natural first
user — allergy/PIN/lock icons), replacing the raw emoji (⚠️, 🌡️) feature 008 shipped as a
placeholder.

- **Stroke width**: `2px`, consistently — inconsistent stroke weight across icons is its own
  "assembled, not designed" tell.
- **Size scale**: `16px` inline-with-text, `20px` default (list items, form fields), `24px`
  standalone/emphasized (empty states, alert icons).
- Never mix icon families (e.g., a stroke icon next to a filled/glyph icon) on the same screen.

## Spacing

4, 8, 12, 16, 24, 32.

## Density

Row/list-item height and internal padding, pinned to the Spacing scale above so density is a
falsifiable number, not a description:

- **Mobile (parent) — low density**: `56px` row/item min-height, `16px` internal padding,
  `32px` section gap.
- **Tablet (caregiver) — medium density**: `48px` row/item min-height, `12px` internal
  padding, `24px` section gap.
- **Web (director) — high density**: `40px` row min-height, `8px` vertical / `12px`
  horizontal cell padding, `16px` section gap.

Tablet's `48px` row height isn't a coincidence — it matches the 48pt touch-target floor
exactly, so a dense list is never denser than what's still tappable. Web has no touch-target
floor (mouse/keyboard), so its rows can run tighter.

## Components

- Cards are containers, not decoration.
- Avoid nested cards.
- Avoid giant empty sections.
- Prefer lists and timelines.

### Buttons

- **Primary**: `bg-primary` fill, white text, `8px` radius. Disabled: `bg-border`, same text
  color at reduced opacity — never a separate gray hardcoded outside the token set.
- **Secondary**: `border` outline, `text` fill color, transparent background.
- **Destructive**: `text-danger`, used as a text-style button (e.g. a modal's destructive
  action) rather than a filled button, so it doesn't compete visually with the one primary
  action on screen.
- Touch feedback (tablet/mobile): opacity dip on press (`active:opacity-60`, already the
  pattern in `ThemedModal.tsx`), not a hover state — hover barely exists on touch surfaces.
  Reserve `primary-hover` specifically for director-web `:hover`.

### Forms

- Inputs: `surface-soft` fill, `8px` radius, no visible border unless invalid (then
  `danger`-colored border).
- Optimize for the platform: caregiver forms should be rare and short (prefer selection over
  typing, per Platform Rules); parent/director forms can be longer but still avoid asking for
  anything not strictly needed.

### Status indicators

- A **badge** (pill, `full round`) is for a single-word/short state attached to an item — e.g.
  an allergy indicator on a child card.
- A **banner** (full-width strip) is for a screen-level state that affects the whole view — the
  offline banner, the sync-pending banner.
- Never use a per-surface accent color (see above) for either — badges and banners are exactly
  where an accent would be misread as a semantic state.
- **Every badge and banner pairs its semantic color with an icon** — never color alone (see
  Color's Semantic section). Fixed pairing: `danger` → alert-triangle, `warning` → clock (pending/
  offline), `success` → check-circle, `info` → refresh/sync. Reuse the same icon for the same
  meaning everywhere; don't pick a new glyph per screen.

## Motion

- Subtle only, under 250ms, no bouncing. Ease out — `ease-out-quart`/`ease-out-expo` curves,
  never linear, never elastic/spring.
- **Reward state changes, not initial render.** The most common AI-UI motion tell is a
  staggered fade-up on every list as it first appears — don't do that. The group-view list
  should just be there on load. Save motion for something actually changing (a queued item
  transitioning from "pending" to "synced").
- **Press feedback**: opacity/scale dip (~100ms) on button/card press — see Buttons above.
- **Offline banner**: slide down on appearing, slide up on disappearing (~200ms) — a real
  state transition, worth animating, unlike a decorative list entrance.
- **Screen transitions**: platform-native stack push/pop only. No custom transitions.
- **Reduced motion is not optional.** Every animation needs a reduced-motion alternative —
  swap to an instant state change or a plain opacity crossfade, never skip the state change
  itself. Web: honor `prefers-reduced-motion`. Mobile: honor
  `AccessibilityInfo.isReduceMotionEnabled()` / `useReducedMotion()`.

## Accessibility

- Before introducing any new color into the palette, verify contrast against the backgrounds
  it'll actually appear on (WCAG AA: 4.5:1 normal text, 3:1 large/bold text and fills) — don't
  assume "looks fine." See the `primary` fill-vs-text rule above for a worked example.
- Touch targets: 48pt minimum everywhere, 64pt for the highest-frequency single interaction on
  a screen (e.g. kiosk PIN entry, feature 008a) — see `platform-rules.md`.
