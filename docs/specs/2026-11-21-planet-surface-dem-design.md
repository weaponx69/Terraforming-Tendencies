# Design: Planet surface from real DEM + photo textures

**Date:** 2026-11-21  
**Status:** Approved  
**Engine:** Unity MainGame / `PlanetGenerator`  
**Canonical path:** `docs/specs/` (Unity docs — not `.summer`)  
**Supersedes (look/height source):** [2026-11-20-planet-terrain-design.md](2026-11-20-planet-terrain-design.md) for Easy planet visuals — keep mesh height sampling / grounding from that work; **replace Perlin landforms** with curated DEM + photo albedo.

**Visual target:** `Assets/Art/References/PlanetSurface/planet-before-after-dem.jpg` (**After**) and `planet-look-colony-plains.jpg` — dusty Mars plains, flat camp area, natural ridges farther out (not smooth blob hills).

## Summary

Easy planet ground uses a **curated elevation map (DEM)** for shape and the terrain photo plus its normal for the hills and valleys. A repeating dust texture sits on top of that photo. Height is stable per planet asset. Resources stay random per run and snap to DEM height. Landing / early colony stays mostly flat.

## Goals

- Surface reads like photographed planetary terrain (After image), not plastic Perlin blobs.
- Plenty of **buildable flat ground** on Easy: pick a plains-leaning DEM crop + flatten landing disk.
- Keep existing grounding APIs: `ColonyTileGrid.SampleGroundY` / `ApplyGroundY` / `CellToWorldOnGround`.
- Ship first for **Planet 1 – Easy** / MainGame; other planets can reuse the same slots later.

## Non-goals (v1)

- Runtime download of DEM/imagery  
- Many random DEM crops per run (one curated crop per planet type)  
- Full multi-biome splat painting / vegetation  
- Changing MainGame rules, cards, or fog  
- Godot / `.summer` game tree assets (this is Unity; references live under `Assets/Art/…`)

## Design

### Authoring (offline)

1. Obtain public-domain Mars elevation (e.g. NASA / USGS MOLA or similar) and a matching or compatible dusty albedo photo.
2. Crop a **plains-leaning** region sized for Easy map (`MapWidth` × `MapHeight` world units at `CellSize`).
3. Downsample height to a texture the mesh can sample (power-of-two okay; bilinear sample at runtime).
4. Store under `Assets/Art/Planets/Easy/` with a short `SOURCES.md` citing NASA/USGS (or other) license/source URLs.
5. Assign textures on the Easy `PlanetConfig` asset (`Resources` / `Settings` as used today).

### `PlanetConfig` fields

| Field | Role |
|-------|------|
| `Heightmap` | Grayscale DEM texture (bright = high) |
| `HeightAmplitude` | World-unit vertical span black→white |
| `AlbedoMap` | Photo surface texture |
| `NormalMap` | Optional; skip if unavailable in v1 |
| `LandingFlatRadius` | Keep; flatten disk around landing |

`NoiseScale` / procedural Perlin are **fallback only** when `Heightmap` is null (boot safety). Easy ships with maps assigned.

### Runtime (`PlanetGenerator`)

1. Build / update the planet **mesh** heights by sampling `Heightmap` across vertices (same UV/world mapping as map extents).
2. Apply **landing flatten** (smoothstep disk) around `StartingAreaCenter` so CP / early Survey stay nearly flat.
3. Apply URP Lit (or current planet shader) with `AlbedoMap` (and normal if present); stop using the flat height-gradient-only look as the primary surface.
4. Keep `SampleGroundY` consistent with the mesh height field (math sample from the same DEM + flatten — not NavMesh Y).
5. Optional: keep non-drawn Terrain collider in sync or MeshCollider only; placement Y remains DEM math.
6. After gen: existing `GroundExistingWorldObjects` / placement paths unchanged in behavior.

### Resources & playability

- Scatter / deny-list unchanged; node and building Y from `SampleGroundY`.
- Colony-friendly: DEM crop chosen for flats + landing flatten; distant ridges for silhouette.

## Success

- Play Mode Easy/MainGame: ground looks like the **After** reference (photo dust/rock, natural relief, flat camp).
- No floating buildings/deposits vs mesh.
- Console/boot still works if maps missing (fallback), but Easy assets have maps assigned for the intended look.

## References (in repo)

- `Assets/Art/References/PlanetSurface/what-is-a-dem.jpg`
- `Assets/Art/References/PlanetSurface/planet-look-colony-plains.jpg`
- `Assets/Art/References/PlanetSurface/planet-before-after-dem.jpg`
