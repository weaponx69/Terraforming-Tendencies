#!/usr/bin/env bash
# Drive the Colony Acts *teaching* player bot (Watch by default).
# Shows how the game works: places from hand, spends weeks, shops, narrates.
# Stress mode also probes gates (CP on claimed sector, off-feature geology, etc.).
#
# Usage:
#   1. Open this project in Unity (Pipeline connected)
#   2. ./tools/colony-acts-player-bot-cli.sh           # Watch
#      ./tools/colony-acts-player-bot-cli.sh stress    # Stress
#
# Exit codes:
#   0 = WIN or intentional STUCK/FAIL that surfaced an edge (still useful)
#   1 = setup / Editor not ready
#   2 = bot never started / aborted with no useful signal
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

eval_json() {
  local code="$1"
  local timeout="${2:-120}"
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
  sleep 1
done

PLAYING="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 30 | extract_result_text)"
echo "Play mode: $PLAYING"

if [[ "$PLAYING" != "True" && "$PLAYING" != "true" ]]; then
  echo "== editor_play =="
  unity command eval 'UnityEditor.EditorApplication.isPlaying = true; return "play";' --json >/dev/null || true
fi

echo "== wait for Colony Acts =="
for i in $(seq 1 60); do
  ST="$(eval_json 'var a=GameDevTV.RTS.Player.ColonyActManager.Instance; if(!UnityEditor.EditorApplication.isPlaying) return "notplaying"; if(a==null||!a.IsRunActive) return "waiting"; return "ready";' 60 | extract_result_text)"
  echo "  wait $i: $ST"
  [[ "$ST" == "ready" ]] && break
  sleep 2
done

MODE="${1:-watch}"
BOT_CALL="return GameDevTV.RTS.Player.ColonyActsPlayerBot.RunWatch();"
if [[ "$MODE" == "stress" ]]; then
  BOT_CALL="return GameDevTV.RTS.Player.ColonyActsPlayerBot.RunStress();"
fi

echo "== ColonyActsPlayerBot ($MODE) =="
START="$(eval_json "$BOT_CALL" 90 | extract_result_text)"
echo "$START"
if ! echo "$START" | grep -q "STARTED"; then
  echo "Bot failed to start."
  exit 2
fi

echo "== poll Status (watch Console + Colony Acts banner in Game view) =="
FINAL=""
for i in $(seq 1 240); do
  FINAL="$(eval_json 'return GameDevTV.RTS.Player.ColonyActsPlayerBot.Status();' 60 | extract_result_text)"
  echo "  [$i] $FINAL"
  if echo "$FINAL" | grep -Eq 'running=False'; then
    break
  fi
  sleep 3
done

# Pull last console RESULT if Status is terse.
CONSOLE="$(unity command console --json 2>/dev/null | python3 -c '
import sys,json
d=json.load(sys.stdin)
entries=((d.get("data") or {}).get("result") or {}).get("entries") or []
for e in reversed(entries[-40:]):
  msg=e.get("message","")
  if "ColonyActsPlayerBot" in msg and "RESULT:" in msg:
    print(msg)
    break
' || true)"

echo ""
echo "Status: $FINAL"
[[ -n "$CONSOLE" ]] && echo "Console: $CONSOLE"

if echo "$FINAL $CONSOLE" | grep -q "RESULT: WIN"; then
  echo "Player bot: useful run (WIN)."
  exit 0
fi
if echo "$FINAL $CONSOLE" | grep -Eq "STUCK|RESULT: FAIL"; then
  echo "Player bot: useful run (stuck/fail = edge case signal). Read Console narration."
  exit 0
fi

echo "Player bot: no clear outcome — check Game view / Console."
exit 2
