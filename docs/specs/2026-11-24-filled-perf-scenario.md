# Filled perf scenario (vertical slice)

## Purpose

Soak-test **FPS and panning** with a mid-run colony: surveyed trail, finished buildings, fewer draw calls than cube dirt roads + full reveal meshes.

## How to run

1. Enter Play on `Terraforming-Tendencies`.
2. Unity CLI or eval:

   `MainGameController.Instance.BeginFilledPerfRun();`

3. Or: `./tools/main-game-perf-scenario.sh` (enters Play unless `SKIP_ENTER_PLAY=1`).

Env var `TT_PERF_FILLED_RUN=1` before bootstrap also applies the scenario on the next `BeginNewMainGameRunForPlaytest()`.

## What it does

- BFS survey path from Command Post toward nearest ice; reveals fog along path.
- **Baked path overlay** — one ground quad + texture (`MainGamePathGroundOverlay`), no `DirtRoad_*` cubes.
- **Silhouette reveal ghosts** — low-vertex hex footprint + frustum culling (`MainGameRevealGhosts`).
- Board + world: Aquifer, Oxygen Processor, two Habitats (instant complete).

## Default slice perf (all runs)

On planet sync, the slice enables **silhouette reveal ghosts** (no 3D name labels). **Baked path overlay** stays opt-in (`BeginFilledPerfRun` / `TT_PERF_FILLED_RUN=1`). MainGame also:

- Skips `BaseBuilding.Update` climate ticks and **does not spawn** world-space health bars.
- **Construction:** scaffold only until complete — **no per-material clone/fade** during build.
- **One shared scaffold mesh/material** per build (`ConstructionScaffold`).
- **One** construction tick from `MainGameTimeLapse` (not per-building `Update`).
- **`OxygenProcessor.Update`** off in the slice (O2 is week-based on `MainGameColony`).

## Tests

`Assets/Scripts/Tests/MainGamePerfScenarioTests.cs` — pathfinding only (rules/math).