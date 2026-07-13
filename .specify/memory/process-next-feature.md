# Process Next Feature

This is the exact prompt used to process one `BACKLOG.md` feature through the full SpecKit
pipeline (specify → clarify → plan → tasks → checklist → analyze → implement → converge → PR
→ merge). It's saved here so a fresh session (or a teammate) can resume without retyping it.

**This is single-pass, not a loop.** One invocation processes one feature (or resumes whatever
is in progress) to completion or to a clean stopping point, then the turn ends — it does not
reschedule itself and will not chain on to the next backlog item on its own. When you want the
next feature processed, run this prompt again yourself. This was a deliberate choice — see
"Why single-pass" below. (It was previously invoked via `/loop` in dynamic mode; that's no
longer necessary or recommended — see below.)

## Why single-pass

Earlier versions of this prompt ran in `/loop`'s dynamic self-paced mode, which calls
`ScheduleWakeup` at the end of every turn to continue automatically — chaining through the
whole backlog (spec → implement → PR → merge → next feature) with no human checkpoint in
between. That's a lot of unattended autonomy for a pipeline that ends in `gh pr merge`. The
prompt below now explicitly refuses to call `ScheduleWakeup`, so it behaves as one-shot
regardless of how it's invoked — pasting it directly, or via `/loop <prompt>`, are now
equivalent. Just don't use an interval prefix (`/loop 30m ...`), since a cron re-fire would
recreate the same unattended-merge problem this change is meant to avoid. If a run stops
mid-flow (error, blocked question, crashed session), invoke it again — step 0 resumes
in-progress work from wherever it left off.

## Standing process rules (apply regardless of how this is invoked)

- **Fix every `/speckit-checklist`/`/speckit-analyze`/`/speckit-converge` finding**, even ones
  marked LOW/advisory — don't just log them as debt. (Established after an explicit correction
  mid-backlog; see feature 005/006/007's shipped-notes for examples of findings that were fixed
  rather than deferred.)
- When a feature's own prompt block raises a genuinely new, no-precedent scope question (e.g.,
  "should mobile UI work happen here, and is there a foundation for it yet?" — the call that
  led to inserting feature 008 ahead of the original child-events feature), pause and ask
  instead of guessing. Everything else — clarify-phase questions with a clear recommended
  default, minor backend plumbing a feature can't function without — proceed autonomously.
- No screenshot/visual-QA tooling exists in this repo (no Detox/Maestro/simulator pipeline).
  The design-compliance step below is a static code review, not rendered visual QA — don't
  attempt to run a simulator or capture screenshots mid-loop; that's a deliberate future
  decision, not something to improvise.

## The prompt

