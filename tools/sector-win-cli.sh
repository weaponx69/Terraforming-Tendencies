#!/usr/bin/env bash
# Drive sector-win checks against the already-open Unity Editor (Pipeline).
# Safe for this project — does NOT spawn a second Editor via `unity test`.
#
# Runs two Play Mode bots after recompile:
#   1. TryWinAndColonizeViaPlayerUi  — generation summary → colonization popup (player path)
#   2. TryWinAndColonizeNextSector   — direct API advance (regression)
#
# Usage:
#   1. Open this project in Unity (Pipeline connected)
#   2. ./tools/sector-win-cli.sh
#
# Exit codes:
#   0 = both bots passed
#   1 = setup / Editor not ready
#   2 = one or both bots failed
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
import sys, json
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
text = result.get("result")
if text is None:
    text = result.get("output")
if text is None:
    text = d
print(text)
'
}

result_passed() {
  local text="$1"
  echo "$text" | grep -q "RESULT: PASS\|RESULT: ALREADY_BETWEEN_ROUNDS\|RESULT: SKIP"
}

echo "== unity status =="
unity status --format json

STATE="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$STATE" != "ready" ]]; then
  echo "Editor not ready (state=$STATE)."
  echo "Open Terraforming Tendencies in Unity and wait until Pipeline is connected, then re-run."
  exit 1
fi

echo "== recompile =="
unity command recompile --json >/dev/null || true
for i in $(seq 1 90); do
  RS="$(unity command recompile_status --json 2>/dev/null || true)"
  if echo "$RS" | grep -q '"status":"completed"\|"status\\":\\"completed\\"'; then
    if echo "$RS" | grep -q '"failed":true'; then
      echo "Recompile failed."
      echo "$RS"
      exit 1
    fi
    echo "  compile complete"
    break
  fi
  if echo "$RS" | grep -q '"status":"compiling"\|"status\\":\\"compiling\\"'; then
    echo "  compiling... ($i)"
    sleep 1
    continue
  fi
  sleep 1
done

echo "== wait for Editor after recompile =="
for i in $(seq 1 60); do
  STATE="$(unity status --format json 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')" 2>/dev/null || echo none)"
  if [[ "$STATE" == "ready" ]]; then
    echo "  Editor ready"
    break
  fi
  echo "  waiting for Editor ($i, state=$STATE)..."
  sleep 2
done
STATE="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$STATE" != "ready" ]]; then
  echo "Editor not ready after recompile (state=$STATE)."
  exit 1
fi

PLAYING="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 30 | python3 -c "import sys,json; d=json.load(sys.stdin); r=(d.get('data') or {}).get('result') or {}; print(r.get('result', False))" 2>/dev/null || echo false)"
echo "Play mode: $PLAYING"

if [[ "$PLAYING" != "True" && "$PLAYING" != "true" ]]; then
  echo "== editor_play =="
  unity command editor_play --json || true
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

UI_OK=0
API_OK=0

echo ""
echo "== SectorWinAutomation.TryWinAndColonizeViaPlayerUi (player UI path) =="
UI_JSON="$(eval_json 'return GameDevTV.RTS.Player.SectorWinAutomation.TryWinAndColonizeViaPlayerUi();' 180)"
UI_TEXT="$(echo "$UI_JSON" | extract_result_text)"
echo "$UI_TEXT"
if result_passed "$UI_TEXT"; then
  UI_OK=1
  echo "Player UI path: PASS"
else
  echo "Player UI path: FAIL"
fi

echo ""
echo "== SectorWinAutomation.TryWinAndColonizeNextSector (direct API path) =="
API_JSON="$(eval_json 'return GameDevTV.RTS.Player.SectorWinAutomation.TryWinAndColonizeNextSector();' 180)"
API_TEXT="$(echo "$API_JSON" | extract_result_text)"
echo "$API_TEXT"
if result_passed "$API_TEXT"; then
  API_OK=1
  echo "Direct API path: PASS"
else
  echo "Direct API path: FAIL"
fi

echo ""
if [[ "$UI_OK" -eq 1 && "$API_OK" -eq 1 ]]; then
  echo "Both sector-win bots passed (UI + API)."
  exit 0
fi

if [[ "$UI_OK" -eq 0 ]]; then
  echo "Player UI colonization path failed — this matches manual play after clicking Continue on the generation summary."
fi
if [[ "$API_OK" -eq 0 ]]; then
  echo "Direct API colonization path failed."
fi
exit 2
