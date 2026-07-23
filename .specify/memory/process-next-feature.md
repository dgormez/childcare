# Process Next Feature

This is the exact prompt used to process one `BACKLOG.md` feature through the full SpecKit
pipeline (specify → clarify → plan → tasks → checklist → analyze → implement → converge → PR
→ merge). It's saved here so a fresh session (or a teammate) can resume without retyping it.

**This now runs unattended on a schedule.** A cron trigger invokes this prompt every 3 hours;
each invocation processes one feature (or resumes whatever is in progress) to completion, then
the turn ends — the next scheduled trigger picks up the next backlog item. This is a deliberate
reversal of the prior single-pass-only design (see "History" below): the user has explicitly
authorized full autonomy for this pipeline, including unattended `gh pr merge` to `master`,
across scheduled runs with no human checkpoint in between.

## History

Earlier versions of this prompt ran in `/loop`'s dynamic self-paced mode, then were changed to
single-pass only (refusing `ScheduleWakeup`) over concern about unattended `gh pr merge` with no
human checkpoint. That concern still applies in general — this pipeline can pick a government/
compliance-regulated backlog item and merge to master with no review — but the user has weighed
that risk and explicitly asked (2026-07-16) for this specific pipeline to run fully unattended
every 3h regardless, including through the merge step. The "pause and ask" standing rule below
for genuinely novel scope questions still applies and is the remaining human checkpoint for
anything this loop can't resolve from precedent — it should ask more readily, not less, given
the reduced supervision.

## Standing process rules (apply regardless of how this is invoked)

- **This rule applies to every wait in this pipeline, not just the test suite — test suite, CI
  checks, builds, deployments, or anything else that finishes asynchronously.** Never call Bash
  with `run_in_background: true` and never end the turn assuming you'll be notified, re-invoked,
  or able to "check back" when the thing finishes — always wait on it with a **blocking
  foreground call in the same turn** (e.g. `gh pr checks <N> --watch` for CI, not a bare
  `gh pr checks` plus a plan to look again later). This is not a style preference; it will
  silently break the run. Backgrounding relies on a later turn in the same conversation to
  receive the completion signal and act on it. A scheduled invocation of this prompt runs as a
  one-shot headless `claude -p` process with exactly one turn — there is no later turn for any
  notification to arrive in. If you background something and end your turn expecting to "check
  back" or "pick this up automatically," the process exits immediately, nothing is watching
  anymore, and any work not yet committed/pushed/merged is simply abandoned. (Established
  2026-07-16 after this happened on feature 016's test suite step, patched with a rule scoped
  only to that step; recurred 2026-07-17 in the very next step — waiting on CI checks after the
  PR was opened — because the rule was written narrowly around "test suite" instead of the
  general pattern. Written generally this time; if a third async-wait spot turns up, that's a
  sign the pattern needs enforcing structurally, not with another one-off callout.)
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
- **✅ Done features are immutable in BACKLOG.md.** Never edit a shipped feature's prompt
  block, row description, or scope — new needs become a new letter-suffixed follow-up feature
  (precedent: 014's payment-link/reminders scope became `014a` when 014 shipped mid-research on
  2026-07-15). Shipped-notes under a done block may still be appended (they're history, not
  scope).