```text
Process the next eligible feature from the ChildCare SpecKit backlog. This is a SINGLE-PASS
run: do exactly one feature (or resume whatever is already in progress), then STOP. Do not
call ScheduleWakeup. Do not pick up another backlog item afterward. When you finish — or reach
a clean stopping point (blocked question, failing build after retries, red CI) — report status
and end the turn. I will manually run /loop again for the next feature.

Before starting, read these — they define UX, visual, and platform constraints, and every
spec/implementation this loop produces must follow them:
- design-system.md
- platform-rules.md
- reference-products.md
- design-decisions.md
- workflows.md

Each invocation:

0. Check for in-progress work first:
   - Is there a non-master branch checked out?
   - Is there an open (unmerged) PR from a prior run?
   - If yes, resume that feature from wherever it left off. Do not re-run specify/plan/tasks
     if spec.md/plan.md/tasks.md already exist for it.

1. Otherwise:
   - Read BACKLOG.md.
   - Pick the first 🔲 Not started feature (table order) whose "Depends on" column is
     entirely ✅ Done.
   - If none remain, report the backlog is complete. Stop.

2. Find that feature's prompt block under "## Spec Kit Inputs" in BACKLOG.md.

3. Prepare the implementation branch:
   - git checkout master, git pull latest, git checkout -b <branch> (name from the BACKLOG row).

4. Run the SpecKit pipeline:

   a. speckit-specify, with the feature's BACKLOG.md prompt block plus this required
      "Product Context" section appended to the spec:

      ## Product Context

      ### Feature Type
      One of: User-facing UI / API-backend capability / Data-model change / Background
      process / Infrastructure-tooling / Mixed.

      ### Primary Consumer
      One of: Caregiver / Parent / Director / System / Developer / External service.

      ### Workflow Boundary
      Which workflow(s) in workflows.md / Workflows/*.md does this feature belong to? If none
      fits, propose a new workflow and update workflows.md (or add a new Workflows/*.md file)
      as part of this step — don't silently invent behavior outside the documented workflow map.
      Cover: Actors, Actions, Data Flow, Outputs, Cross-platform Impact (which of caregiver
      tablet / parent mobile / director web / backend-only are affected).

      ### User Impact
      One sentence: "This enables [actor] to [capability], resulting in [outcome]."

      ### UX Requirements (only for User-facing UI or Mixed features)
      Persona, platform, user job, success criteria, main flow, loading/empty/error states,
      accessibility, offline behavior — per platform-rules.md and reference-products.md.

      ### Technical Requirements
      API impact, data-model impact, security considerations, performance considerations,
      testing requirements.

      After speckit-specify, verify spec.md actually contains persona/platform/user
      job/success criteria/UX requirements (for UI features) before continuing — if the
      template got skipped, fix the spec now, not later.

   b. speckit-clarify — for every question, pick the option marked recommended/default.
      Only ask me if there's no recommended option AND no comparable precedent in an
      already-Done feature's spec.

   c. speckit-plan.

   d. speckit-tasks.

   e. speckit-checklist — run it, findings are advisory; only stop for a genuine spec
      contradiction or missing functional requirement.

   f. speckit-analyze.

5. Before implementation, a lightweight UX sanity check: confirm target platform(s), primary
   persona, main user goal, and any accessibility/platform-specific interaction requirements
   are actually reflected in the plan/tasks — not a new review pass, just confirming step 4a's
   Product Context section didn't get lost between specify and plan.

6. Implement: run speckit-implement, following design-system.md / platform-rules.md /
   reference-products.md. Concretely: no generic AI-looking UI, no excessive cards, no
   unnecessary gradients, no oversized rounded components, no reinventing a component that
   already exists — match the reference products' quality bar (see reference-products.md's
   per-surface principles).

7. Design compliance review (static code review — no simulator, no screenshots):
   - Read the new/changed UI files for the touched platform(s).
   - Against design-system.md: spacing values are only 4/8/12/16/24/32 (flag anything else);
     no nested cards; no unexplained gradients; any animation is under 250ms with no bounce;
     shared components reused rather than reimplemented.
   - Against platform-rules.md for the relevant surface: caregiver tablet touch targets meet
     the stated minimum (48pt, or a stricter value the feature's own spec sets); parent-facing
     copy reads as natural language, not database/log phrasing; director-web screens expose
     the density/filtering the spec calls for.
   - Fix anything flagged, one remediation pass. If something's still unresolved after that,
     note it in the PR description as follow-up rather than iterating indefinitely on
     subjective polish.

8. Run speckit-converge to close out anything implementation missed, and fix every finding it
   surfaces (same standing rule as checklist/analyze — no LOW-severity items left as debt).

9. Build and run the test suite. If something fails, attempt fixes up to ~3 times. If still
   failing: stop, leave the branch pushed, report what's broken. Never merge broken code.

10. Commit as you go with real messages. Push the branch.

11. gh pr create targeting master.

12. Wait for gh pr checks to go green. Red CI is handled like step 9 — fix, retry, or stop and
    report. Never merge on red.

13. gh pr merge --squash once CI passes.

14. Update BACKLOG.md status for this feature to ✅ Done. Push that change.

15. On a transient API/network error mid-turn, retry with backoff before giving up on the
    current step.

Never force-push. Never merge on failing build/tests/CI. Never start a feature whose
dependencies aren't all ✅ Done. Never skip the design-system/platform-rules read in step 4a.
Never attempt to run a simulator or capture screenshots in step 7 — that tooling doesn't exist;
use the static code review instead.
```

