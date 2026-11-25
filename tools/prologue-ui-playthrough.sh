#!/usr/bin/env bash
# Canonical vertical-slice verification (main game prototype): bootstrap + player UI.
# Prologue naming is packaging, not a separate product — every run bootstraps fresh world state.
#
# Prerequisites:
#   - Unity Editor open on this project (Pipeline connected)
#   - xdotool, ImageMagick import (or scrot), python3
#   - Game view visible; for best hit-testing use Maximize on Play or fullscreen Game tab
#
# Usage:
#   ./tools/prologue-ui-playthrough.sh
#   OUT_DIR=Assets/Playtest/my-run ./tools/prologue-ui-playthrough.sh
#
# Recommended (avoids Editor OOM / 6-minute Play load):
#   1. Press Play in Unity yourself (Terraforming-Tendencies scene).
#   2. SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 ./tools/prologue-ui-playthrough.sh
#
# If the Game view shows "No cameras rendering" or several stacked game panes:
#   Stop Play, close extra Game tabs, Layout > Default, one Game view, Play once.
#
# Env:
#   SKIP_RECOMPILE=1     — do not domain-reload before the run (Play Mode stays up).
#   SKIP_ENTER_PLAY=1    — never call EditorApplication.isPlaying=true; poll until you started Play.
#   SKIP_BOOTSTRAP=1     — skip BeginNewPrologueRunForPlaytest (continue current run).
#   FORCE_RECOMPILE=1    — recompile even when Play Mode is already active.
#   PROLOGUE_RAM_LIMIT_MB  — abort if Unity Editor RSS exceeds this (default 12288 ≈ 12 GiB).
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# One automation run at a time (prevents stacked Play toggles / multiple Game views).
PLAY_LOCK="/tmp/prologue-ui-playthrough.lock"
exec 9>"$PLAY_LOCK"
if ! flock -n 9; then
  echo "Another prologue playthrough is already running (lock: $PLAY_LOCK)."
  echo "If this is stale: rm -f $PLAY_LOCK"
  exit 7
fi

OUT_DIR="${OUT_DIR:-Assets/Playtest/ui-prologue-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT_DIR"
LOG="$OUT_DIR/steps.jsonl"
SHOT=0
PROLOGUE_RAM_LIMIT_MB="${PROLOGUE_RAM_LIMIT_MB:-12288}"
NOTIFY="$ROOT/tools/notify-player-help.sh"

command -v unity >/dev/null || { echo "unity CLI not found"; exit 1; }
command -v xdotool >/dev/null || { echo "xdotool not found"; exit 1; }
command -v python3 >/dev/null || { echo "python3 not found"; exit 1; }

if command -v import >/dev/null; then
  grab_window() { import -window "$1" "$2"; }
elif command -v scrot >/dev/null; then
  grab_window() { scrot -u "$2" 2>/dev/null || scrot "$2"; }
else
  echo "Need ImageMagick 'import' or scrot for screenshots."
  exit 1
fi

eval_json() {
  local code="$1"
  local timeout="${2:-120}"
  unity command eval "$code" --json --timeout "$timeout"
}

unity_editor_pid() {
  unity status --format json 2>/dev/null | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print(d['data']['instances'][0]['pid'])
except Exception:
    pass
" || true
}

unity_editor_rss_mb() {
  local pid="${1:-$(unity_editor_pid)}"
  [[ -z "$pid" || ! -r "/proc/$pid/status" ]] && echo "0" && return
  awk '/^VmRSS:/ {print int($2/1024)}' "/proc/$pid/status"
}