- **Government/compliance features carry verified regulatory contracts — don't re-derive or
  invent.** Features 015, 019, 033–041 (and any future Opgroeien/FOD work) have prompt blocks
  whose regulatory facts were verified 2026-07-15 against official documents archived in
  `docs/integrations/opgroeien/` (XSDs, the AARON Swagger JSON, official .docx models, the
  kindratio special — see that folder's README.md for the file→feature map and source URLs).
  During specify/plan for those features: cite the archived file, not memory. Several of those
  prompt blocks also contain an explicit "Open questions (do NOT invent)" section — treat every
  item there as the pause-and-ask category above (they need the product owner or
  software-ontwikkeling@kindengezin.be, not a guessed default). Two recurring examples: the PSP
  choice in 014a, and AARON production-token onboarding in 033.

## The prompt

```text
Process the next eligible feature from the ChildCare SpecKit backlog. Do exactly one feature (or
resume whatever is already in progress), then STOP — do not pick up another backlog item within
this same invocation. When you finish — or reach a clean stopping point (blocked question,
failing build after retries, red CI) — report status and end the turn. This prompt is triggered
externally on a 3-hour cron schedule, so the next backlog item is picked up automatically by the
next scheduled run, not by a manual re-invocation. Full pipeline autonomy is authorized for
scheduled runs, including `git push` and `gh pr merge` without pausing for confirmation — the
only pauses are the "pause and ask" cases the standing rules above define (genuinely novel
scope questions, government/compliance open questions marked "do NOT invent").

Before starting, read these — they define UX, visual, and platform constraints, and every
spec/implementation this loop produces must follow them:
- design-system.md
- platform-rules.md
- reference-products.md
- design-decisions.md
- workflows.md

Additionally, if the picked feature is one of 015, 019, 033–041 or 014a (government
integration / Flemish regulatory compliance / PSP payments): read
docs/integrations/opgroeien/README.md first and open the specific source files it maps to the
feature (XSD / Swagger JSON / official .docx model / PDF). The prompt blocks embed verified
contract facts and explicit "do NOT invent" open questions — resolve those questions with the
product owner before /speckit-plan, and cite the archived source files in the spec.

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

9. Build and run the test suite as a normal blocking Bash call — **do not pass
   `run_in_background: true`**, even though it's slow. Wait for the actual tool result in this
   same turn before continuing; do not end the turn early expecting to resume when a backgrounded
   run finishes (see the standing rule above — there is no later turn in a scheduled invocation).
   If something fails, attempt fixes up to ~3 times. If still failing: stop, leave the branch
   pushed, report what's broken. Never merge broken code.

10. Commit as you go with real messages. Push the branch.

11. gh pr create targeting master.

12. Wait for gh pr checks to go green using a **blocking foreground call**
    (`gh pr checks <N> --watch`, which polls internally and only returns once checks finish) —
    do not end the turn assuming you'll be notified or re-invoked when CI completes; nothing
    will re-invoke you within this run (see the standing rule above — it applies to any
    asynchronous wait, not just the test suite). Red CI is handled like step 9 — fix, retry, or
    stop and report. Never merge on red.

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
- 013d (`013d-meal-list`): ✅ Done, merged 2026-07-13 (PR #26, squash-merged after green CI —
  582/582 backend + 99/99 web + 154/154 mobile passing). Daily meal-list aggregation
  (`child_meal_preferences` table, one `GET /locations/{id}/meal-list` read model, director-web
  printable page, caregiver-tablet own-group view). Two BACKLOG-prompt premises were wrong and
  corrected rather than assumed: allergen severity comes from `Child.AllergySeverity` (006), not
  `HealthRecord` (013c) — that entity has no severity field; and "present" needed an explicit
  `CheckOutAt == null` check alongside `Status == Present`, since `CheckOutCommand` (010) never
  actually changes `Status` away from `Present` — caught by writing this feature's own tests, not
  by inspection, and worth remembering generally whenever a future feature reads
  `AttendanceRecord.Status` to mean "currently present." Confirms the recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern (012a,
  013c, 006a) applies to *any* test that reverts a tenant schema to an earlier migration state,
  not just the one file usually named — this feature needed the fix in
  `LegacyVaccinationMigrationTests` specifically, whose own code comment predicted exactly this
  gap for "any future migration." `/speckit-checklist`'s safety-focused pass found six genuine
  spec gaps (inclusive date-boundary handling for standing medication, single-icon-not-count for
  multiple medication records, among others) — all fixed in spec.md, not deferred, same standing
  rule as every prior feature.
- 013g (`013g-vaccine-catalog`): ✅ Done, merged 2026-07-13 (PR #28, squash-merged after green CI
  — 609/609 backend + 108/108 web passing). Shared, platform-wide vaccine catalog (`vaccine_types`,
  seeded from the Vlaamse basisvaccinatieschema) backing 013c's free-text `vaccineName`, a
  per-tenant "remembered custom entry" mechanism so a non-catalog name is only ever typed once per
  KDV, and attachment support on `VaccineRecord` reusing 013c's signed-URL health-attachment
  infrastructure under a distinct object-path prefix. `vaccine_types` lives in `PublicDbContext` —
  the first table there that's shared reference data rather than a tenant-management record;
  confirmed this doesn't weaken tenant isolation (Principle I) since it carries no tenant/personal
  data. `VaccineRecord.VaccineTypeId` deliberately has no DB-level FK across the schema boundary
  (the catalog is soft-delete-only, so the one failure mode a FK guards against can't occur) —
  worth remembering as a general pattern for any future cross-schema reference to genuinely
  immutable-by-deletion reference data. This feature's own prompt raised a real, no-precedent scope
  question (who manages the shared catalog, given no platform-admin role exists anywhere in this
  codebase) — resolved directly with the product owner before specifying rather than guessed:
  platform-operator-managed only, no director write path, and a new BACKLOG item (`013h`) logged
  for the deferred platform-admin management UI. `/speckit-checklist`'s safety/tenant-isolation/
  data-integrity pass tightened several FRs for precision (mutual-exclusivity between a catalog and
  custom-entry reference, diacritic-insensitive dedupe — the original BACKLOG example only
  mentioned case/whitespace, missing that "Rabiës" vs "rabies" is actually an accent difference)
  and added a DB `CHECK` constraint enforcing the mutual-exclusivity rule rather than leaving it
  application-layer-only. `/speckit-analyze` found one real coverage gap (FR-014's caregiver-summary
  decoupling had zero tasks) — fixed with a regression test, same standing rule as every prior
  feature. Confirms the recurring `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests`
  revert-helper pattern (012a, 013c, 006a, 013d) yet again — but this time a *new* variant of the
  same mistake slipped through code review and was only caught by actually running the full test
  suite: the two tables were dropped in the wrong order relative to their own FK (the referencing
  table must drop before the table it references, not after) — worth double-checking FK direction,
  not just presence, next time this pattern comes up. `/speckit-converge` found one real gap: the
  spec's own Assumptions section promised logging the platform-admin follow-up as a new BACKLOG
  item, which hadn't actually been done until the converge pass caught it.
- 013h (`013h-platform-admin-vaccine-catalog`): ✅ Done, merged 2026-07-14 (PR #29, squash-merged
  after green CI — 637/637 backend + 118/118 web tests). Resumed mid-flight: a prior session had
  already written spec/plan/tasks/data-model/contracts/checklists and Foundational-phase code
  (`IsPlatformAdmin` flag, JWT claim, `PlatformAdminOnly` policy, `VaccineType` audit columns,
  `grant-platform-admin` CLI) with zero commits and a build-breaking bug (a raw-string
  interpolation mixed with an ADO parameter placeholder in `GrantPlatformAdminCommand`) — fixed,
  then this session implemented all three user stories end-to-end (create/list, rename/reorder,
  deactivate/reactivate), backend + director-web. Also exposed `IsPlatformAdmin` on
  `AuthenticatedUser` (login/Google/Apple/refresh) since this web app never decodes the JWT
  client-side — every existing screen gates purely on session presence, so the sidebar's
  platform-admin nav entry needed a response field, not a token claim, to key off. Corrected the
  contract's originally-written `400` to `422` for validation failures, matching this codebase's
  actual `ValidationBehavior` pipeline convention. `/speckit-analyze`/`/speckit-converge` found
  and fixed two real gaps: the management table was missing FR-012's display-order column, and no
  test proved a token carrying `is_platform_admin` without the `director` role is rejected
  (FR-009) — both fixed. A full-suite run also surfaced two pre-existing, unrelated failures:
  hardcoded `2026-07-13`/`14` dates in `ClosureCalendarTests`/`DayReservationEndpointsTests`/
  `ReservationSettingsEnforcementTests` had expired against today's actual date (fixed by bumping
  forward with a wide buffer, plus deriving the remaining year/range literals from the constants
  instead of drifting independently again), and `LegacyVaccinationMigrationTests`' revert helper
  needed extending for this feature's own tenant-schema migration — confirms the recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` pattern (012a, 013c, 006a, 013d,
  013g) yet again. T049 (granting the real `dgormez@gmail.com` production account) is a manual
  post-merge step — no production DB access from this session, and per this codebase's convention
  that production data changes are run manually, not autonomously.
- 2026-07-15: research pass (not a feature run) — full opgroeien.be crawl, official documents
  supplied by the product owner (XSDs, AARON Swagger JSON, kindratio special, meldingsfiche,
  document models), and a bitcare.com/D-Care competitor scan. BACKLOG.md gained features
  033–041 + 014a (government reporting, safety/compliance, retention, migration/onboarding,
  occupancy planning, BKR-2027 versioning, invoice payments) with verified contract facts
  embedded in their prompt blocks; PROJECT-BRIEF.md gained the integration-surface table,
  expanded regulatory context and competitor snapshot; the source documents were committed
  under `docs/integrations/opgroeien/` (see its README.md); constitution amended to 1.4.0
  (Principle II: regulation is time-versioned — 2027 kindratio). 014 shipped the same day this
  research amended it, so its additions were extracted into `014a` and 014's block restored —
  origin of the "done features are immutable" standing rule above. A broader corpus of ~60
  markdown-converted official documents lives OUTSIDE the repo in the product owner's
  `Claude-markdown` folder (Belcotax/BOW set, verwerkersovereenkomst templates, subsidy
  procedures) — BACKLOG.md's Notes section indexes it; ask the product owner for a file if a
  spec needs one that isn't in docs/integrations/opgroeien/.
- 014 (`014-invoicing`): ✅ Done, merged 2026-07-15 (PR #33, squash-merged after green CI —
  716/716 backend + 175/175 web + 79/79 parent-mobile tests). Resumed mid-flight: backend
  (US1-US4) and most of director-web were already implemented in a prior session with zero
  tests for the web layer and the parent-mobile side entirely unbuilt; this session verified the
  existing work (ran the full backend suite before touching anything), wrote the missing web
  component tests (T038-T040, T052, T067), and built parent-mobile's invoices feature from
  scratch (list, detail, PDF download) — the first PDF download in either mobile app, since no
  prior precedent existed anywhere in the monorepo for it (director-web's blob-download approach
  has no React Native equivalent); added `expo-file-system`/`expo-sharing` and extended
  `jest.config.js`'s transform allowlist for both. `/speckit-converge` found and fixed two real
  gaps after implementation: FR-020's nullable IKT-subsidy placeholder field was never added to
  `InvoiceLineItems` anywhere, and FR-015's backend status filter (`ListInvoicesQuery`'s `status`
  param) was fully implemented but never exposed in the director-web invoice list UI — both
  fixed, not deferred, same standing rule as every prior feature. The polish-phase accessibility
  pass (T068) also caught a real inconsistency: `InvoiceTable` only made the child-name cell
  clickable via a nested `Link`, unlike every sibling table's full-row `onClick`+`router.push`
  pattern (`LocationsTable`, `IncidentReportsTable`) — switched to match. CI caught one flaky
  test neither local run reproduced: `RegenerateInvoiceTests`' `Assert.Equal(sent.SentAt,
  regenerated.SentAt)` compared an in-memory `DateTime` against one round-tripped through
  PostgreSQL's `timestamptz` column, differing by a few ticks — fixed with the same
  millisecond-tolerant comparison this codebase's `IncidentReportImmutabilityTests`/
  `DeactivateVaccineTypeTests` already established for exactly this precision class.
- 014a (`014a-invoice-payments-plus`): ✅ Done, merged 2026-07-16 (PR #34, squash-merged after
  green CI — 741/741 backend + 182/182 web + 85/85 parent-mobile tests). Full pipeline run from
  a clean `master` in a single session: the PSP decision (Mollie Connect for Platforms) was
  already resolved in BACKLOG.md's prompt block from a prior product-owner conversation, so no
  pause was needed there. `IPaymentProvider` mirrors `IExpoPushSender`'s existing port/adapter
  shape; two new public-schema tables (`payment_provider_connections`, `payments`) resolve the
  public webhook to a tenant/invoice from a system-generated `PaymentReference` alone, never a
  client-supplied claim; OAuth tokens are the first per-tenant third-party credential this
  codebase stores, encrypted via ASP.NET Core Data Protection. `send-payment-reminders` (a new
  CLI subcommand, mirrors `MigrateTenantsCommand`'s per-tenant loop) is this codebase's first
  scheduled-job entrypoint — two prior features (008a, 014 itself) had each deferred building
  this exact infrastructure with a note to "revisit... and fold this in then"; this was that
  feature. Corrected one BACKLOG-prompt-adjacent assumption during planning: the spec's first
  draft assumed the betalingsbewijs needed a stored/signed-URL pattern, but 014's own invoice
  PDF turned out to already be on-demand-rendered (research.md R1) — fixed before implementation
  rather than carried through. `/speckit-checklist`'s security/regulatory pass found five real
  spec gaps (token-expiry edge case, reminder-cap persistence across settings changes,
  reminder-progress across regenerate, receipt PII scope, per-tenant job failure isolation) —
  all fixed in spec.md. `/speckit-converge` found four real implementation gaps the first pass
  missed: a revoked Mollie token propagated as an unhandled 500 instead of the spec's required
  graceful reconnect state (the connection now flips to `Disconnected` and the parent sees a
  clean not-available state, not a broken link); no test exercised `FakeExpoPushSender` despite
  two new notification types; the reminder job's per-tenant failure isolation existed in code
  (same try/catch shape as `MigrateTenantsCommand`) but was never tested — worth remembering
  generally: a comment claiming something "isn't testable" is itself a claim to verify, not
  assume, especially when a near-identical existing test (`BackfillGrowthCheckCommandTests`)
  already proves the pattern is testable; and the concurrent-payment race (online payment vs.
  manual mark-paid at the same instant) had a correctness guard but no test. Writing those tests
  surfaced two test-isolation bugs of this session's own making, both fixed: `FakePaymentProvider`
  is registered Singleton (mirroring `FakeExpoPushSender`'s established pattern) and so its
  `Payments` dictionary accumulates across every test in a class sharing one `IClassFixture` —
  assertions must scope by `InvoiceId`, not assume a fresh/empty collection; and a deliberately
  broken tenant schema (used to prove failure isolation) must be cleaned up afterward
  (`ProvisioningStatus = Failed`) or it permanently breaks every later test in the same shared
  fixture that calls the same cross-tenant command. Confirms the recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern (012a,
  013c, 006a, 013d, 013g, 013h, 014) applies yet again, with a reminder of the two files' actual
  difference: `TenantMigrationRolloutTests` drops `locations` wholesale so new `Location` columns
  need no separate step there, but `LegacyVaccinationMigrationTests` never drops `locations` and
  needed explicit `DROP COLUMN`s for this feature's three new fields. The Terraform for the new
  Cloud Scheduler + Cloud Run Job (`infra/gcp/payment-reminders-scheduler.tf`) was authored but
  deliberately not applied, and real Mollie OAuth credentials were not provisioned — both are
  manual post-merge steps, per this loop's standing infra-caution rule.
- 015 (`015-fiscal-attestations`): ✅ Done, merged 2026-07-16 (PR #35, squash-merged after green
  CI — 767/767 backend + 187/187 web + 92/92 parent-mobile tests). Full pipeline run from a clean
  `master` in a single session, scheduled via a one-shot session-local cron. Annual Belgian fiscal
  attestation PDFs (childcare cost certificates) per (child, location, tax year), aggregated from
  that year's `Paid` invoices into up to 4 daily-rate periods (>4 consolidates the oldest overflow
  into the earliest retained one); director-web bulk-generate/regenerate; parent-mobile
  list+download via a signed GCS URL. Deliberate, explicitly-reasoned departure from 014/014a's
  on-demand PDF rendering: the attestation is rendered once and persisted to GCS (mirrors
  `GcsGroupActivityPhotoStorage`'s server-side-write pattern, not the client-signed-upload one),
  since a document a parent files with the tax authority needs a stable snapshot — regenerate
  explicitly re-renders and overwrites in place rather than silently drifting if unrelated invoice
  data changes later. `/speckit-clarify` self-answered its one real ambiguity (does
  generate/regenerate notify parents?) against clear 014/014a precedent, per this loop's standing
  rule for autonomous runs with no product owner present. `/speckit-checklist`'s safety/compliance
  pass and `/speckit-analyze` each found one real gap (FR-004's ambiguous "day count" needing to
  specify billable vs. calendar days; SC-004's unquantified "a few taps") — both fixed in spec.md.
  `/speckit-converge` found two real test-coverage gaps on explicitly-named acceptance scenarios
  (US1/AC4 "child left mid-year", US3/AC3 "parent sees the corrected version, not stale") — both
  fixed with dedicated tests, not left as debt. Found and fixed a real pre-existing bug while
  extending shared code: `NotificationType`'s union and the parent-mobile notifications screen's
  icon/nav map were never updated for 014/014a's `invoicesent`/`paymentreminder`/`invoicepaid`
  types, so any parent receiving one hit an undefined icon component — extended for those three
  plus this feature's own type, with regression tests. Confirms the recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern (012a,
  013c, 006a, 013d, 013g, 013h, 014, 014a) yet again — and this time the second file's *own* doc
  comment already explained exactly why it would break (`MigrateAsync()`'s computed "pending"
  range goes empty if the newest migration is left marked-applied while an earlier one is
  reverted), yet it was still missed on the first pass and only caught by actually running the
  full suite before opening the PR, not by inspection. CI then caught a second, different issue
  the full local run didn't reproduce: three `GeneratedAt` equality assertions hit the same
  PostgreSQL `timestamptz` round-trip precision flake `RegenerateInvoiceTests` (014) had already
  established a millisecond-tolerant comparison pattern for — applied the same fix. Real
  regulatory/legal content (the Opgroeien declaration wording, exact PDF layout) is sourced from
  the official template at implementation time per spec.md's own Assumptions, not fabricated.
- 2026-07-16: prompt reworked from single-pass-only back to scheduled/unattended (see "History"
  above) — the user explicitly requested a 3-hour cron trigger, including unattended `gh pr
  merge` to master, reversing the 2026-07-07 single-pass change. The "pause and ask" standing
  rule is now this pipeline's only remaining human checkpoint; watch progress-log entries below
  for whether that rule is actually catching novel scope/compliance questions under reduced
  supervision, or silently guessing instead.
- 2026-07-16: first scheduled cron run (016-developmental-milestones) fully implemented the
  feature — backend entities/migrations/endpoints, mobile/web/parent-mobile UI, tests, dozens of
  files — but ended the turn deferring to a backgrounded test suite it assumed it could "check
  back on later," which doesn't exist in a one-shot headless `claude -p` invocation: the process
  exited, nothing was committed/pushed, no PR opened. Everything was still intact on disk on the
  branch (nothing lost), and the next scheduled run's step 0 can resume it, but the run itself
  never converged. Added the "never background a long-running command" standing rule above and
  made step 9 explicit about running the test suite synchronously, to prevent this recurring
  every 3h indefinitely without ever committing.
- 2026-07-17: the 2026-07-16 fix above didn't hold — a later scheduled run (still on 016) hit the
  identical bug: backgrounded the test suite and ended the turn expecting to "pick this back up
  automatically once it completes." The softer "run synchronously, don't background it" wording
  wasn't strong enough to override backgrounding a slow command, which is normally correct
  practice in an interactive session. Reworded both the standing rule and step 9 to name the
  actual mechanism explicitly — forbidding Bash's `run_in_background: true` by name for this
  pipeline — rather than describing the desired behavior abstractly. Also worth noting for
  anyone tuning the 3h interval: one run in this window was wiped out entirely by hitting the
  account's session usage limit (a single heavy run can exhaust it), so not every scheduled tick
  is guaranteed to do real work.
- 2026-07-17: the run_in_background fix held for step 9 — a subsequent run implemented 016 in
  full (789 backend + 162 mobile + 96 parent-mobile + 193 web tests passing), committed in 3
  logical commits, pushed, and opened PR #36. But the identical pattern recurred one step later:
  it ended the turn waiting on CI checks, saying it would "be notified automatically" with a
  "20-minute fallback check scheduled" — that mechanism doesn't exist here either. Lower-stakes
  than the step-9 recurrence since the work was already committed/pushed/PR'd (nothing to lose),
  and a later run picked the open PR back up per step 0 and completed the merge — PR #36 merged
  2026-07-17T09:34:44Z, 016 done. Rewrote the standing rule to be general (any async wait, not
  just "test suite") and step 12 to require `gh pr checks --watch` as a blocking foreground call,
  rather than patching this one spot narrowly again.
- 2026-07-17: cron interval changed from 3h to 4h at the user's request.
- 2026-07-19: feature 017 (MeMoQ) UNBLOCKED — the 2026-07-17 run's pause (no official
  instrument content in the repo, correctly refused to fabricate) is resolved. The product owner
  supplied the full official document set; the instrument sources now live in `docs/memoq/`
  (handleiding, six groepsopvang dimension forms, Zorginspectie monitoring note, pedagogisch
  raamwerk — markdown conversions; original PDFs remain with the product owner). BACKLOG's 017
  prompt block was fully rewritten from the verified instrument (the old block's dimension names
  and director-annual-form model were wrong — do not resurrect them from git history) and four
  product decisions were confirmed via AskUserQuestion: structured-but-all-optional (no
  compliance wizard), director-web full cycle + caregiver-tablet observation capture,
  participant-private-until-shared statement ratings, observation child-rows detached from child
  records. ONE open question remains in the block — content-licensing permission from Opgroeien
  to embed the statement sets verbatim — with a defined fallback (scaffolding + user-entered
  content) so it is a clarify-phase item, not a blocker; per the standing rule it goes to the
  product owner/Opgroeien, not guessed. Note for the run that picks 017 up: read `docs/memoq/`
  before /speckit-specify, same pattern as docs/integrations/opgroeien/ for 033–041.
- 2026-07-19: features 042–049 added (product-owner request after a pure-childcare gap
  analysis): settling-in/wenperiode planning, medication authorisations, day-specific pickup
  authorisations, activity planning, parent survey (ouderbevraging), rustmoment sleep checks,
  supplies requests, message auto-translation. Evacuation-drill logging was folded into 035's
  scope rather than a separate feature. Permission slips for uitstapjes were explicitly
  REJECTED by the product owner (KDV babies don't go on outings) — don't re-propose. Several
  blocks contain spec-time verification pointers (wennen regulatory framing, medication legal
  basis — check the huishoudelijk reglement model) and 049 has a provider/GDPR decision — same
  do-NOT-invent handling as the government features.
- 030 (`030-family-siblings`): ✅ Done, merged 2026-07-19 (PR #39, squash-merged after green CI —
  871/871 backend + 105/105 parent-mobile + 213/213 web tests). This run resumed mid-flight: a
  prior session had already fully implemented all five user stories (all 65 tasks checked off,
  no open PR) — this invocation found the in-progress branch per step 0 and ran `/speckit-converge`
  before proceeding, since no converge pass had been recorded. It found two real gaps, both fixed
  rather than deferred: (1) the full-price sibling tie-break (`GenerateInvoicesCommand`) had no
  deterministic secondary sort for two contracts sharing the exact same start date, despite
  spec.md's own Assumptions section requiring one (`Contract.CreatedAt`, earliest wins); (2) a
  much bigger one — FR-009a/Clarifications promise "one payment action covers the whole bundled
  group... including via a 014a PSP payment link," but the shipped code made that path
  categorically unreachable (`FamilyInvoiceChildLineResponse` carried no `InvoiceId`, and
  `invoices/index.tsx`'s own comment recorded the missing navigation as a deliberate choice) and,
  independently, `ProcessPaymentWebhookCommand` never cascaded Paid status to sibling invoices the
  way `MarkInvoicePaidCommand` already did — worth remembering generally: fixing a webhook/manual
  path parity gap like that without also checking the *amount* charged would have been worse than
  not fixing it at all, since `CreatePaymentLinkCommand` was charging only the one targeted
  invoice's share, not the bundle's combined total, so the cascade alone would have marked a whole
  family's invoices Paid for less money than they owed. Fixed all three (InvoiceId exposure,
  correct combined-amount charging, webhook cascade) plus added a Pay action to parent-mobile's
  family invoice tile, with backend and mobile regression tests for each. Also worth logging: this
  run hit a self-inflicted git accident mid-session (a stray `git checkout master -- .` while
  investigating an unrelated file, followed by a conflicted `git stash pop`) that briefly
  overwrote working-tree files with master's content — recovered cleanly since the stash is kept
  on a failed pop and nothing was committed yet; `git reset --hard HEAD` followed by a clean
  `git stash apply` restored exactly the intended session diff, verified via `git diff --stat`
  before re-committing. No damage occurred, but worth remembering: never run `git checkout
  <other-branch> -- .` on a feature branch to inspect something — use `git show
  <branch>:<path>` (read-only) instead.
- 031 (`031-photo-lifecycle-governance`): ✅ Done, merged 2026-07-20 (PR #40, squash-merged after
  green CI — 891/891 backend + 213/213 web + 108/108 parent-mobile tests). Resumed mid-flight: a
  prior session had already implemented and committed all of US1 (GDPR purge), US3 (archive/cost-
  tiering job + Terraform lifecycle rule), and US4 (staff/director RBAC parity), plus US2's backend
  route, but left tasks.md entirely unchecked and US2's parent-mobile UI (download-original action,
  offline-aware) half-built on disk with zero test coverage and no commit. This session verified
  every foundational/US1/US3/US4 task actually existed in code (not just trusted the commit
  messages), finished US2's UI (gallery detail view, download+share, `useIsOffline` hook, i18n),
  and wrote the test coverage that was missing for it (open-detail, download+share, download-
  failure toast, offline-hides-action — none of these existed before this pass). Running the full
  suite surfaced a real regression: `ChildHealthSummaryReadOnlyTests.Caregiver_CannotCreateVaccine
  OrHealthRecord` asserted the exact pre-US4 policy this feature deliberately reverses (staff can
  now create/edit/delete health/vaccine records at their assigned location) — fixed by removing the
  obsolete assertion and its now-unused helpers, keeping the still-valid device-token restriction;
  `PhotoRbacParityTests` (already written by the prior session) fully covers the corrected staff-
  allowed/staff-denied-by-location behavior, so nothing was left untested. T044 (real-GCS
  quickstart scenarios) and T046 (a real `terraform plan`) were left as explicit manual post-merge
  follow-ups — no GCP credentials in this session, same constraint as 013h's T049 and 014a's
  un-applied Terraform; `terraform validate` passed as the syntax-level substitute, and the
  scenarios that don't need a real bucket (RBAC, parent download, GDPR purge) are already covered
  by TestContainers tests. Also worth noting: this run hit the tool harness auto-backgrounding a
  command after its 120s default timeout (both `dotnet test` and `gh pr checks --watch`) — distinct
  from the standing rule's warned-against failure mode (deliberately passing
  `run_in_background: true` and walking away). Here the fix was to actively watch the
  already-backgrounded task to completion via Monitor in the same turn, rather than treat the
  auto-backgrounding as a signal to stop waiting.
- 023 (`023-digital-enrollment`): ✅ Done, merged 2026-07-21 (PR #43, squash-merged after green CI
  — 965/965 backend + 228/228 web tests). Resumed mid-flight: a prior session had already fully
  implemented all four user stories (68/68 tasks checked, including a design-compliance pass
  T063-T066 sitting uncommitted on disk) with zero commits since the last checkpoint — this
  session verified and committed that work first, then ran `/speckit-converge` (not yet recorded
  for this feature). It found three real testing/UX gaps against spec.md's own explicit Testing
  Requirements and UX guidance, all fixed rather than deferred: the public-enrollment rate-limit
  policy (3/IP/rolling hour) had only ever been tested structurally (attribute metadata), never
  behaviorally, since `AddRateLimiter` is deliberately disabled in the Testing environment
  codebase-wide (an established, precedented convention — confirmed by checking
  `AuthSessionLifecycleTests`'s identical pattern before treating it as a gap) — fixed by
  extracting the policy's options into a standalone `RateLimiterPolicies.PublicEnrollment` class
  so a unit test can exercise the real `SlidingWindowRateLimiter` directly without touching that
  convention; tour-invitation accept/decline tokens never expired despite spec.md explicitly
  listing "valid, expired, tampered" as required test cases (worth noting: the code comment's
  claim of "mirrors IUnsubscribeTokenService" doesn't actually mean time-limited — that service
  doesn't expire either, deliberately, since a permanent unsubscribe link is a different tradeoff
  than a time-bound tour-invitation link; verified the precedent's actual behavior before treating
  the comment's wording as settled fact) — fixed with `ToTimeLimitedDataProtector` (added the
  `Microsoft.AspNetCore.DataProtection.Extensions` package) and an expired-token test; and the
  public form's submit button only dimmed via opacity while submitting — fixed with a spinner +
  localized "Submitting…" label in all three locales. This run also produced this pipeline's own
  cautionary tale, self-inflicted: while investigating a web `tsc --noEmit` failure, ran `git
  checkout master -- .` on the feature branch to compare against master — the exact mistake
  030's shipped-note above already named and warned against by file path — which overwrote the
  entire working tree (all uncommitted converge fixes) with master's content. Recovered cleanly
  with no work lost: the fixes were already `git stash`-ed before the checkout (habit, not luck,
  from following the pre-destructive-command `git status`-first rule), so `git checkout HEAD --
  .` restored the tree to the branch's last commit and `git stash pop` reapplied the stash
  cleanly. The `tsc` failure itself turned out to be pre-existing, unrelated tech debt on an
  untouched file (a DOM-global-`Location`-vs-test-mock typing issue) that isn't even part of
  CI's actual web gate (`Web tests` only runs `npm test`, no typecheck step) — not this feature's
  regression, correctly left unfixed rather than scope-creeped in.
- 024 (`024-esignature`): ✅ Done, merged 2026-07-22 (PR #44, squash-merged after green CI — 986/986
  backend + 240/240 web tests). Resumed mid-flight: a prior session had already fully implemented
  all four user stories (51/51 tasks checked) and applied its own `/speckit-converge` findings
  (signed-PDF-download endpoint, org-wide contracts-list endpoint, an atomic-signing bug that
  could permanently mark a contract signed with no PDF on a storage failure, FR-015 coverage,
  generated production SQL scripts, the recurring tenant-migration-rollout revert-helper fix) —
  all committed, nothing left to implement. This session only needed to run the test suite,
  push, PR, and merge. Signing is additive to the existing Draft→Active lifecycle (007) rather
  than a precondition for it — confirmed against 010/012a/014's existing assumptions before
  merging, per this feature's own spec.md Clarifications. The organisation's SEPA Creditor
  Identifier (CID) is configured once at the org level (mirrors `Tenant.KboNumber`, 014), not
  generated per signing — only the per-mandate reference is generated per signing; worth
  remembering as a concrete example of a BACKLOG prompt conflating two distinct concepts that
  needed correcting before implementation, not during it. Running the full backend suite before
  pushing surfaced a real, deterministic, date-dependent bug unrelated to this feature:
  `BulkDayReservationTests.Submit_SiblingsAtDifferentLocations_EachEvaluatesOwnLocationNoticeHours`
  computes its contract date as a plain `AddDays(3)` with no weekend check, but
  `CreateContractCommandValidator` only accepts Monday–Friday as contracted days — whenever
  "today + 3 days" lands on a weekend (as it did today, 2026-07-22 + 3 = Saturday), contract
  creation fails validation and the test's own downstream assertion reports a misleading 404 on
  activation. Confirmed present on a clean `master` before this branch touched anything (same
  failure, same date), so unlike 023's precedent (an unrelated `tsc` issue that wasn't part of
  CI's actual gate and was correctly left unfixed) this one WAS part of the CI gate and would
  have blocked this PR's merge on an unrelated red check — fixed by skipping forward to the next
  weekday instead of a raw offset, rather than left as debt. Worth remembering as a new variant
  of the recurring "date-dependent test breaks on the day it happens to run" class 013h's and
  023's shipped-notes already logged (hardcoded dates expiring, `git checkout master -- .`
  overwriting a working tree) — this time neither a hardcoded date nor a git accident, but a
  relative-date computation that doesn't account for a business-day constraint elsewhere in the
  system. `gh pr checks 44 --watch` was auto-backgrounded by the tool harness after its own
  timeout mid-run (not a deliberate `run_in_background: true`) — per 031's precedent, the
  correct response is to actively watch the already-backgrounded task to completion rather than
  treat the auto-backgrounding as license to stop waiting; re-ran `gh pr checks 44 --watch` as a
  fresh blocking call (which completed once CI actually finished) while a parallel Monitor polled
  the same PR as a second signal, and confirmed completion via `gh pr view --json state,mergedAt`
  before proceeding — never assumed success from the auto-backgrounded notification alone.
- 025 (`025-coda-payment-matching`): ✅ Done, merged 2026-07-22 (PR #45, squash-merged after green
  CI — 1020/1020 backend + 245/245 web tests). Full pipeline run from a clean `master` in a single
  session. CODA bank-statement import with automatic OGM-reference matching, director-confirmable
  amount+IBAN suggestions (only possible for families with a SEPA mandate on file, feature 024 —
  there is no other IBAN-capture point anywhere in this codebase, confirmed during research rather
  than assumed), and a manual-review queue for unmatched/duplicate/closed-invoice/reversal
  transactions. During `/speckit-plan` research, discovered a real licensing problem the BACKLOG
  prompt's "use an existing .NET CODA parser" instruction didn't anticipate: the only two real
  Belgian-CODA .NET libraries on NuGet are GPL-2.0/GPL-3.0, no MIT/Apache option exists anywhere —
  paused and asked the product owner per the standing rule (this codebase's own constitution
  already shows license-consciousness: QuestPDF was picked specifically for being MIT). Resolved
  after clarifying the actual GPL mechanics (the "distribution" copyleft trigger doesn't apply to
  running a library server-side in a pure SaaS product with no on-prem distribution — that's what
  AGPL exists to close, and neither library is AGPL): use the GPL-2.0 `CodaParser` package,
  revisit only if this product's distribution model ever changes. Reused `MarkInvoicePaidCommand`
  (014/014a/030's paid-transition, sibling-cascade, receipt-notification) rather than
  reimplementing it a third time, the same lesson 030's own shipped-note already drew from
  `ProcessPaymentWebhookCommand` duplicating that logic instead of reusing it. Gave sender IBAN
  its own Data Protection purpose string (`ICodaSenderIbanProtector`) distinct from `Contract`'s
  existing `IIbanProtector` (024) — a bank-statement counterparty account and a signed SEPA
  mandate's account are different data even though both happen to be IBANs; mixing them under one
  purpose string would have been a real, easy-to-miss bug (Data Protection ciphertext only
  decrypts under the exact purpose it was encrypted with). `/speckit-checklist`'s safety-focused
  pass (15 items, all fixed) caught that the spec's first draft treated partial-payment
  "outstanding total" as a single transaction's shortfall rather than cumulative across multiple
  partials, and that FR-008's duplicate case and FR-009's closed-invoice case read as the same
  thing without explicit disambiguating language — both corrected in spec.md before planning
  continued, not deferred. `/speckit-analyze` then caught that the checklist's own fixes (the
  cumulative-partial-payment math, IBAN access-logging) had outpaced tasks.md — added T015a/T018a
  and extended T009/T010/T018 rather than letting the plan drift from the just-updated spec.
  Running the full test suite before merging surfaced the exact recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` revert-helper pattern yet again
  (012a onward) — but this time it also broke two OTHER independent schema-reverting tests
  (`PaymentReminderTests`' broken-tenant simulation, `PublicEnrollmentSlugBackfillMigrationTests`'
  own revert helper) that don't live in the two "usual" files, confirming the pattern applies to
  *any* test that drops/reverts tenant schema objects, not just the two canonically-named ones —
  worth grepping broadly (`__EFMigrationsHistory`, `DROP TABLE.*invoices`) rather than trusting
  memory of "the two files that always need this" on the next migration-adding feature. Also
  self-caught a real i18n authoring bug via the web test suite before it ever reached review: a
  Python script used to bulk-add the new `codaReconciliation` translation keys nested them at the
  JSON root instead of under `invoices`, which `next-intl`'s dotted-namespace lookup surfaced
  immediately as a hard `MISSING_MESSAGE` error rather than a silent fallback — fixed by
  re-nesting in all three locale files before it became a shipped bug.
