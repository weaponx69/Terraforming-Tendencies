#!/usr/bin/env bash
# Drive a sector-win check against the already-open Unity Editor (Pipeline).
# Safe for this project — does NOT spawn a second Editor via `unity test`.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "== unity status =="
unity status --format json

STATE="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$STATE" != "ready" ]]; then
  echo "Editor not ready (state=$STATE). Open this project in Unity first."
  exit 1
fi

PLAYING="$(unity command eval "return UnityEditor.EditorApplication.isPlaying;" --json | python3 -c "import sys,json; d=json.load(sys.stdin); print(str(d.get('data',{})).lower())" 2>/dev/null || true)"
echo "Play mode probe: $PLAYING"

if ! echo "$PLAYING" | grep -qi true; then
  echo "== editor_play =="
  unity command editor_play --json || true
  sleep 3
fi

echo "== SectorWinAutomation.Report =="
unity command eval "return GameDevTV.RTS.Player.SectorWinAutomation.Report();" --json --timeout 60

echo "== SectorWinAutomation.TryWinCurrentSector =="
unity command eval "return GameDevTV.RTS.Player.SectorWinAutomation.TryWinCurrentSector();" --json --timeout 120

echo "Done. Look for RESULT: PASS in the eval output above."