abort_if_ram_over_limit() {
  local rss
  rss="$(unity_editor_rss_mb)"
  if [[ "$rss" -gt "$PROLOGUE_RAM_LIMIT_MB" ]]; then
    echo "RAM limit exceeded: Editor RSS ${rss} MB > ${PROLOGUE_RAM_LIMIT_MB} MB"
    "$NOTIFY" "Playtest stopped: Unity RAM ${rss}MB over limit ${PROLOGUE_RAM_LIMIT_MB}MB. Stop other apps or raise PROLOGUE_RAM_LIMIT_MB."
    unity command eval 'if(UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=false;' --json --timeout 60 >/dev/null || true
    exit 6
  fi
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

# True when eval succeeded and Editor is in Play Mode.
is_playing() {
  local raw
  raw="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 120 2>&1 || true)"
  python3 -c '
import sys, json
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except json.JSONDecodeError:
    print("eval_error")
    sys.exit(0)
if not d.get("success"):
    print("eval_error")
    sys.exit(0)
r = d.get("data", {}).get("result", {}).get("result")
if r is True or str(r).lower() == "true":
    print("yes")
else:
    print("no")
' <<<"$raw"
}

print_eval_error() {
  local raw="$1"
  python3 -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
except Exception:
    print(sys.stdin.read()[:500])
    sys.exit(0)
if not d.get("success"):
    for e in d.get("errors") or []:
        print(e.get("message", e))
' <<<"$raw"
}

ensure_play_mode() {
  if [[ "$(is_playing)" == "yes" ]]; then
    echo "Play Mode already active."
    return 0
  fi

  if [[ "${SKIP_ENTER_PLAY:-0}" == "1" ]]; then
    echo "SKIP_ENTER_PLAY=1 — waiting for you to press Play in the Editor..."
    for i in $(seq 1 90); do
      if [[ "$(is_playing)" == "yes" ]]; then
        echo "Play Mode active (~${i}×2s wait)."
        return 0
      fi
      if [[ $((i % 5)) -eq 0 ]]; then
        echo "  waiting for Play... ~$((i * 2))s"
      fi
      sleep 2
    done
    echo "Play Mode not active after ~3 minutes."
    echo "Press Play on Assets/Scenes/Terraforming-Tendencies.unity, then re-run with SKIP_ENTER_PLAY=1."
    return 1
  fi

  echo "Requesting Play Mode (Terraforming-Tendencies — load can take several minutes)..."
  echo "(Tip: press Play yourself and use SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 to avoid this path.)"
  local req
  req="$(eval_json '
var path = "Assets/Scenes/Terraforming-Tendencies.unity";
if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != path)
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
    return "wait_compile";
UnityEditor.EditorApplication.isPlaying = true;
return "requested";
' 45 2>&1 || true)"
  local tag
  tag="$(extract_result_text <<<"$req")"
  if [[ "$tag" == *eval_error* ]] || [[ "$tag" == *COMMAND_FAILED* ]] || [[ "$tag" == *timed* ]]; then
    print_eval_error "$req"
    echo "(Non-fatal: will keep polling — main thread may still be entering Play Mode.)"
  else
    echo "Play request: $tag"
  fi

  for i in $(seq 1 180); do
    if [[ "$(is_playing)" == "yes" ]]; then
      echo "Play Mode active (~${i}×2s wait)."
      return 0
    fi
    if [[ $((i % 10)) -eq 0 ]]; then
      echo "  still entering Play Mode... ~$((i * 2))s (close heavy apps if this exceeds ~5 min)"
    fi
    sleep 2
  done

  echo "Could not enter Play Mode after ~6 minutes."
  echo "Check Unity Console for compile errors, or enter Play manually once, then re-run."
  return 1
}

unity_status="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$unity_status" != "ready" ]]; then
  echo "Editor not ready (state=$unity_status). Open the project and wait for Pipeline."
  exit 1
fi

already_playing="no"
if [[ "$(is_playing)" == "yes" ]]; then
  already_playing="yes"
fi

if [[ "${SKIP_RECOMPILE:-0}" == "1" ]]; then
  echo "== recompile skipped (SKIP_RECOMPILE=1) =="
elif [[ "${SKIP_RECOMPILE:-0}" == "0" && "$already_playing" == "yes" ]]; then
  echo "== recompile (SKIP_RECOMPILE=0 — exiting Play for domain reload) =="
  unity command eval 'if(UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=false; return "stopped";' --json --timeout 60 >/dev/null || true
  sleep 2
  unity command recompile --json >/dev/null || true
  for i in $(seq 1 90); do
    RS="$(unity command recompile_status --json 2>/dev/null || true)"
    if echo "$RS" | grep -q '"status":"completed"'; then
      if echo "$RS" | grep -q '"failed":true'; then
        echo "Recompile failed: $RS"
        exit 1
      fi
      break
    fi
    sleep 1
  done
elif [[ "$already_playing" == "yes" && "${FORCE_RECOMPILE:-0}" != "1" ]]; then
  echo "== recompile skipped (Play Mode active — use FORCE_RECOMPILE=1 to reload scripts) =="
else
  echo "== recompile =="
  unity command recompile --json >/dev/null || true
  for i in $(seq 1 90); do
    RS="$(unity command recompile_status --json 2>/dev/null || true)"
    if echo "$RS" | grep -q '"status":"completed"'; then
      if echo "$RS" | grep -q '"failed":true'; then
        echo "Recompile failed: $RS"
        exit 1
      fi
      break
    fi
    sleep 1
  done
fi

WIN_ID=""
for pattern in "Terraforming Tendencies" "Unity Editor" "Unity 6" "Unity"; do
  WIN_ID="$(xdotool search --name "$pattern" 2>/dev/null | head -1 || true)"
  [[ -n "$WIN_ID" ]] && break
done
if [[ -z "$WIN_ID" ]]; then
  echo "Could not find Unity window (xdotool search)."
  exit 1
fi
echo "Unity window id=$WIN_ID"

echo "== enter Play Mode =="
ensure_play_mode || exit 1

