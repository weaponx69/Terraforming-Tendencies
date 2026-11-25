# Planet Terrain Implementation Plan

> **For agentic workers:** implement this plan task-by-task, in order, checking off each step as it lands.

**Goal:** Seeded Unity Terrain per planet with random resources snapped to height.

**Architecture:** New `PlanetTerrainBuilder` fills `TerrainData` from `PlanetConfig` noise + seed and flattens the landing disk. `PlanetGenerator.GeneratePlanet` creates/updates a Terrain child, hides the flat mesh renderer, and keeps existing resource scatter (already raycasts).

**Tech Stack:** Unity Terrain / TerrainData, PlanetConfig, PlanetGenerator

---

### Task 1: Config seed field

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetConfig.cs`

- [x] Add `TerrainSeed`, `LandingFlatRadius`, document NoiseScale / HeightMultiplier as active

### Task 2: PlanetTerrainBuilder

**Files:**
- Create: `Assets/Scripts/Environment/PlanetTerrainBuilder.cs`

- [x] `BuildOrUpdate(...)` → Terrain
- [x] Layered noise heights; flatten landing; Terrain layer

### Task 3: Wire GeneratePlanet

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetGenerator.cs`

- [x] Call builder; hide flat mesh renderer
- [x] ClearPlanet destroys Terrain child (destroys all children)
- [x] Deposit raycast includes Terrain

### Task 4: Verify visually

- [ ] Enter Play Mode; confirm `PlanetTerrain` child + hills (Editor was thrashing under swap during agent capture)