## Progress log (update as features land)

- 001–007: shipped prior to this file existing (see BACKLOG.md's own shipped-notes per feature).
- 008 (`008-caregiver-app-scaffold`): ✅ Done, merged 2026-07-07 (PR #9, squash-merged after
  green CI — 185/185 backend + 58/58 mobile tests). Also folded in before merge: the design
  system foundation (`design-system.md`/`platform-rules.md`/`reference-products.md`/
  `workflows.md`) and its retrofit into feature 008's UI (`mobile/theme/colors.js` as the
  color-token source of truth), plus a critical fix found only by actually running the app on
  a simulator — `app.config.js` still referenced the `expo-apple-authentication`/
  `expo-web-browser` plugins after their packages were removed, so the app couldn't start at
  all; `tsc`/`jest` never caught it since neither touches Expo's native config resolution.
  Feature `008a-caregiver-kiosk-mode` now sits between 008 and 009/010 in BACKLOG.md; 008's
  login screen remains the correct underlying mechanism, scoped as scaffolding pending 008a's
  UX (still being decided separately, not yet started).
- 2026-07-07: loop prompt reworked — fixed a duplicated/malformed step 4 block from an earlier
  edit, added the design-system/workflow-aware spec template, replaced the screenshot-based
  "visual review" step (no simulator/screenshot tooling exists in this repo) with a static
  code-level design-compliance review, and switched from self-rescheduling dynamic-loop mode to
  single-pass (no `ScheduleWakeup`, manual re-invocation between features).
