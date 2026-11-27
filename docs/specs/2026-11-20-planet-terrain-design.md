# Design: Per-planet procedural Unity Terrain

**Date:** 2026-11-20  
**Status:** Superseded (Easy visuals) by [2026-11-21-planet-surface-dem-design.md](2026-11-21-planet-surface-dem-design.md) — grounding/mesh sampling ideas may still apply  
**Engine:** Unity MainGame / planet gen

## Summary

Replace the flat planet mesh look with a **seeded Unity Terrain** per planet type. Landforms (hills / valleys / mountains) are stable for a planet seed; **resource nodes stay randomly scattered** each run and snap to terrain height.

## Goals

- Look like a real planetary surface (relief + lit slopes), not a billiard table.
- Same planet config + terrain seed → same height field.
- Gatherable / feature / ice pads still random per run (existing `PlanetConfig` deny-list + scatter).
- Colony / hex placement remains playable: **gentle slopes near landing**; steeper relief farther out.

## Non-goals (v1)

- Hand-authored unique heightmaps per campaign mission  
- Caves / overhangs  
- Full TerrainLayer biome painting / trees (can follow)  
- Seamless toroidal Terrain wrap ghosts (flat wrap ghosts may remain for NavMesh edge cases)

## Design

### Terrain

- At `PlanetGenerator.GeneratePlanet`, build (or rebuild) a child `Terrain` covering `MapWidth×MapHeight×CellSize`.
- Height from layered Perlin noise using `PlanetConfig.NoiseScale`, `HeightMultiplier`, and a **terrain seed** (`PlanetConfig.TerrainSeed`, 0 = derive from asset name).
- Optional baked `TerrainData` asset on the config later; v1 is runtime generate from seed.
- Flatten a disk around `StartingAreaCenter` / sector 0 so CP + early Survey tiles stay nearly flat.
- Put Terrain on layer `Terrain` so existing deposit raycasts hit it.

### Resources

- Unchanged scatter rules; after position pick, set `y` from `Terrain.SampleHeight` (or raycast).
- Visual mesh of the old flat plane: hide/disable renderer on the generator (collider may remain for wrap ghosts) so Terrain is what the player sees.

### Playability

- Max slope near home kept low (flatten radius ~ sector occupation / CP keepout × scale).
- NavMesh rebake after Terrain exists (existing `BakeAllNavMeshes`).

## Success

- Play Mode: visible hills/valleys away from landing; deposits sit on the surface; MainGame can still place Aquifer / Survey.
