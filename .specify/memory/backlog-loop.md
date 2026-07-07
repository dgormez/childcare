# Backlog Processing Loop

This is the exact prompt used to autonomously process `BACKLOG.md` one feature at a time
through the full SpecKit pipeline (specify → clarify → plan → tasks → checklist → analyze →
implement → converge → PR → merge). It's saved here so a fresh session (or a teammate) can
resume the loop without retyping it — paste the block below after `/loop` (no interval prefix
runs it in dynamic/self-paced mode; prefix with an interval like `30m` for fixed-cadence cron
mode instead).

## Standing process rules (apply regardless of how the loop is invoked)

- **Fix every `/speckit-checklist`/`/speckit-analyze`/`/speckit-converge` finding**, even ones
  marked LOW/advisory — don't just log them as debt. (Established after an explicit correction
  mid-backlog; see feature 005/006/007's shipped-notes for examples of findings that were fixed
  rather than deferred.)
- When a feature's own prompt block raises a genuinely new, no-precedent scope question (e.g.,
  "should mobile UI work happen here, and is there a foundation for it yet?" — the call that
  led to inserting feature 008 ahead of the original child-events feature), pause and ask
  instead of guessing. Everything else — clarify-phase questions with a clear recommended
  default, minor backend plumbing a feature can't function without — proceed autonomously.
- Recurring cron-mode loops are session-only; if a decision blocks progress, cancel the cron
  (`CronDelete`) rather than letting it re-fire uselessly every interval, and wait for the
  human to respond before restarting `/loop`.

## The prompt

```text
Process the ChildCare SpecKit backlog one feature at a time until BACKLOG.md has no 🔲 Not started rows left with satisfied dependencies.

Each firing:
0. Check for in-progress work first: is there a non-master branch checked out, or an open
   (unmerged) PR from a prior firing? If so, resume that feature from wherever it left off —
   don't re-run specify/plan/tasks if spec.md/plan.md/tasks.md already exist for it — instead
   of starting a new one.
1. Otherwise, read BACKLOG.md and pick the first 🔲 Not started feature (table order) whose
   "Depends on" column is entirely ✅ Done. If none remain, report the backlog is complete and
   end the loop (stop scheduling further firings).
2. Find that feature's prompt block under "## Spec Kit Inputs" in BACKLOG.md.
3. git checkout master, pull latest, then git checkout -b <branch> (name from the BACKLOG row).
4. Run the SpecKit pipeline for this feature:
   - speckit-specify with the feature's prompt block
   - speckit-clarify — for every question, pick the option it recommends/marks default; only
     ask me if a question has no clear recommended option and no comparable precedent in an
     already-Done feature's spec
   - speckit-plan
   - speckit-tasks
   - speckit-checklist — run it, but treat findings as advisory; only stop the loop for a
     genuine spec contradiction or missing FR, not style nits
   - speckit-analyze
   - speckit-implement
   - speckit-converge to close out anything implement missed
5. Build and run the test suite. If something fails, try to fix it (up to ~3 attempts). If
   still failing, stop the loop, leave the branch pushed, and report what's broken — do not
   merge broken code.
6. Commit as you go with real messages. Push the branch.
7. gh pr create targeting master.
8. Wait for gh pr checks to go green. Red CI is handled like step 5 — fix, retry, or stop and
   report. Never merge on red.
9. gh pr merge --squash once CI passes (matches how 001-003 were merged).
10. Update the BACKLOG.md status for this feature to ✅ Done and push that change.
11. On a transient API/network error mid-turn, retry with backoff before giving up on the
    current step.

Never force-push. Never merge on failing build/tests/CI. Never start a feature whose
dependencies aren't all ✅ Done.
```

## Progress log (update as features land)

- 001–007: shipped prior to this file existing (see BACKLOG.md's own shipped-notes per feature).
- 008 (`008-caregiver-app-scaffold`): in progress as of 2026-07-07 — first feature touching `mobile/`.
