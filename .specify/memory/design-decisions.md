# Design Decisions

A running log of cross-feature UX decisions, so features don't each re-litigate things like
"where does navigation go." Governed the same way as `workflows.md`:

- Claude may add decisions when a feature settles one, and may revise a decision a later
  feature's research supersedes.
- Claude must not silently drop or contradict an existing decision — superseding one requires
  updating the entry in place and documenting what changed, why, and which feature drove it.
- If a decision and the current BACKLOG.md/spec content disagree, the spec is the source of
  truth going forward; fix the decision entry rather than leaving the contradiction standing.

## Current decisions

- Parent app uses timeline as the primary home screen.
- Caregiver tablet opens into kiosk PIN entry (feature 008a), then today's classroom for the
  identified caregiver. *(Superseded 2026-07-07 — previously "opens directly into today's
  classroom," written before industry research showed caregivers share one tablet per room and
  identify by PIN rather than each logging in individually. See BACKLOG.md feature 008a.)*
- Director web uses sidebar navigation.
- Photos are treated as primary content, not attachments.
- Attendance is optimized for speed over data entry.
- Primary/interactive color moves to a muted steel-blue (`#4F7CAC` light / `#7CA0C7` dark),
  with a cream/tan neutral scale to match — see `design-system.md`'s Color section for the
  full token set. *(Superseded 2026-07-07, same day it was written — the first version of
  this entry kept the shipped `#2563EB` blue and argued against a retrofit for warmth alone.
  That held only until an actual warm palette was proposed and reviewed; genuine warmth in the
  neutral scale plus a repainted primary was judged worth it. Feature 008 is still uncommitted,
  so this is the cheapest point to apply it — touches `mobile/hooks/useColors.ts` and the
  Tailwind `gray-*`/`blue-*` className strings in `login.tsx`, `index.tsx`, `child/[id].tsx`,
  `_layout.tsx`, `ThemedModal.tsx`.)*
- Color tokens live in exactly one file, `mobile/theme/colors.js`, imported by both
  `tailwind.config.js` and `hooks/useColors.ts` — not duplicated between them. *(2026-07-07 —
  before this, the palette was defined twice (a hardcoded object in `useColors.ts`, plus raw
  Tailwind classNames scattered across every screen), which is exactly why the v2 palette
  change above needed five files touched by hand. A platform-wide color change is now a
  one-line edit in `theme/colors.js`. Applies to `mobile/` only for now — when `web/`
  (director) exists, it needs its own consumer of the same token values, likely via a shared
  Tailwind preset or CSS custom properties, rather than a third hardcoded copy.)*
