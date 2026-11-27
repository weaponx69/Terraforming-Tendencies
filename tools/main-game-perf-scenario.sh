#!/usr/bin/env bash
# Bootstrap the vertical slice with a filled colony: baked path overlay, buildings, light ghosts.
# Use to compare FPS vs a fresh landing (fewer road cubes, silhouette reveal ghosts).
#
# Prerequisites: Unity Editor connected, Play optional (script enters Play if needed).
#
# Usage:
#   ./tools/main-game-perf-scenario.sh
#   SKIP_ENTER_PLAY=1 ./tools/main-game-perf-scenario.sh
#
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

unity status --format json >/dev/null

enter_play='if(!UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying=true; return "enter";'
if [[ "${SKIP_ENTER_PLAY:-0}" != "1" ]]; then
  unity command eval "$enter_play" --json >/dev/null || true
  for _ in $(seq 1 90); do
    st=$(unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --json 2>/dev/null | python3 -c 'import json,sys; print(json.load(sys.stdin)["data"]["result"]["result"])' 2>/dev/null || echo False)
    [[ "$st" == "True" ]] && break
    sleep 2
  done
fi

code='var c=GameDevTV.RTS.MainGame.MainGameController.Instance;
if(c==null){GameObject.FindObjectsOfType<UnityEngine.MonoBehaviour>();
 c=UnityEngine.Object.FindAnyObjectByType<GameDevTV.RTS.MainGame.MainGameController>();}
if(c==null) return "error:no_controller";
c.BeginFilledPerfRun();
return "filled";'
unity command eval "$code" --json | python3 -c '
import json,sys
d=json.load(sys.stdin)
r=d.get("data") or {}
res=(r.get("result") or {})
print(res.get("result", res))
'

code2='var o=GameDevTV.RTS.MainGame.MainGamePathGroundOverlay.Instance;
var r=GameDevTV.RTS.MainGame.MainGameSearchedTiles.UseBakedPathOverlay;
var roads=UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None);
int cubes=0;
foreach(var mr in roads){if(mr!=null&&mr.gameObject.name.StartsWith("DirtRoad")) cubes++;}
return $"baked={r} overlay={(o!=null)} dirtCubes={cubes} fps={GameDevTV.RTS.MainGame.MainGamePerformanceBudget.SmoothedFps()}";'
unity command eval "$code2" --json | python3 -c '
import json,sys
d=json.load(sys.stdin)
r=d.get("data") or {}
res=(r.get("result") or {})
print("verify:", res.get("result", res))
'