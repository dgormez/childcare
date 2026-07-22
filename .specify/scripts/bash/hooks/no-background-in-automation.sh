#!/usr/bin/env bash
# PreToolUse hook (Bash matcher). Denies run_in_background:true, but only
# when CHILDCARE_AUTOMATION_NO_BACKGROUND=1 is set in the environment —
# only run-process-next-feature.sh sets it, so interactive Claude Code use
# in this repo (which relies on backgrounding, e.g. `nohup dotnet run`) is
# unaffected.
#
# Exists because the unattended process-next-feature pipeline (headless
# `claude -p`, one-shot process, no later turn) has repeatedly backgrounded
# a wait (test suite, gh pr checks) and then ended its turn expecting to be
# "notified automatically" — the process just exits, the background work is
# abandoned, and the run silently fails to finish. See the "Standing process
# rules" section of .specify/memory/process-next-feature.md for the history;
# this hook makes that rule impossible to violate instead of relying on the
# model to keep remembering it.
set -euo pipefail

if [ "${CHILDCARE_AUTOMATION_NO_BACKGROUND:-}" != "1" ]; then
  exit 0
fi

jq -c 'select(.tool_input.run_in_background == true) | {
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: "Backgrounding is disabled for this unattended run (process-next-feature automation). A scheduled `claude -p` invocation is a one-shot process with no later turn to receive a background completion notification -- anything backgrounded here gets silently abandoned when the process exits. Retry this exact command as a blocking foreground call instead (drop run_in_background, or use a synchronous equivalent such as `gh pr checks <N> --watch`)."
  }
}'
