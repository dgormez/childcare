---
version: alpha
name: ChildCare Design System
description: Calm, trustworthy design system for a childcare operations platform spanning caregiver tablet, parent mobile, and director web on one shared brand.
colors:
  background: { light: "#FAF9F6", dark: "#1C1917" }
  surface: { light: "#FFFFFF", dark: "#262220" }
  surface-soft: { light: "#F5F3EE", dark: "#302B27" }
  border: { light: "#E7E2D8", dark: "#47403A" }
  text: { light: "#1F2937", dark: "#F5F1EA" }
  text-soft: { light: "#6B7280", dark: "#A39B8F" }
  primary: { light: "#4F7CAC", dark: "#7CA0C7" }
  primary-hover: { light: "#3D638A", dark: "#93B2D3" }
  primary-soft: { light: "#E8EEF4", dark: "primary @ 15% opacity" }
  danger: { light: "#B91C1C", dark: "#FCA5A5" }
  warning: { light: "#F59E0B", dark: "#D97706" }
  success: { light: "#15803D", dark: "#86EFAC" }
  info: { light: "#0EA5E9", dark: "#0284C7" }
  accent-director: { light: "#5B5FC7", dark: "#8B8EE0" }
  accent-parent: { light: "#B8768C", dark: "#D49CB0" }
typography:
  family: "Public Sans"
  weights: [300, 400, 500, 600, 700, 800]
  mono: "ui-monospace"
rounded:
  input: 8px
  card: 12px
  pill: full
spacing:
  1: 4px
  2: 8px
  3: 12px
  4: 16px
  5: 24px
  6: 32px
---

## Overview

Warm, calm, low-cognitive-load. Typography and spacing carry hierarchy; color and containers
are secondary. One brand across three surfaces (caregiver tablet, parent mobile, director
web) that differ in density and interaction pattern, never in core visual identity. Minimal
gradients, minimal decoration, no illustration library.

## Colors

Neutral scale is a genuine warm cream/tan (not a tinted-white default), paired with a muted
steel-blue primary — a deliberate departure from the vivid blue shipped pre-2026-07. `primary`
is fill-only (white-on-primary clears large-text AA at ~4.36:1 but not body text); use
`primary-hover` for primary-hued text/links (~5.9:1 against background).

Semantic colors (`danger`, `warning`, `success`, `info`) are locked platform-wide and never
reused for branding. Per-surface accent colors (`accent-director`, `accent-parent`) are
chrome-only — nav highlights, selected-tab underline — never a badge, banner, or checkmark.
Caregiver has no accent by design (green would collide with `success`'s meaning on the one
surface where glanceable status matters most).

Dark-mode hover states go lighter, not darker, against the dark ground.

## Typography

Single family, Public Sans, across the full 300–800 weight range, used for headings, body,
labels, and captions alike — deliberately not Inter or Space Grotesk (too recognizable as the
"safe AI default"), and deliberately not a display/body pairing (this app has too much small
utility text — table cells, timestamps, card metadata — for a pairing to earn its keep).
Numeric/tabular data (attendance counts, invoice amounts, timestamps) uses `ui-monospace` with
`font-variant-numeric: tabular-nums` so digit columns align.

Avoid: serif display face on the cream background — a recognizable AI-generated-design tell.

## Layout

Spacing scale: 4 / 8 / 12 / 16 / 24 / 32, no other values. Density is per-surface: tablet
medium, mobile low, web high. Cards are containers, not decoration — avoid nested cards and
avoid giant empty sections; prefer lists and timelines over dashboards of cards.

## Elevation & Depth

Shadows reserved for genuinely elevated/floating elements only (a modal, a menu) — never a
default card treatment. Use `surface` vs `surface-soft` (a flat background shift) to
distinguish grouped content instead of a shadow or border.

## Shapes

Corner radius: `8px` inputs/small controls, `12px` cards, `full` round only for avatars and
pill badges/chips. Nothing else gets rounded.

## Components

- **Buttons** — primary: `bg-primary` fill, white text, `8px` radius, disabled via `bg-border`
  at reduced opacity. Secondary: `border` outline, transparent fill. Destructive: text-style
  `danger`, not a filled button. Touch feedback via opacity dip (`active:opacity-60`), not
  hover, except `primary-hover` reserved specifically for director-web `:hover`.
- **Forms** — `surface-soft` fill, `8px` radius, no visible border unless invalid
  (`danger`-colored). Caregiver forms are rare/short and favor selection over typing.
- **Status** — badge (pill, full round) for a single short state on an item; banner (full
  width) for a screen-level state (offline, sync-pending). Never a per-surface accent color for
  either.
- **Icons** — `lucide-react-native`, one stroke-based set, `2px` stroke width consistently.
  Sizes: `16px` inline, `20px` default, `24px` standalone/emphasized. Never mix icon families
  on one screen.
- **Empty states** — one icon + one short human sentence, no illustration library.
- **Motion** — subtle, under 250ms, no bounce/elastic easing. Reward state changes (an item
  going from pending → synced), not initial render — no staggered fade-up on list mount.
  Offline banner slides in/out (~200ms). Screen transitions are platform-native stack push/pop
  only.

## Do's and Don'ts

**Do:**
- Verify contrast per-token against the actual background it appears on before adding a color.
- Keep color tokens in exactly one file per platform (`mobile/theme/colors.js`,
  `web/theme/colors.ts`), kept in sync by hand until a shared package exists.
- Use typography size/weight/spacing before reaching for color or a container.

**Don't:**
- Don't hardcode a hex value or raw Tailwind color class in a component.
- Don't nest cards, or default every piece of content into a card.
- Don't use a per-surface accent color for a badge, banner, or checkmark.
- Don't animate on initial list render — only on real state changes.
- Don't pair the cream background with a serif display face.
