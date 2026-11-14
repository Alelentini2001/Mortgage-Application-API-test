#!/usr/bin/env bash
# Single verification command for humans, hooks and CI.
#   ./scripts/verify.sh          full: restore, build (warnings as errors), all tests, hook tests, format check
#   ./scripts/verify.sh --fast   fast: restore, build, unit tests only (used by the Stop hook)
set -euo pipefail

cd "$(dirname "$0")/.."

SOLUTION="MortgageApplications.slnx"
CONFIG="Release"
MODE="full"
[[ "${1:-}" == "--fast" ]] && MODE="fast"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

step() { printf '\n==> %s\n' "$1"; }
fail() { printf '\nVERIFY FAILED (%s): %s\n' "$MODE" "$1" >&2; exit 1; }

step "restore"
dotnet restore "$SOLUTION" --nologo -v quiet || fail "restore"

step "build ($CONFIG, warnings as errors)"
dotnet build "$SOLUTION" -c "$CONFIG" --no-restore --nologo -v quiet || fail "build"

if [[ "$MODE" == "fast" ]]; then
  step "unit tests"
  dotnet test --project tests/MortgageApplications.UnitTests -c "$CONFIG" --no-build || fail "unit tests"
else
  step "all tests"
  dotnet test --solution "$SOLUTION" -c "$CONFIG" --no-build || fail "tests"

  step "hook regression tests"
  .claude/hooks/tests/test-hooks.sh || fail "hook tests"

  step "format check"
  dotnet format "$SOLUTION" --verify-no-changes --no-restore -v quiet || fail "format (run: dotnet format $SOLUTION)"
fi

printf '\nVERIFY PASSED (%s)\n' "$MODE"
