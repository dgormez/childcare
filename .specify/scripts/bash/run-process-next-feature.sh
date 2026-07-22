#!/usr/bin/env bash

# Runs one headless, non-interactive pass of the process-next-feature pipeline.
# Invoked by launchd every 4h (com.dgormez.childcare.process-next-feature.plist)
# and safe to run manually for a one-off pass outside the schedule.
#
# Each invocation is a brand-new `claude -p` process with no prior conversation
# context, so token usage never compounds across runs. Recovery from a crashed
# or interrupted prior run is handled entirely by the prompt's own step 0
# (resume from git branch/PR state), not by session resumption.

set -euo pipefail

# launchd gives this script a minimal PATH (no Homebrew, no .NET) — set it
# explicitly so both this script and any tool `claude` shells out to
# (git, gh, node, npm, dotnet) can actually be found.
export PATH="/opt/homebrew/bin:/usr/local/share/dotnet:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$PATH"

REPO_DIR="/Users/dgormez/Documents/Claude/Projects/ChildCare Software/ChildCare"
PROMPT_FILE="$REPO_DIR/.specify/memory/process-next-feature.md"
LOG_DIR="$HOME/Library/Logs/ChildCareAutomation"
LOG_FILE="$LOG_DIR/process-next-feature.log"
LOCK_DIR="/tmp/childcare-process-next-feature.lock.d"

mkdir -p "$LOG_DIR"

# Rotate the log once it gets large rather than growing forever.
if [ -f "$LOG_FILE" ] && [ "$(stat -f%z "$LOG_FILE" 2>/dev/null || echo 0)" -gt 20971520 ]; then
  mv "$LOG_FILE" "$LOG_FILE.$(date +%Y%m%d%H%M%S).old"
fi

exec >> "$LOG_FILE" 2>&1

echo ""
echo "=================================================================="
echo "=== Run started: $(date '+%Y-%m-%d %H:%M:%S %z')"
echo "=================================================================="

# Guard against overlapping runs if a prior pass is still in progress when the
# next 4h trigger fires (e.g. a large feature took >4h). Uses `mkdir` rather
# than a check-then-write on a plain file: mkdir is atomic, so two invocations
# racing at (nearly) the same instant can't both pass the check before either
# writes — exactly what let two concurrent runs both work feature 022 on
# 2026-07-20 (see process-next-feature.md's progress log for that incident).
acquire_lock() {
  if mkdir "$LOCK_DIR" 2>/dev/null; then
    echo $$ > "$LOCK_DIR/pid"
    return 0
  fi
  return 1
}

if ! acquire_lock; then
  existing_pid=$(cat "$LOCK_DIR/pid" 2>/dev/null || echo "")
  if [ -n "$existing_pid" ] && kill -0 "$existing_pid" 2>/dev/null; then
    echo "Previous run (PID $existing_pid) still in progress — skipping this trigger."
    exit 0
  fi
  # Owning process is dead (or the pid file is mid-write) — reclaim once.
  echo "Found stale lock dir (no live owning process) — reclaiming."
  rm -rf "$LOCK_DIR"
  if ! acquire_lock; then
    echo "Lost the race to reclaim the lock — another run just started. Skipping."
    exit 0
  fi
fi
trap 'rm -rf "$LOCK_DIR"' EXIT

cd "$REPO_DIR"

PROMPT=$(awk '/^```text$/{flag=1; next} /^```$/{if(flag) exit} flag' "$PROMPT_FILE")

if [ -z "$PROMPT" ]; then
  echo "ERROR: could not extract prompt block from $PROMPT_FILE — aborting."
  exit 1
fi

# Gates .specify/scripts/bash/hooks/no-background-in-automation.sh, which
# denies any Bash call with run_in_background:true. Only set here — never
# for interactive sessions — because backgrounding a wait (test suite, CI
# checks) and expecting to be notified later silently abandons the work:
# this is a one-shot headless process with no later turn for that
# notification to arrive in.
export CHILDCARE_AUTOMATION_NO_BACKGROUND=1

claude -p "$PROMPT" \
  --permission-mode bypassPermissions \
  --no-session-persistence \
  --add-dir "$REPO_DIR"

echo "=== Run finished: $(date '+%Y-%m-%d %H:%M:%S %z')"
