# Design Decisions

A running log of cross-feature UX decisions, so features don't each re-litigate things like
"where does navigation go." Governed the same way as `workflows.md`:

- Claude may add decisions when a feature settles one, and may revise a decision a later
  feature's research supersedes.
- Claude must not silently drop or contradict an existing decision — superseding one requires
  updating the entry in place and documenting what changed, why, and which feature drove it.
- If a decision and the current BACKLOG.md/spec content disagree, the spec is the source of
  truth going forward; fix the decision entry rather than leaving the contradiction standing.
- Entries stay terse: the decision, the reasoning, and what it supersedes (if anything). Don't
  enumerate the files touched to implement it — that's what git history and commit messages
  are for; listing it here just goes stale the next time those files change.

## Current decisions

- Parent app uses timeline as the primary home screen.
- Caregiver tablet opens into kiosk PIN entry (feature 008a), then today's classroom for the
  identified caregiver. *(Superseded 2026-07-07 — previously "opens directly into today's
  classroom," written before industry research showed caregivers share one tablet per room and
  identify by PIN rather than each logging in individually. See BACKLOG.md feature 008a.)*
- Director web uses sidebar navigation.
- A future public marketing site (selling the platform to prospective childcare centers) is
  explicitly a separate, brand-register surface — not a fourth view of the "one product, three
  surfaces" operational platform, and not bound by its product-register design principles. Not
  built yet; see `PRODUCT.md`'s Surfaces section.
- Photos are treated as primary content, not attachments.
- Attendance is optimized for speed over data entry.
- Primary/interactive color moves to a muted steel-blue (`#4F7CAC` light / `#7CA0C7` dark),
  with a cream/tan neutral scale to match — see `design-system.md`'s Color section for the
  full token set. *(Superseded 2026-07-07, same day it was written — the first version of
  this entry kept the shipped `#2563EB` blue and argued against a retrofit for warmth alone.
  That held only until an actual warm palette was proposed and reviewed; genuine warmth in the
  neutral scale plus a repainted primary was judged worth it.)*
- Color tokens live in exactly one file per platform — `mobile/theme/colors.js` and
  `web/theme/colors.ts` — each imported by that platform's consumers, never a hardcoded value
  scattered across screens. *(2026-07-07 — replaced a setup where the palette was defined
  twice within `mobile/` alone, a hardcoded object in `useColors.ts` plus raw Tailwind
  classNames on every screen. Updated 2026-07-08 when `web/` shipped: the original version of
  this entry hoped `web/` would consume the same values via a shared Tailwind preset or CSS
  custom properties rather than a second hardcoded copy — in practice `web/theme/colors.ts` is
  a second hand-maintained copy, kept in sync with `mobile/theme/colors.js` by hand, with
  nothing enforcing the two don't drift. Revisit once a shared-package setup exists across
  `mobile/`/`web/`; until then, any color-token change must be applied to both files.)*