- 026 (`026-sepa-direct-debit`): ✅ Done, merged 2026-07-22 (PR #46, squash-merged after green CI —
  1055/1055 backend + 254/254 web tests). Resumed mid-flight: a prior session had created the
  branch with zero commits on it (identical to `master`) — this run started the pipeline from
  scratch on that branch rather than creating a new one. pain.008.001.02 SEPA direct debit batch
  generation, hand-built via `System.Xml.Linq` and validated against the real, embedded EPC/
  ISO20022 schema (cross-verified byte-identical from two unrelated open-source projects before
  trusting it) rather than a third-party SEPA-generation package — avoided the exact licensing
  question 025's CODA parsing needed to resolve, since generation for this feature's actual scope
  (one `PmtInf` block per batch) is a small, mechanical, well-documented tree, unlike CODA's real
  parsing complexity. Corrected two BACKLOG-prompt premises during specify, both resolved against
  already-shipped code rather than guessed: no new settings entity for creditor identifier/name/
  IBAN (reuses `Tenant.SepaCreditorIdentifier` from 024, `Location.BankAccountNumber` from 014),
  and the execution-date "business day" rule is a plain Mon-Fri check, independent of 011's
  closure calendar. This feature's own safety checklist (CHK008) then surfaced a real data-model
  gap during planning: determining SEPA sequence type (FRST/RCUR) correctly across a returned
  debit and a mandate revoke-and-resign needed a new immutable `Invoice.SepaMandateReferenceUsed`
  snapshot, since the live, clearable `SepaBatchId` pointer alone silently gives the wrong answer
  in both cases — worth remembering generally: a checklist pass can surface implementation-level
  data-model gaps, not just requirements-wording issues, when the requirement itself implies a
  history query the current design can't actually answer. The concurrency test for FR-013 (two
  requests racing to claim the same invoice) caught a second, more subtle bug of this session's
  own making: an earlier *tracked* EF Core read of the same invoices left stale instances in the
  DbContext's change tracker, which identity resolution then silently returned from the later
  `SELECT ... FOR UPDATE` read instead of the fresh, lock-guaranteed values — the database-level
  lock was working correctly the whole time, but EF's own change tracker defeated it anyway; fixed
  with `AsNoTracking()` on the earlier read. `ITenantDbContext` had no raw SQL escape hatch by
  design (documented in its own doc comment, to keep Application layer Relational-agnostic) — added
  one narrowly-scoped `LockInvoicesForUpdateAsync` method for this, rather than widening the
  interface generally. Running the full test suite before merging surfaced the exact recurring
  `TenantMigrationRolloutTests`/`LegacyVaccinationMigrationTests` pattern yet again (012a onward) —
  and this time also broke `PublicEnrollmentSlugBackfillMigrationTests`, a *third* revert-helper
  file 025's own shipped-note had already explicitly named as needing this same fix — worth being
  honest that this session initially updated only the two "usual" files anyway and caught the third
  one solely by running the full suite, the exact failure mode that shipped-note already warned
  against by name. `/speckit-converge` found four real test-coverage gaps (a documented exclusion
  reason never exercised, two untested asymmetric-configuration edge cases, and a task tasks.md had
  already checked off without the test it names actually being written) — all fixed, not deferred,
  same standing rule as every prior feature; the checked-off-but-unwritten task is itself worth
  remembering as a new variant of "a claim to verify, not trust."