- 008a (`008a-caregiver-kiosk-mode`): ✅ Done, merged 2026-07-08 (PR #10, squash-merged after
  green CI — 218/218 backend + 76/76 mobile tests). Implementation (device pairing, PIN
  management, check-in/out) had already landed in an earlier session with zero test coverage
  for ~40 of tasks.md's own tasks (T017-T044, T056-T076) and a missing US6 (device-token
  rotation) production implementation; this pass wrote the backend/mobile tests, implemented
  `DeviceTokenRotationFilter`, fixed a real design bug the tests surfaced (naive strict-version-
  match token rotation would have invalidated an offline-queue replay burst — fixed with a
  one-generation grace window), added FR-021's missing audit logging for revoked-device
  rejections, built the missing `AdministratorConfirmation.tsx` mobile component (US5), and
  extracted a shared `CaregiverCard` component during the design-compliance pass. Also
  committed a separate in-flight fix found by actually running the app on-device: React
  Native's `fetch` silently dropped POST bodies through the base-URL-rewrite `new Request(url,
  request)` pattern, and several mobile services were re-reading `result.response.json()` after
  openapi-fetch had already consumed it. `/speckit-converge` surfaced one finding (FR-023's
  "tenant's configured timezone" assumption vs. no timezone field anywhere in the domain
  model) — resolved by documenting the simplification per an explicit decision, not new scope.
  T052 (wiring check-in/out through feature 008's offline queue) is intentionally not
  implemented — PIN correctness can't be verified client-side, so an optimistic queue risks a
  false "checked in" record, which undermines this feature's whole audit purpose; see
  tasks.md's T052 entry for the full reasoning.
- 007a (`007a-web-admin-scaffold`): ✅ Done, merged 2026-07-08 (PR #11, squash-merged after
  green CI — 225/225 backend + 18/18 web tests). First real screens in `web/`: Habits template
  removed, director login (email/password + Google OAuth) rebuilt on a generated openapi-fetch
  client, a collapsible sidebar shell, and Staff/Devices management screens. Two small backend
  additions were needed and added (`GET /api/devices`, `GET /api/organisations/me` +
  `AuthenticatedUser.Name`) since 008a/003 never had a reason to expose them until this UI
  needed them — see BACKLOG.md's shipped-notes for the full reasoning. Also fixed two real
  pre-existing bugs found while wiring login: the `/api/refresh` BFF route and Google sign-in
  never sent `organisationSlug`, silently broken against feature 003's contract since it
  shipped. `/speckit-checklist` and `/speckit-analyze` each surfaced a few findings (a missing
  loading-state requirement, a task-coverage gap, a duplicated task) — all fixed, not deferred.
  `/speckit-converge` surfaced three more post-implementation (a hardcoded "Close" label
  violating Principle IV, a missing fallback for a failed organisation-name fetch, and missing
  `login()` test coverage) — all fixed. First component-level (`jsdom` + React Testing Library)
  tests in `web/`; every prior test there was pure `lib/` logic. One CI-only issue (not caught
  locally): the committed `package-lock.json` was missing a nested `next-intl`/`@swc/helpers`
  resolution that only `npm ci` (not `npm install`) validates strictly — fixed by a clean
  reinstall, pushed as a follow-up commit before merge.
- 009 (`009-child-events`): ✅ Done, merged 2026-07-09 (PR #12, squash-merged after green CI —
  258/258 backend + 88/88 mobile tests). Child event timeline: 11 event types in one JSONB
  table, caregiver-tablet quick-entry (2-tap routine types), temperature push-alerts, same-day
  corrections, daily-summary query. Biggest deviation from plan: FR-006's edit authorization was
  originally clarified as a per-caregiver `StaffLocationEligibility` check, discovered
  mid-implementation to be impossible — device-token-authenticated routine actions carry no
  individual caregiver identity to check eligibility against (constitution's Technology Stack
  Constraints). Corrected to a device-location match instead (research.md R4) — worth
  remembering generally: a clarification answer that assumes an identity a given auth path
  doesn't actually carry needs re-checking against the real auth model, not just implemented as
  agreed. Also added `DeviceOrDirector` (first dual-auth-scheme policy in this codebase — a
  BACKLOG draft had incorrectly assumed `RoomShiftEndpoints` already had precedent for this) and
  `ChildEventTypeExtensions` (multi-word enum wire-string mapping, since the default
  `ToString().ToLowerInvariant()` convention silently drops underscores). `/speckit-converge`
  found two data-model constraints (`EndedAt`/`AdministeredBy` type restrictions) that nothing
  enforced — fixed, not deferred, same as every prior feature's standing rule. A follow-up
  backlog item (`009a`) was logged for a caregiver-requested "custom event type" + a
  `measurement`→`growth_check` rename, raised mid-review and deliberately kept out of this
  feature's scope rather than expanding it mid-flight.
- 009a (`009a-child-events-custom-type`): ✅ Done, merged 2026-07-09 (PR #13, squash-merged after
  green CI — 267/267 backend + 96/96 mobile tests). Adds the `custom` type (`{ label, text? }`)
  and bundles the `measurement`→`growth_check` rename. This backlog item's own prompt flagged two
  genuinely open design questions with no recommended default ("what does `custom` provide over
  `note`?", "bundle the rename or split it?") — per the standing rule about pausing on no-precedent
  scope questions, both were resolved with the user via `AskUserQuestion` before specifying, rather
  than guessed: label+text (not a key/value bag), and bundle the rename into this feature. The
  rename ships as a new `backfill-growth-check` CLI command (mirrors `migrate-tenants`'s tenant-loop
  pattern, feature 002) — a raw per-tenant SQL `UPDATE`, not an EF migration, since no schema
  changes. **Must run against every tenant schema before deploying** the build that drops
  `"measurement"` recognition (hard cutover, no dual-write window) or reads on any un-migrated row
  throw. Two real bugs were caught by the new tests before merge, not assumed away: the backfill's
  first draft used lowercase `event_type`/`id` column names, but this codebase's Postgres columns
  are PascalCase (`"EventType"`, `"Id"`, no snake_case convention configured) — worth remembering
  for any future raw-SQL-against-this-schema work; and `@testing-library/react-native` v14/React 19
  needs `fireEvent.changeText` explicitly wrapped in `await act(async () => ...)`, unlike
  `fireEvent.press` — otherwise the state update never flushes before the next assertion.
  `/speckit-checklist`/`/speckit-analyze` found small gaps (missing offline-sync and
  `EditEventModal` test coverage for `custom`, an orphaned `note.text` i18n key after a
  field-label refactor) — all fixed, not deferred, same standing rule as every prior feature.
- 010 (`010-attendance`): ✅ Done, merged 2026-07-09 (PR #14, squash-merged after green CI —
  309/309 backend + 23/23 web checks; local validation also ran 105/105 mobile tests and web
  typecheck). Daily attendance register: tenant `attendance_records`, caregiver-tablet one-tap
  check-in/out with offline queue registration, absence marking, director/caregiver correction
  rules, director web history/correction UI, `planned_duration_minutes` derivation, and a live
  caregiver BKR indicator sourced from attendance + 008a room-shift roster + 009 sleep events.
  Prompt deltas were made explicit rather than hidden: `recorded_by` follows feature 009 as a
  `uuid[]` checked-in-caregiver set because device-token writes cannot identify a single caregiver;
  closure-day generation remains feature 011 (010 only ships the `closure` status/blocking rule);
  exchange/extra-day request UI remains feature 013 (010 already supports extra-day manual
  check-in with `planned_duration_minutes = null`); and the leefgroep 18-cap is out of scope until
  a group/location type flag exists. Finish pass fixed a real director-web gap (correction dialog
  originally edited only times and kept stale form state; now handles status/absence fields with
  tests) and a CI-only PostgreSQL timestamp precision assertion (`.4072354Z` vs `.4072350Z`).
- 012 (`012-caregiver-scheduling`): ✅ Done, merged 2026-07-10 (PR #16, squash-merged after
  green CI — 359/359 backend + 34/34 web passing). Weekly staff rota (`staff_schedules`):
  director-web week-grid builder, overlap/past-date rules, absence marking, copy-week
  (skipping closure days/existing entries, never overwriting), and a personal-account-scoped
  own-schedule read for feature 027 to consume later — no caregiver-facing UI ships here
  (008a's kiosk tablet has no personal session; explicit scope decision confirmed with the
  user before specifying). Biggest deviation from BACKLOG's own prompt: research during
  planning found the prompt's premise ("BKR uses staff_schedules") false against what
  feature 010 actually shipped — `GetBkrRatioQuery` reads real-time `RoomShift` check-in
  presence, not any schedule, and this feature deliberately does not change that; it ships a
  separate "projected on-duty count" instead, with a dedicated regression test proving the
  two stay decoupled. `/speckit-converge` found a genuine integrity gap the spec missed
  (nothing enforced `StaffLocationEligibility` on schedule create/update, even though
  check-in already does) — fixed, not deferred, same standing rule as every prior feature.
  Worth remembering generally: a live browser verification pass (not just mocked component
  tests) caught a real bug mocked tests couldn't have — `Date.toISOString()` round-tripping
  through UTC shifted the week grid's dates backward by a day in positive-UTC-offset
  timezones.
- 012a (`012a-waiting-list`): ✅ Done, merged 2026-07-10 (PR #17, squash-merged after green CI —
  395/395 backend + 41/41 web passing). This run resumed mid-flight: a prior session had
  already written spec/plan/tasks and most of the `Application` layer (commands/queries) with
  zero commits and no endpoints/migration/tests/web UI — this session picked up from tasks.md's
  checkboxes rather than re-running specify/plan/tasks. Director-web waiting-list tool:
  registration with cross-status duplicate flagging, per-location priority reorder (pointer +
  keyboard), a `waiting → offered → enrolled/withdrawn` allow-list lifecycle with an
  offer-notification email, a forward-looking occupancy view, and a manual child-record
  link/create on enrollment. Same BACKLOG-premise correction pattern as feature 012:
  occupancy was specified to read from attendance (010) + contracts (007), but attendance is
  same-day/historical and doesn't exist for the future dates a waiting-list check needs —
  corrected during specification to read only from active contracts against
  `Location.MaxCapacity`, honoring the closure calendar (011), with a dedicated regression
  test proving it never reads attendance. `/speckit-converge` found three real gaps after
  implementation, all fixed rather than deferred: an EF tracking-query crash from projecting an
  owned-type collection (`ContractedDays`) without `.AsNoTracking()`, a missing
  `MaximumLength(2000)` validator on `Notes` that would have let an over-length value reach
  Postgres as an unhandled 500 instead of a clean 400, and occupancy silently computing for a
  deactivated location instead of being rejected per the spec's own Edge Cases section. Also
  extended `TenantMigrationRolloutTests`' schema-revert helper for the new table's FKs (to both
  `children` and `locations`) — every migration-adding feature since 003 has hit this same
  test and needed the identical fix; worth checking that test first when adding any new
  tenant-schema table with a foreign key.
- 013b (`013b-incident-reports`): ✅ Done, merged 2026-07-12 (PR #23, squash-merged after green
  CI — 533/533 backend + 133/133 mobile + 70/70 web tests passing). This run resumed mid-flight:
  a prior session had already written spec/plan/tasks/data-model/contracts/checklists with zero
  commits and 0/61 tasks implemented — this session picked up from tasks.md's checkboxes rather
  than re-running specify/clarify/plan/tasks. Digital incident/accident report form (Besluit
  Kwaliteit Kinderopvang legal requirement): caregiver-tablet filing with `reportedBy` resolved
  server-side (mirrors feature 009's `child_events.recorded_by`, no PIN step so offline filing is
  never blocked), a 24-hour immutability lock, and a director-web Incidents screen. See
  BACKLOG.md's own shipped-note for the full list of corrected premises (no director push
  channel, no child-file screen yet) and fixed CI-only issues (web lockfile out of sync under
  Node 20, flaky Postgres timestamp-precision equality) — same two classes of bug 007a's and
  010's shipped-notes already describe, worth checking for on every feature that touches
  `web/package-lock.json` or compares a just-saved timestamp against a re-queried one.
- 013c (`013c-vaccine-health-records`): ✅ Done, merged 2026-07-13 (PR #24, squash-merged after
  green CI — 563/563 backend + 84/84 web + 140/140 mobile passing). Structured vaccine records
  and categorized health records, a director-web due-soon dashboard, and a caregiver read-only
  summary extension. Found a genuinely new, no-precedent scope question only during planning
  research (not raised by the BACKLOG prompt itself): an unused `vaccination_records` table from
  feature 006 overlapped this feature's new schema — paused and confirmed with the user directly
  before migrating it (data backfill in the EF migration) rather than guessing. Also built this
  codebase's first per-child detail screen and first dashboard-shaped screen, since the feature's
  own due-soon block needed both to exist to be reachable — worth remembering that a feature
  whose spec only lightly implies a screen (BACKLOG's prompt just said "director dashboard
  block") can still require building the whole screen category from scratch if none exists yet,
  same lesson 007a and 013b already logged for `/children`. `/speckit-converge` found one real
  gap: the mobile offline-cache-fallback service for the new caregiver summary had no test
  exercising its actual cache logic (only the screen mocked past it) — fixed with a test
  mirroring the group view's existing cache-fallback test.
- 006a (`006a-child-profile-ui`): ✅ Done, merged 2026-07-13 (PR #25, squash-merged after green
  CI — 567/567 backend + 92/92 web + 144/144 mobile passing). This run resumed mid-flight: a
  prior session had already fully implemented, design-reviewed, and converged the feature (all
  38 tasks checked off, PR open with green CI) — this invocation found the in-progress branch
  per step 0, verified nothing was left outstanding, and only needed to merge and update
  BACKLOG.md's status. Director-web "Profiel" tab on `/children/[id]` (create + edit, alongside
  013c's "Gezondheid" tab), a new `PediatricianName`/`PediatricianPhone` pair on `Child` distinct
  from the existing GP fields, and a caregiver-tablet read-only extension of the existing medical
  summary. Same two recurring bug classes prior features' shipped-notes already flagged: a
  migration-adding feature needed the `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests`
  revert-helper fix (012a, 013c), and the web lockfile needed regenerating for a clean `npm ci`
  under Node 20 (007a, 010, 013b).
