#!/usr/bin/env bash
# Fresh main-game run + JSON probe + zoom screenshots (opening survey / command post).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT_DIR="${OUT_DIR:-Assets/Playtest/opening-diagnostic-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT_DIR"

eval_result() {
  python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('data',{}).get('result',{}).get('result',''))" 2>/dev/null || true
}

unity command eval 'if(UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=false;' --json --timeout 90 >/dev/null 2>&1 || true
sleep 2
unity command eval 'UnityEditor.EditorApplication.isPlaying=true; return "play";' --json --timeout 120 >/dev/null || true

for _ in $(seq 1 120); do
  playing="$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json --timeout 60 2>/dev/null | eval_result || true)"
  [[ "$playing" == "True" ]] && break
  sleep 2
done

sleep 8
unity command eval 'GameDevTV.RTS.MainGame.MainGameController.Instance?.BeginNewMainGameRunForPlaytest(); return "boot";' --json --timeout 180 >/dev/null || true
sleep 6

for attempt in 1 2 3; do
  snap="$(unity command eval 'return GameDevTV.RTS.Diagnostics.MainGameGroundVisualQc.OpeningSnapshot();' --json --timeout 120 2>/dev/null | eval_result)"
  echo "$snap" | tee "$OUT_DIR/snapshot-attempt-$attempt.json"
  unity command screenshot --output "$OUT_DIR/boot-camp-$attempt.png" --width 1920 --height 1080 2>/dev/null || true
  dx="$(echo "$snap" | python3 -c "import sys,json; d=json.loads(sys.stdin.read()); print(abs(d.get('campScreenDx',999)))" 2>/dev/null || echo 999)"
  dy="$(echo "$snap" | python3 -c "import sys,json; d=json.loads(sys.stdin.read()); print(abs(d.get('campScreenDy',999)))" 2>/dev/null || echo 999)"
  if python3 -c "import sys; dx=float(sys.argv[1]); dy=float(sys.argv[2]); sys.exit(0 if dx<24 and dy<24 else 1)" "$dx" "$dy" 2>/dev/null; then
    cp "$OUT_DIR/snapshot-attempt-$attempt.json" "$OUT_DIR/snapshot.json"
    break
  fi
  unity command eval 'var c=GameDevTV.RTS.MainGame.MainGameController.Instance; if(c!=null && c.TryGetCell(c.ResolveHomeCell(), out var cell)) GameDevTV.RTS.Player.PlayerInput.Instance?.FrameMainGameStartOnCamp(GameDevTV.RTS.Player.ColonyTileGrid.CellToWorldOnHighestGround(cell)); return "reframe";' --json --timeout 90 >/dev/null 2>&1 || true
  sleep 2
done
[[ -f "$OUT_DIR/snapshot.json" ]] || cp "$OUT_DIR/snapshot-attempt-3.json" "$OUT_DIR/snapshot.json" 2>/dev/null || true

for label in min mid max; do
  lab=$([ "$label" = min ] && echo close || ([ "$label" = max ] && echo far || echo mid))
  unity command eval "return GameDevTV.RTS.Diagnostics.MainGameGroundVisualQc.SetZoomAndSample(\"$lab\");" --json --timeout 90 >/dev/null 2>&1 || true
  sleep 1
  shot="$OUT_DIR/zoom-$label.png"
  unity command screenshot --output "$shot" --width 1920 --height 1080 2>/dev/null || true
  echo "screenshot: $shot"
done

probe="$(unity command eval 'return GameDevTV.RTS.Diagnostics.MainGameGroundVisualQc.RunOrbitalSurveyProbe();' --json --timeout 120 2>/dev/null | eval_result)"
echo "$probe" | tee "$OUT_DIR/orbital-survey-probe.json"

unity command screenshot --output "$OUT_DIR/after-survey-route.png" --width 1920 --height 1080 2>/dev/null || true

echo "Done -> $OUT_DIR"