# Product

## Register

product

## Platform

adaptive

## Users

Three distinct personas share one brand and one backend:

- **Caregivers** (tablet, mounted or laid flat, landscape-locked) — busy, standing, often
  one-handed, multitasking. Job to be done: complete attendance, meals, naps, and incident
  logging in seconds, without breaking attention on the room. Identify by PIN on a shared
  device (kiosk mode), not individual login.
- **Parents** (mobile, handheld) — emotional users checking in on their child during the day.
  Job to be done: get reassurance ("what happened with my child today?") through photos,
  timeline updates, and warm, human-toned communication — not data entry.
- **Directors** (web, desktop) — running the operational and administrative side: staffing,
  billing, compliance, reporting. Job to be done: see across rooms/children at a glance, manage
  exceptions, and act on dense information quickly.

## Product Purpose

A childcare operations platform that replaces the paper/whiteboard workflow of a childcare
center with one connected product: caregivers log care events in real time, parents receive
warm, immediate visibility into their child's day, and directors get the operational and
reporting layer to run the business. Success is measured by caregivers never feeling slowed
down by the software, parents feeling reassured rather than surveilled, and directors having
trustworthy data without manual reconciliation.

## Brand Personality

Calm, trustworthy, unhurried-but-fast. Three words: **calm, warm, efficient**. The product
should never feel like enterprise software (cold, transactional, ticket-system tone) or like a
consumer social app (attention-seeking, gamified). It is warm without being twee, and fast
without being frantic.

## Anti-references

- The generic 2025/2026 AI-SaaS look: giant hero sections inside app screens, excessive
  rounded cards, random gradients, decorative illustrations without purpose, every piece of
  content wrapped in its own card, identical dashboard layouts.
- Cream/warm-neutral background paired with a serif display face — a recognizable
  AI-generated-design signature this system explicitly avoids (see `design-system.md`
  Typography).
- The vivid `#2563EB` blue / cool-gray palette shipped in features 001–008 — deliberately
  superseded by a muted steel-blue + cream/tan neutral scale (see `design-decisions.md`).
- Desktop-style dense tables and long configuration forms surfaced on the caregiver tablet —
  that complexity belongs on director web only.
- Parent-facing copy that reads like a system log or ticketing tool ("Nap record created at
  13:45") instead of natural language ("Emma had a great nap today").

## Design Principles

- One coherent brand across three surfaces — caregiver tablet, parent mobile, director web
  differ in density and interaction pattern, never in core visual identity.
- Information before decoration — typography and spacing establish hierarchy before color or
  containers do.
- Speed and glanceability for caregivers outrank visual richness; warmth and reassurance for
  parents outrank density; density and control for directors outrank simplicity.
- Do not copy any single reference product wholesale — combine the operational efficiency of
  childcare-specific tools, the emotional warmth of family communication apps, the speed of
  frontline business tools, and the clarity of modern productivity software.
- Low cognitive load throughout: minimal typing, intelligent defaults, quick selection over
  free text wherever the workflow allows it.

## Accessibility & Inclusion

- WCAG AA minimum: 4.5:1 contrast for normal text, 3:1 for large/bold text and UI fills —
  verified per-token in `design-system.md`, not assumed.
- Touch targets: 48pt minimum everywhere on caregiver tablet and parent mobile, 64pt for the
  single highest-frequency interaction on a screen (e.g. kiosk PIN entry).
- Every animation has a reduced-motion alternative (instant change or opacity crossfade) —
  `prefers-reduced-motion` on web, `AccessibilityInfo`/`useReducedMotion` on mobile. Every
  semantic color (danger/warning/success/info) pairs with an icon, never color alone.

## Surfaces

This is one product with three differentiated surfaces rather than three separate products.
Platform above is set to `adaptive` for the primary/most complex surface (the caregiver +
parent mobile app, Expo/React Native, one codebase shipping both iOS and Android). Director
web (`web/`, Next.js) is a separate platform (`web`) within the same brand — when an impeccable
command targets `web/` specifically, treat that surface as `web`, not `adaptive`, and skip
`reference/ios.md` / `reference/android.md` guidance for it.

Note also: this app does not adopt per-OS HIG/Material chrome or components — it renders one
deliberate custom design system (see `design-system.md`) identically on iOS and Android.
Platform-specific reference material should inform platform *mechanics* (safe areas, gesture
zones, native navigation idioms) on the mobile surface, not its visual language, which
`design-system.md` already owns.

**Out of scope of this PRODUCT.md**: a public marketing site is planned (selling the platform
to prospective childcare centers — homepage, pricing, sign-up) and is deliberately **not**
part of the "one product, three surfaces" model above. It's a distinct, brand-register surface
with its own visual license (larger type, freer layout, persuasive copy) that should not
inherit this document's product-register constraints, and this document's Design Principles
and Anti-references should not be read as applying to it. It doesn't exist in the codebase yet
(`web/app/` currently has only `(app)` admin routes and `(auth)` login). When it's built, give
it its own `PRODUCT.md` (e.g. at its own route group or sub-path) with `Register: brand` — do
not fold it into this file or treat it as another view of the director-web admin product.
