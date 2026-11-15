#!/usr/bin/env bash
# SessionStart (compact): thin shim kept so the existing wiring and test still work (H1, A13).
# The work is done by session-context.sh with the source fixed to "compact".
set -uo pipefail

exec "$(dirname "$0")/session-context.sh" --source compact
