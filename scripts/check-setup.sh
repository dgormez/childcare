#!/usr/bin/env bash
set -euo pipefail

PASS=0
FAIL=0

ok()   { echo "  [OK]  $1"; ((PASS++)); }
fail() { echo "  [FAIL] $1"; ((FAIL++)); }
warn() { echo "  [WARN] $1"; }

echo ""
echo "ChildCare setup check"
echo "====================="
echo ""

# .NET 10
if command -v dotnet &>/dev/null; then
  DOTNET_VER=$(dotnet --version 2>/dev/null | cut -d. -f1)
  if [ "$DOTNET_VER" = "10" ]; then
    ok ".NET $(dotnet --version)"
  else
    fail ".NET 10 required — found $(dotnet --version). Install from https://dotnet.microsoft.com/download"
  fi
else
  fail ".NET not found. Install from https://dotnet.microsoft.com/download"
fi

# Node 20+
if command -v node &>/dev/null; then
  NODE_VER=$(node --version | sed 's/v//' | cut -d. -f1)
  if [ "$NODE_VER" -ge 20 ]; then
    ok "Node $(node --version)"
  else
    fail "Node 20+ required — found $(node --version). Install from https://nodejs.org"
  fi
else
  fail "Node not found. Install from https://nodejs.org"
fi

# EAS CLI
if command -v eas &>/dev/null; then
  ok "EAS CLI $(eas --version 2>/dev/null | head -1)"
else
  fail "EAS CLI not found. Run: npm install -g eas-cli"
fi

# mobile/.env
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
ENV_FILE="$REPO_ROOT/mobile/.env"

if [ -f "$ENV_FILE" ]; then
  ok "mobile/.env exists"
else
  warn "mobile/.env not found — run: cp mobile/.env.example mobile/.env"
  ((FAIL++))
fi

# Summary
echo ""
echo "====================="
if [ "$FAIL" -eq 0 ]; then
  echo "  All checks passed ($PASS/$((PASS + FAIL))). You're good to go."
else
  echo "  $FAIL check(s) failed, $PASS passed. Fix the issues above and re-run."
fi
echo ""

exit $FAIL
