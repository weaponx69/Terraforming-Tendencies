#!/usr/bin/env bash
# Drive a sector-win check against the already-open Unity Editor (Pipeline).
# Safe for this project — does NOT spawn a second Editor via `unity test`.
#
# Usage:
#   1. Open this project in Unity (Pipeline connected)
#   2. ./tools/sector-win-cli.sh
#
# Exit codes:
#   0 = RESULT: PASS (or already between rounds)
#   1 = setup / Editor not ready
#   2 = RESULT: FAIL from automation
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

eval_json() {
  local code="$1"
  local timeout="${2:-90}"
  unity command eval "$code" --json --timeout "$timeout"
}

extract_result_text() {
  python3 -c '
import sys, json, re
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print(raw)
    sys.exit(0)
if not d.get("success"):
    errs = d.get("errors") or []
    msg = errs[0].get("message") if errs else "eval failed"
    print(msg)
    sys.exit(0)
result = d.get("data", {}).get("result", {})
# Pipeline wraps differently depending on version
text = result.get("result")
if text is None:
    text = result.get("output")
if text is None:
    text = d
print(text)
'
}

echo "== unity status =="
unity status --format json

STATE="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$STATE" != "ready" ]]; then
  echo "Editor not ready (state=$STATE)."
  echo "Open Terraforming Tendencies in Unity and wait until Pipeline is connected, then re-run."
  exit 1
fi

# Ensure scripts are compiled before Play Mode (stale DLLs caused false ATMOSPHERE softlock fails).
echo "== recompile check =="
unity command recompile --json >/dev/null || true
for i in $(seq 1 30); do
  RS="$(unity command recompile_status --json 2>/dev/null || true)"
  if echo "$RS" | grep -q '"up_to_date"\|"status\":\"up_to_date"\|"failed\":false'; then
    if echo "$RS" | grep -q '"compiling":true'; then
      echo "  compiling... ($i)"
      sleep 1
      continue
    fi
    break
  fi
  sleep 1
done
EDITOR_STATUS="$(unity command editor_status --json 2>/dev/null || true)"
if echo "$EDITOR_STATUS" | grep -q '"compiling":true'; then
  echo "Waiting for script compile to finish..."
  for i in $(seq 1 60); do
    sleep 1
    EDITOR_STATUS="$(unity command editor_status --json 2>/dev/null || true)"
    echo "$EDITOR_STATUS" | grep -q '"compiling":true' || break
  done
fi

PLAYING="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 30 | python3 -c "import sys,json; d=json.load(sys.stdin); r=(d.get('data') or {}).get('result') or {}; print(r.get('result', False))" 2>/dev/null || echo false)"
echo "Play mode: $PLAYING"

if [[ "$PLAYING" != "True" && "$PLAYING" != "true" ]]; then
  echo "== editor_play =="
  unity command editor_play --json || true
  # Wait for GenerationManager / planet gen
  for i in $(seq 1 40); do
    sleep 2
    PLAYING="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 30 | python3 -c "import sys,json; d=json.load(sys.stdin); r=(d.get('data') or {}).get('result') or {}; print(r.get('result', False))" 2>/dev/null || echo false)"
    HAS_GM="$(eval_json 'return GameDevTV.RTS.Player.GenerationManager.Instance != null;' 60 | python3 -c "import sys,json; d=json.load(sys.stdin); r=(d.get('data') or {}).get('result') or {}; print(r.get('result', False))" 2>/dev/null || echo false)"
    echo "  wait ${i}: playing=$PLAYING generationManager=$HAS_GM"
    if [[ "$PLAYING" == "True" || "$PLAYING" == "true" ]] && [[ "$HAS_GM" == "True" || "$HAS_GM" == "true" ]]; then
      break
    fi
  done
fi

echo "== SectorWinAutomation.Report =="
REPORT_JSON="$(eval_json 'return GameDevTV.RTS.Player.SectorWinAutomation.Report();' 90)"
echo "$REPORT_JSON" | extract_result_text

echo "== SectorWinAutomation.TryWinAndColonizeNextSector (strict) =="
WIN_JSON="$(eval_json 'return GameDevTV.RTS.Player.SectorWinAutomation.TryWinAndColonizeNextSector();' 180)"
WIN_TEXT="$(echo "$WIN_JSON" | extract_result_text)"
echo "$WIN_TEXT"

if echo "$WIN_TEXT" | grep -q "RESULT: PASS\|RESULT: ALREADY_BETWEEN_ROUNDS\|RESULT: SKIP"; then
  echo "Bot finished successfully."
  exit 0
fi

if echo "$WIN_TEXT" | grep -q "RESULT: FAIL\|Compilation Failed\|does not exist"; then
  echo "Bot did not pass. See RESULT above."
  exit 2
fi

echo "Could not find RESULT: PASS in output."
exit 2