- 027 (`027-staff-app`): ✅ Done, merged 2026-07-23 (PR #47, squash-merged after green CI —
  1072/1072 backend + 257/257 web + 20/20 staff-mobile tests). Resumed mid-flight: a prior session
  had already fully implemented all four user stories (all 66 tasks checked off, including the
  Phase 7 design-compliance/quickstart/lockfile polish pass) with zero commits since the last
  checkpoint beyond the branch itself and no open PR — this invocation verified the work rather
  than trusting the checked boxes: read spec.md/plan.md/tasks.md in full and cross-checked the
  actual code for every FR/SC/constitution obligation (JWT-resolved identity on every staff read
  and write per FR-015/015a, FR-011a's Covered-row protection on leave-approval, FR-016's extended
  BKR-decoupling test, FR-008a's no-PII-leakage push content, the Monday-anchored publish gate,
  the `staff_leave_requests` revert-helper fix in both `TenantMigrationRolloutTests` and
  `LegacyVaccinationMigrationTests`, the production SQL script, and the workflow-doc extension)
  before running `/speckit-converge`, which then genuinely found nothing to append — a clean
  `converged` outcome, unlike most resumed-mid-flight features in this log. New `staff-mobile` Expo
  app (personal phone, distinct from both `mobile`'s kiosk tablet and `parent-mobile`): published
  schedule view (day/week, contracted-day de-emphasis, closure-day "KDV gesloten" via a new
  StaffOrDirector-scoped `/api/closures/dates` route added to `ClosureCalendarEndpoints.cs` rather
  than `StaffScheduleEndpoints.cs`, since a more permissive policy can't live inside the existing
  `DirectorOnly` route group — the same "separate MapGroup, same path" pattern this codebase's
  `LocationEndpoints.cs` had already established), one-tap "Ik ben ziek" sick reporting (idempotent
  per FR-005a), and planned leave requests. `StaffSchedule.IsAbsent`/`AbsenceReason` were reconciled
  into a single `Status` enum as plan.md's research.md R3 specified, with a migration backfill —
  confirmed the old boolean column was actually dropped, not left dangling alongside the new field.
  A `web/tsc --noEmit` run reproduced the exact same pre-existing, unrelated DOM-`Location`-vs-
  test-mock typing failure feature 023's shipped-note already documented on an untouched file — re-
  confirmed it still isn't part of CI's actual web gate (`web-test.yml` only runs `npm test`) before
  correctly leaving it alone rather than scope-creeping in a fix. `gh pr checks --watch` was again
  auto-backgrounded by the tool harness after its own timeout mid-run (the same non-deliberate
  auto-backgrounding 024/031 already logged, not the standing rule's warned-against failure mode of
  deliberately walking away) — per that precedent, started a `Monitor` polling `gh pr checks --json`
  to watch it to completion in the same turn, and independently confirmed the merge via `gh pr view
  --json state,mergedAt` before updating BACKLOG.md, rather than trusting either signal alone.
- 028 (`028-staff-hr-dossier`): ✅ Done, merged 2026-07-23 (PR #48, squash-merged after green CI —
  1096/1096 backend + 265/265 web tests; staff-mobile's 25/25 also run locally, no staff-mobile CI
  job exists yet). Full pipeline run from a clean `master` in a single session, invoked
  interactively rather than by the 4h cron trigger. Staff clock in/out via `staff-mobile` (one-tap;
  a location picker and a function picker each appear only when genuinely ambiguous — more than one
  `StaffLocationEligibility` grant or more than one configured function, mirroring the same
  ambiguity rule), a new director-web HR dossier per staff member (documents + configurable
  clock-in functions, on this codebase's first staff detail screen), director correction/unlock of
  time entries past a fixed 7-day lock, a contract-expiry dashboard block, and the medewerkersbeleid
  subsidy report (child-hours ÷ staff-hours by function, ratios only — no pass/fail evaluation,
  deferred to feature 041's not-yet-built versioned ruleset per an explicit clarification). New
  tenant tables `staff_time_entries` (computed lock, `UnlockedBy` attribution) and `staff_documents`
  (soft-deleted via `DeletedAt`/`DeletedBy`, mirroring this codebase's `DeactivatedAt` idiom rather
  than losing the audit trail on hard delete); a new **Staff Management** workflow added to
  `workflows.md` (no existing workflow covered staff HR/time tracking). `/speckit-checklist`'s
  safety/security/regulatory pass and `/speckit-analyze` together found and fixed 10 real
  requirements gaps before implementation began — the most consequential: clock-in never validated
  `StaffLocationEligibility` or the configured-function set (FR-001a/FR-005a/FR-004a), and unlock/
  document-upload/delete had no attribution (FR-007a/FR-012a) — all genuine subsidy-hours integrity
  gaps this feature exists to close, not leave open, caught before a single line of implementation
  code existed. Two design gaps were also found and fixed *during* implementation, not planning:
  the original contract had no way for staff-mobile to learn its own open-entry state on app
  reopen (added `GET /api/staff-time-entries/me/current`), and the one-tap clock-in flow had no
  design for *which location* to clock in at (resolved the same way as the function-ambiguity
  rule, extended `StaffMeResponse` with `eligibleLocationIds`/`timeEntryFunctions` so the client
  can decide without an extra round-trip). Running the full backend suite before opening the PR
  caught a real, environment-dependent bug via the CSV-parity test: `StaffHoursCsvWriter` formatted
  durations with the server's default culture instead of invariant, so "6.00" silently rendered as
  "6,00" on a comma-decimal locale — splitting the `DurationHours` field across two CSV columns;
  fixed with explicit `CultureInfo.InvariantCulture` on every formatted field (this codebase's first
  CSV writer to touch decimals/dates, so not a repeat of an existing bug, a new class of it). The
  same full-suite run also confirmed the recurring `TenantMigrationRolloutTests`/
  `LegacyVaccinationMigrationTests` revert-helper pattern (012a onward) yet again — but this time
  it also broke a *third* file, `PublicEnrollmentSlugBackfillMigrationTests`, whose own doc comment
  already named the exact mechanism (`TenantDbContext.MigrateAsync()` computes its pending-migration
  script from `applied.LastOrDefault()`, the last *recorded* migration, not the first gap — leaving
  a later migration's history row in place after an otherwise-full revert makes it think the tenant
  is already current, silently no-op'ing the rest of the forward migration) and predicted it would
  recur for "any future migration"; this is the first time that file's own DELETE list was still one
  migration short when the predicted failure actually landed. `gh pr checks --watch` was again
  auto-backgrounded by the tool harness after its own timeout (the same non-deliberate pattern
  024/027/031 already logged) — watched it to completion via `Monitor` in the same turn and
  independently confirmed the merge via `gh pr view --json state,mergedAt` before updating
  BACKLOG.md, same precedent as every prior recurrence.