echo "== wait for Prologue =="
for i in $(seq 1 180); do
  ST="$(eval_json 'if(!UnityEditor.EditorApplication.isPlaying) return "notplaying"; return GameDevTV.RTS.Prologue.PrologueController.IsActive ? "ready" : "waiting";' 60 | extract_result_text)"
  echo "  wait $i: $ST"
  [[ "$ST" == "ready" ]] && break
  sleep 2
done
if [[ "$ST" != "ready" ]]; then
  echo "Prologue did not become active."
  "$NOTIFY" "Prologue playtest: PrologueController never became active after wait. Run tools/unity-editor-reset.sh or check Game view camera."
  exit 2
fi

if [[ "${SKIP_BOOTSTRAP:-0}" == "1" ]]; then
  echo "== fresh prologue skipped (SKIP_BOOTSTRAP=1) =="
else
  echo "== fresh prologue (bootstrap) =="
  eval_json 'var c=GameDevTV.RTS.Prologue.PrologueController.Instance; if(c==null) return "error:no_controller"; c.BeginNewPrologueRunForPlaytest(); return "bootstrapped";' 120 | extract_result_text
fi

echo "== UI playthrough -> $OUT_DIR =="
xdotool windowactivate --sync "$WIN_ID" 2>/dev/null || true
sleep 0.5

FINAL=""
METRICS_LOG="$OUT_DIR/metrics.jsonl"
: >"$METRICS_LOG"
for i in $(seq 1 400); do
  if (( i == 1 || i % 4 == 0 )); then
    abort_if_ram_over_limit
  fi
  RAW="$(eval_json 'return GameDevTV.RTS.Prologue.PrologueUiPlaythrough.StepJson();' 90 | extract_result_text)"
  read -r STATUS PHASE X Y WAIT SHOT_FLAG CSTEP WEEK MSG CLICK_METHOD FPS HEAP <<<"$(python3 -c "
import json, sys
s = sys.stdin.read().strip()
try:
    d = json.loads(s)
except Exception:
    print('parse_error', 'parse_error', 0, 0, 2000, 'false', -1, -1, s[:80], 'xdotool', 0, 0)
    sys.exit(0)
print(
    d.get('status','?'),
    d.get('phase',''),
    d.get('xdotoolX',0),
    d.get('xdotoolY',0),
    d.get('waitMs',500),
    'true' if d.get('screenshot') else 'false',
    d.get('colonyStep',-1),
    d.get('week',-1),
    d.get('message','').replace(' ', '_')[:40],
    d.get('clickMethod','xdotool'),
    d.get('fps',0),
    d.get('heapMb',0),
)
" <<<"$RAW")"

  RSS="$(unity_editor_rss_mb)"
  if (( i % 10 == 0 )); then
    echo "{\"step\":$i,\"fps\":$FPS,\"heapMb\":$HEAP,\"editorRssMb\":$RSS}" >>"$METRICS_LOG"
    echo "  [metrics] fps=$FPS heap=${HEAP}MB editor_rss=${RSS}MB"
  fi

  echo "$RAW" >>"$LOG"
  FINAL="$RAW"
  echo "  [$i] $STATUS / $PHASE step=$CSTEP week=$WEEK — $MSG"

  if [[ "$SHOT_FLAG" == "true" ]]; then
    SHOT=$((SHOT + 1))
    FMT=$(printf "%03d" "$SHOT")
    grab_window "$WIN_ID" "$OUT_DIR/${FMT}_${PHASE}.jpg" || true
  fi

  if [[ "$STATUS" == "click" && "$CLICK_METHOD" != "editor" ]]; then
    xdotool windowactivate --sync "$WIN_ID" 2>/dev/null || true
    xdotool mousemove --sync "$X" "$Y"
    xdotool click 1
  fi

  if [[ "$STATUS" == "error" && "$PHASE" == "stuck" ]]; then
    "$NOTIFY" "Playtest stuck at phase=$PHASE step=$CSTEP week=$WEEK — check $LOG"
  fi

  if [[ "$STATUS" == "done" || "$STATUS" == "error" ]]; then
    grab_window "$WIN_ID" "$OUT_DIR/final.jpg" || true
    break
  fi

  MS=$((WAIT > 0 ? WAIT : 400))
  sleep "$(python3 -c "print(max(0.05, $MS/1000.0))")"
done

echo ""
echo "Log: $LOG"
echo "Screenshots: $OUT_DIR (${SHOT} before clicks + final)"
echo "Automation log:"
eval_json 'return GameDevTV.RTS.Prologue.PrologueUiPlaythrough.StatusLog();' 60 | extract_result_text

if echo "$FINAL" | grep -q '"status":"done"'; then
  if echo "$FINAL" | grep -q '"phase":"cancelled"'; then
    echo "Finished: contract cancelled (see final.jpg)."
    exit 3
  fi
  echo "Finished: prologue complete."
  exit 0
fi

echo "No done status — check shots and steps.jsonl."
exit 2