#!/usr/bin/env bash
# Run EditMode tests against the already-open Unity Editor (Pipeline).
# Does NOT spawn a second Editor — safe for this heavy project.
#
# Usage:
#   1. Open this project in Unity (Pipeline connected: unity status)
#   2. ./tools/run-editmode-tests.sh
#   3. ./tools/run-editmode-tests.sh SectorColonizationTests   # optional filter
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

FILTER="${1:-SectorColonizationTests;SectorWinAutomationTests;CardDeckProgressionTests}"

echo "== unity status =="
unity status --format json

echo "== EditMode tests (filter: ${FILTER}) =="
unity command run_tests --mode editmode --filter "$FILTER" --json
