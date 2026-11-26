#!/usr/bin/env bash
# Ground sharpness + FPS under load: filled prologue, heavy buildings, min/max zoom screenshots.
# Output: Assets/Playtest/ground-qc-<timestamp>/
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ITERATIONS="${ITERATIONS:-1}"
WAIT_SEC="${WAIT_SEC:-2.5}"
FPS_SAMPLES="${FPS_SAMPLES:-8}"
MIN_FPS="${MIN_FPS:-30}"
TS="$(date -u +%Y%m%dT%H%M%SZ)"
OUT_DIR="${OUT_DIR:-$ROOT/Assets/Playtest/ground-qc-$TS}"
mkdir -p "$OUT_DIR"

unity status --format json >/dev/null

extract_result() {
  python3 -c 'import json,sys; d=json.load(sys.stdin); print("" if not d.get("success") else d.get("data",{}).get("result",{}).get("result",""))'
}

echo "== recompile =="
unity command eval 'if(UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=false; return "stopped";' --json --timeout 60 >/dev/null 2>&1 || true
sleep 1
unity command recompile --json >/dev/null 2>&1 || true
for _ in $(seq 1 120); do
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

enter_play='if(!UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=true; return "play";'
unity command eval "$enter_play" --json >/dev/null 2>&1 || true
for _ in $(seq 1 90); do
  st=$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json 2>/dev/null \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["data"]["result"]["result"])' 2>/dev/null || echo False)
  [[ "$st" == "True" ]] && break
  sleep 2
done

"$ROOT/tools/prologue-perf-scenario.sh" >/dev/null 2>&1 || true
sleep 4

prep=$(unity command eval 'return GameDevTV.RTS.Diagnostics.PrologueGroundVisualQc.PrepareHeavyLoad();' --json --timeout 300 2>/dev/null \
  | extract_result || echo fail)
echo "prepare: $prep"
if [[ "$prep" != prepared* ]]; then
  echo "FAIL heavy-load prepare: $prep"
  exit 1
fi
sleep 12

unity command eval 'UnityEngine.Application.targetFrameRate=-1;' --json >/dev/null 2>&1 || true

summary="$OUT_DIR/summary.jsonl"
: >"$summary"
worst_fps=999

for iter in $(seq 1 "$ITERATIONS"); do
  for zoom in close mid far; do
    sample=$(unity command eval "return GameDevTV.RTS.Diagnostics.PrologueGroundVisualQc.SetZoomAndSample(\"$zoom\");" --json --timeout 120 2>/dev/null \
      | extract_result || echo '{}')
    sleep "$WAIT_SEC"
    fps_list=()
    for _ in $(seq 1 "$FPS_SAMPLES"); do
      f=$(unity command eval 'return GameDevTV.RTS.Prologue.PrologueFpsMonitor.LastSecondFps;' --json --timeout 30 2>/dev/null \
        | python3 -c 'import json,sys; print(json.load(sys.stdin)["data"]["result"]["result"])' 2>/dev/null || echo 0)
      fps_list+=("$f")
      sleep 0.8
    done
  fps_med=$(printf '%s\n' "${fps_list[@]}" | python3 -c 'import sys; v=sorted(int(x) for x in sys.stdin if x.strip().isdigit()); print(v[len(v)//2] if v else 0)')
    if [ "$fps_med" -lt "$worst_fps" ]; then worst_fps=$fps_med; fi
    shot="$OUT_DIR/iter${iter}_${zoom}.png"
    unity command screenshot --output "$shot" --width 1920 --height 1080 2>/dev/null || true
    diag=$(unity command eval 'return GameDevTV.RTS.Prologue.PrologueGroundVisualTuning.DiagnosticsLine();' --json --timeout 30 2>/dev/null \
      | extract_result || echo "")
    SAMPLE_JSON="$sample" DIAG="$diag" python3 -c 'import json,os; s=os.environ.get("SAMPLE_JSON") or "{}"; 
try: sample=json.loads(s)
except json.JSONDecodeError: sample={"raw":s}
print(json.dumps({"iter":'"$iter"',"zoom":"'"$zoom"'","fpsMedian":'"$fps_med"',"sample":sample,"diag":os.environ.get("DIAG",""),"shot":"'"$shot"'"}))' >>"$summary"
    echo "iter=$iter zoom=$zoom fps=$fps_med -> $(basename "$shot")"
  done
done

unity command eval 'UnityEngine.Application.targetFrameRate=60;' --json >/dev/null 2>&1 || true

echo "Wrote $OUT_DIR (worst fps median=$worst_fps)"
if [ "$worst_fps" -ge "$MIN_FPS" ]; then
  echo "PASS fps>=$MIN_FPS"
  exit 0
fi
echo "FAIL fps below $MIN_FPS under load"
exit 1