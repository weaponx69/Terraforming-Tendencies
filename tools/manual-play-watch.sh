#!/usr/bin/env bash
# Passive lag watch: screenshots + FPS while YOU play. Does not press Play or drive input.
#
# Usage:
#   ./tools/manual-play-watch.sh
#   INTERVAL_SEC=2 DURATION_SEC=600 OUT_DIR=Assets/Playtest/manual-watch-foo ./tools/manual-play-watch.sh
#
# Tips:
#   - One Game tab visible (Layout > Default helps).
#   - Press Play on Terraforming-Tendencies; watcher starts capturing when Play is active.
#   - Ctrl+C stops early; writes summary to metrics.jsonl + frames under OUT_DIR.
#
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# CLI eval each tick stalls the main thread — default slow interval for manual play.
INTERVAL_SEC="${INTERVAL_SEC:-8}"
DURATION_SEC="${DURATION_SEC:-900}"
WAIT_PLAY_SEC="${WAIT_PLAY_SEC:-600}"
OUT_DIR="${OUT_DIR:-Assets/Playtest/manual-watch-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT_DIR"

if command -v import >/dev/null; then
  grab_crop() { import -window "$1" -crop "$2" "$3"; }
elif command -v scrot >/dev/null; then
  grab_crop() { scrot -a "$2" "$3" 2>/dev/null || scrot "$3"; }
else
  echo "Need ImageMagick import or scrot."
  exit 1
fi

command -v xdotool >/dev/null || { echo "xdotool required"; exit 1; }
command -v unity >/dev/null || { echo "unity CLI required"; exit 1; }
command -v python3 >/dev/null || { echo "python3 required"; exit 1; }

eval_json() {
  unity command eval "$1" --json --timeout "${2:-60}" 2>/dev/null
}

extract_result() {
  python3 -c '
import sys, json
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print("")
    sys.exit(0)
if not d.get("success"):
    print("")
    sys.exit(0)
data = d.get("data")
if not isinstance(data, dict):
    print("")
    sys.exit(0)
r = data.get("result") or {}
if not isinstance(r, dict):
    print(r)
    sys.exit(0)
print(r.get("result", r.get("output", "")))
'
}

is_playing() {
  local raw
  raw="$(eval_json 'return UnityEditor.EditorApplication.isPlaying;' 30)"
  local r
  r="$(extract_result <<<"$raw")"
  [[ "$r" == "True" || "$r" == "true" ]]
}

unity_status="$(unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['data']['instances'][0]['state'] if d.get('data',{}).get('instances') else 'none')")"
if [[ "$unity_status" != "ready" ]]; then
  echo "Editor not ready (state=$unity_status)."
  exit 1
fi

WIN_ID=""
for pattern in "Terraforming Tendencies" "Unity Editor" "Unity 6" "Unity"; do
  WIN_ID="$(xdotool search --name "$pattern" 2>/dev/null | head -1 || true)"
  [[ -n "$WIN_ID" ]] && break
done
if [[ -z "$WIN_ID" ]]; then
  echo "Could not find Unity window."
  exit 1
fi
echo "Watching Unity window id=$WIN_ID → $OUT_DIR"
echo "Press Play when ready (waiting up to ${WAIT_PLAY_SEC}s for Play Mode)..."

elapsed=0
until is_playing; do
  sleep 2
  elapsed=$((elapsed + 2))
  if (( elapsed >= WAIT_PLAY_SEC )); then
    echo "Timed out waiting for Play Mode."
    exit 2
  fi
  if (( elapsed % 20 == 0 )); then
    echo "  still waiting for Play... ${elapsed}s"
  fi
done
echo "Play active — capturing every ${INTERVAL_SEC}s for up to ${DURATION_SEC}s (Ctrl+C to stop)."

METRICS="$OUT_DIR/metrics.jsonl"
: >"$METRICS"
frame=0
end_at=$((SECONDS + DURATION_SEC))

play_rect_py='
import sys, json, re
raw = sys.stdin.read()
m = re.search(r"rect=(\d+),(\d+),(\d+),(\d+)", raw)
if not m:
    print("")
    sys.exit(0)
x, y, w, h = (int(m.group(i)) for i in range(1, 5))
if w < 80 or h < 80:
    print("")
    sys.exit(0)
print(f"{w}x{h}+{x}+{y}")
'

metrics_code='
int fps = GameDevTV.RTS.MainGame.MainGameFpsMonitor.LastSecondFps;
int smooth = GameDevTV.RTS.MainGame.MainGamePerformanceBudget.SmoothedFps();
bool prologue = GameDevTV.RTS.MainGame.MainGameController.IsActive;
int step = GameDevTV.RTS.MainGame.MainGameController.Instance?.Colony?.MainGameStep ?? -1;
int week = GameDevTV.RTS.MainGame.MainGameController.Instance?.Colony?.Week ?? -1;
long heap = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L);
return $"fps={fps} smooth={smooth} prologue={prologue} step={step} week={week} heapMb={heap} dtMs={UnityEngine.Time.unscaledDeltaTime * 1000f:0.0}";
'

rect_code='
#if UNITY_EDITOR
var asm = typeof(UnityEditor.Editor).Assembly;
var t = asm.GetType("UnityEditor.PlayModeView") ?? asm.GetType("UnityEditor.GameView");
if (t == null) return "rect=0,0,0,0";
var m = t.GetMethod("GetMainPlayModeViewRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
if (m == null) return "rect=0,0,0,0";
if (m.Invoke(null, null) is UnityEngine.Rect r)
    return $"rect={(int)r.x},{(int)r.y},{(int)r.width},{(int)r.height}";
#endif
return "rect=0,0,0,0";
'

while (( SECONDS < end_at )); do
  if ! is_playing; then
    echo "Play ended at frame $frame."
    break
  fi

  frame=$((frame + 1))
  ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  shot="$OUT_DIR/frame_$(printf '%04d' "$frame").jpg"

  rect_raw="$(eval_json "$rect_code" 45)"
  crop="$(extract_result <<<"$rect_raw" | python3 -c "$play_rect_py" 2>/dev/null || true)"

  if [[ -n "$crop" ]]; then
    grab_crop "$WIN_ID" "$crop" "$shot" || true
  else
  import -window "$WIN_ID" "$shot" 2>/dev/null || true
  fi

  met_raw="$(eval_json "$metrics_code" 45)"
  metrics="$(extract_result <<<"$met_raw")"
  printf '{"t":"%s","frame":%d,"file":"%s","metrics":"%s"}\n' "$ts" "$frame" "$(basename "$shot")" "$metrics" >>"$METRICS"

  echo "[$frame] $metrics → $(basename "$shot")"
  sleep "$INTERVAL_SEC"
done

python3 <<PY
import json, re, statistics
rows=[]
for line in open("$METRICS"):
    line=line.strip()
    if not line: continue
    rows.append(json.loads(line))
fps=[]
for r in rows:
    m=r.get("metrics","")
    mo=re.search(r"fps=(\d+)", m)
    if mo: fps.append(int(mo.group(1)))
print(f"frames={len(rows)} out=$OUT_DIR")
if fps:
    print(f"fps min={min(fps)} median={statistics.median(fps):.0f} mean={statistics.mean(fps):.1f} max={max(fps)}")
    print("pass_30fps" if statistics.median(fps) >= 30 else "below_30fps_median")
PY

echo "Done. Open $OUT_DIR/metrics.jsonl and frame_*.jpg"