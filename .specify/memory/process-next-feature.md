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
- 008 (`008-caregiver-app-scaffold`): implementation complete as of 2026-07-07 (all tasks +
  convergence findings closed, 185/185 backend + 58/58 mobile tests passing), held uncommitted
  pending rework — industry research surfaced that caregivers use a shared room tablet with a
  PIN, not individual email/password login. Feature `008a-caregiver-kiosk-mode` now sits between
  008 and 009/010 in BACKLOG.md to add that layer; 008's login screen was explicitly scoped as
  underlying-mechanism scaffolding, not final UX, in BACKLOG.md's own post-shipping note.
- 2026-07-07: loop prompt reworked — fixed a duplicated/malformed step 4 block from an earlier
  edit, added the design-system/workflow-aware spec template, replaced the screenshot-based
  "visual review" step (no simulator/screenshot tooling exists in this repo) with a static
  code-level design-compliance review, and switched from self-rescheduling dynamic-loop mode to
  single-pass (no `ScheduleWakeup`, manual re-invocation between features).
