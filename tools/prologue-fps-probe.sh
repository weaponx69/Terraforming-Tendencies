#!/usr/bin/env bash
# Sample in-game FPS while Prologue is active (Unity CLI). Requires Editor in Play on main scene.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
SAMPLES="${SAMPLES:-40}"
WAIT="${WAIT_SEC:-1.05}"
unity status --format json | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['data']['instances'][0]['state']=='ready'" >/dev/null
playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 20 2>/dev/null | python3 -c "import sys,json; r=json.load(sys.stdin).get('data',{}).get('result',{}).get('result'); print('yes' if r is True else 'no')" || echo no)"
if [[ "$playing" != "yes" ]]; then
  echo "Enter Play on Assets/Scenes/Terraforming-Tendencies.unity first (or run playthrough bootstrap)."
  exit 2
fi
echo "Warm-up 3s (FPS counter uses 1s windows)..."
sleep 3
: > /tmp/prologue-fps-samples.txt
for i in $(seq 1 "$SAMPLES"); do
  fps="$(unity command eval 'return GameDevTV.RTS.Prologue.PrologueFpsMonitor.LastSecondFps;' --json --timeout 20 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin).get('data',{}).get('result',{}).get('result',0))" || echo 0)"
  echo "$fps" >> /tmp/prologue-fps-samples.txt
  sleep "$WAIT"
done
python3 <<'PY'
import statistics
vals=[int(x) for x in open("/tmp/prologue-fps-samples.txt") if x.strip().isdigit()]
if not vals:
    print("no samples"); raise SystemExit(1)
print(f"samples={len(vals)} min={min(vals)} median={statistics.median(vals):.0f} mean={statistics.mean(vals):.1f} max={max(vals)}")
print("pass_30fps" if statistics.median(vals) >= 30 else "below_30fps_median")
PY