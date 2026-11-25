#!/usr/bin/env bash
# Supervised improve loop: one playthrough per iteration, metrics + UI health, optional stop file.
# Does NOT nohup — you kill with Ctrl+C or touch STOP_FILE.
#
# Usage:
#   ./tools/prologue-improve-until-win.sh
#   ITERATIONS=20 MIN_FPS=20 ./tools/prologue-improve-until-win.sh
#
# Env:
#   ITERATIONS          — max attempts (default 12; 0 = unlimited)
#   MIN_FPS             — warn / pre-mitigate if metrics fall below (default 20)
#   STOP_FILE           — default /tmp/prologue-improve-stop (touch to stop between iterations)
#   OUT_DIR_PREFIX      — default Assets/Playtest/improve-loop
#   SKIP_ENTER_PLAY     — passed through when Editor already in Play
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ITERATIONS="${ITERATIONS:-12}"
MIN_FPS="${MIN_FPS:-20}"
STOP_FILE="${STOP_FILE:-/tmp/prologue-improve-stop}"
PLAY="${ROOT}/tools/prologue-ui-playthrough.sh"
LOG="${LOG:-Assets/Playtest/improve-until-win.log}"
mkdir -p "$(dirname "$LOG")"

rm -f "$STOP_FILE"

iter=0
while true; do
  iter=$((iter + 1))
  if [[ -f "$STOP_FILE" ]]; then
    echo "Stop file present — exiting at iteration $iter." | tee -a "$LOG"
    exit 0
  fi
  if [[ "$ITERATIONS" != "0" && "$iter" -gt "$ITERATIONS" ]]; then
    echo "No win after $ITERATIONS iterations." | tee -a "$LOG"
    exit 4
  fi

  stamp="$(date +%Y%m%d-%H%M%S)"
  export OUT_DIR="${OUT_DIR_PREFIX:-Assets/Playtest/improve-loop}-${stamp}-i${iter}"
  echo "=== improve iteration $iter -> $OUT_DIR ===" | tee -a "$LOG"

  unity command eval 'if(GameDevTV.RTS.Prologue.ProloguePlaytestPerf.TryMitigateLag(out var s)) return s; return "perf_ok";' \
    --json --timeout 30 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); r=d.get('data',{}).get('result',{}).get('result',''); print(r)" >>"$LOG" 2>/dev/null || true

  set +e
  MAX_ATTEMPTS=1 "${ROOT}/tools/prologue-ui-playthrough-until-win.sh" 2>&1 | tee -a "$LOG"
  ec=${PIPESTATUS[0]}
  set -e

  METRICS="${OUT_DIR}/metrics.jsonl"
  if [[ -f "$METRICS" ]]; then
    python3 -c "
import json,sys
fps=[]
for line in open(sys.argv[1]):
    try: fps.append(json.loads(line).get('fps',0))
    except: pass
if fps:
    lo=min(fps); med=sorted(fps)[len(fps)//2]
    print(f'  fps min={lo} median={med}')
    if lo < int(sys.argv[2]):
        print(f'  WARN: fps below {sys.argv[2]} — lag mitigation may apply in Play on next poll.')
" "$METRICS" "$MIN_FPS" | tee -a "$LOG"
  fi

  UI="$(unity command eval 'return GameDevTV.RTS.Prologue.ProloguePlaytestPerf.UiHealthJson();' --json --timeout 30 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin).get('data',{}).get('result',{}).get('result',''))" 2>/dev/null || true)"
  echo "  ui: $UI" | tee -a "$LOG"

  if [[ "$ec" -eq 0 ]]; then
    echo "WIN iteration $iter — $OUT_DIR" | tee -a "$LOG"
    exit 0
  fi

  bash "${ROOT}/tools/unity-editor-reset.sh" >/dev/null 2>&1 || true
  sleep 3
done