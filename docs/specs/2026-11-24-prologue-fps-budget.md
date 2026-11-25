# Prologue FPS budget

**Date:** 2026-11-24  
**Target:** ≥ **30 FPS median** in Editor Play on the vertical slice (Prologue step 0–6).

## Root causes (measured under bot playthrough)

| Hot spot | Cost | Mitigation |
|----------|------|------------|
| `MinimapUI.LateUpdate` | Full 256×256 `Texture2D` rebuild **every frame** | Suspend during Prologue; **5 Hz** cap elsewhere |
| `PrologueHUD.Update` | `FindObjectsByType` + full `Refresh` every frame | Chrome suppress **0.5s**; HUD refresh **~8 Hz** |
| `HexGridManager.Update` | `FindObjectsByType` on commandables 4×/s | **Skip** while Prologue active |
| Quality defaults | PC tier + long shadows on dense planet mesh | `ProloguePerformanceBudget` → Mobile-ish tier, shadow distance cap |
| `TerrainWrapStreamer` | Up to **8** full planet mesh copies drawn near camera | **Off** during Prologue |
| Planet mesh | 200×200 cells ≈ 40k verts | Easy config **128×128** (~16k verts) for slice perf |
| `RuntimeUI` / `PrologueHUD` | `FindObjects` + full refresh **every frame** | Throttled 0.12–0.75s |

## APIs

- [`ProloguePerformanceBudget`](../../Assets/Scripts/Prologue/ProloguePerformanceBudget.cs) — applied when planet sync completes.
- [`tools/prologue-fps-probe.sh`](../../tools/prologue-fps-probe.sh) — CLI median FPS sample in Play.

## Verify

```bash
# After Play + prologue bootstrapped:
./tools/prologue-fps-probe.sh

# Or full slice with metrics:
SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 OUT_DIR=Assets/Playtest/fps-check ./tools/prologue-ui-playthrough.sh
# Inspect $OUT_DIR/metrics.jsonl fps field
```