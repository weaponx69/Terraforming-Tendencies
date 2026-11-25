#!/usr/bin/env bash
# Run prologue-ui-playthrough in a loop until prologue completes (exit 0).
# Retries on stuck runs (exit 2) or contract cancel (exit 3).
#
# Usage:
#   ./tools/prologue-ui-playthrough-until-win.sh
#   MAX_ATTEMPTS=0 ./tools/prologue-ui-playthrough-until-win.sh   # unlimited (default)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# Default cap avoids runaway Play toggles; use OPT_IN_UNLIMITED=1 for MAX_ATTEMPTS=0.
if [[ "${OPT_IN_UNLIMITED:-0}" == "1" ]]; then
  MAX_ATTEMPTS="${MAX_ATTEMPTS:-0}"
else
  MAX_ATTEMPTS="${MAX_ATTEMPTS:-12}"
fi
RUN_LOG="${RUN_LOG:-/tmp/prologue-until-win.log}"
PLAY="${ROOT}/tools/prologue-ui-playthrough.sh"

: >"$RUN_LOG"

attempt=0
while true; do
  attempt=$((attempt + 1))
  if [[ "$MAX_ATTEMPTS" != "0" && "$attempt" -gt "$MAX_ATTEMPTS" ]]; then
    echo "Stopped after $MAX_ATTEMPTS attempts (no complete)." | tee -a "$RUN_LOG"
    exit 4
  fi

  stamp="$(date +%Y%m%d-%H%M%S)"
  export OUT_DIR="${OUT_DIR_PREFIX:-Assets/Playtest/win-loop}-${stamp}-a${attempt}"
  echo "=== attempt $attempt -> $OUT_DIR ===" | tee -a "$RUN_LOG"

  playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 30 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); r=d.get('data',{}).get('result',{}).get('result'); print('yes' if r is True or str(r).lower()=='true' else 'no')" 2>/dev/null || echo no)"
  skip_enter_play=1
  if [[ "$playing" != "yes" ]]; then
    echo "Play Mode off — using CLI enter Play for this attempt." | tee -a "$RUN_LOG"
    skip_enter_play=0
    bash "${ROOT}/tools/unity-editor-reset.sh" >/dev/null 2>&1 || true
  fi

  set +e
  skip_recompile=1
  SKIP_ENTER_PLAY="$skip_enter_play" SKIP_RECOMPILE="$skip_recompile" "$PLAY" 2>&1 | tee -a "$RUN_LOG"
  ec=${PIPESTATUS[0]}
  set -e

  if [[ "$ec" -eq 0 ]]; then
    echo "Prologue complete on attempt $attempt." | tee -a "$RUN_LOG"
    echo "Log: $RUN_LOG"
    echo "Last run: $OUT_DIR"
    exit 0
  fi

  NOTIFY="${ROOT}/tools/notify-player-help.sh"
  case "$ec" in
    6)
      "$NOTIFY" "Attempt $attempt: RAM over PROLOGUE_RAM_LIMIT_MB. Cooling down before retry." | tee -a "$RUN_LOG"
      bash "${ROOT}/tools/unity-editor-reset.sh" >/dev/null 2>&1 || true
      sleep 30
      ;;
    2|3)
      if (( attempt % 5 == 0 )); then
        "$NOTIFY" "Prologue loop attempt $attempt failed (exit $ec). Still retrying — see $RUN_LOG" | tee -a "$RUN_LOG"
      fi
      sleep 5
      ;;
    *)
      echo "Attempt $attempt failed (exit $ec); rebootstrapping next run..." | tee -a "$RUN_LOG"
      sleep 2
      ;;
  esac
done