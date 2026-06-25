# Phase 1: Exploration-Based Sector Unlocking & Discovery Drafts — Final Implementation Plan

## Decisions Confirmed
- **No FoundryCrawler** — exploration happens purely through scouting cards + ExplorationManager
- **New cards** added to [`BlueprintCard.cs`](Assets/Scripts/Player/BlueprintCard.cs) alongside existing types
- **Emergency Caches** is a 4th extra option (3 normal cards + guaranteed fallback)
- **NaturalEventManager** left off — Phase 2 work

---

## Implementation Steps (Ordered)

### Step 1: Persistent Depletion — Remove Auto-Replenish
**Files:** [`GenerationManager.cs`](Assets/Scripts/Player/GenerationManager.cs)

- Remove `PlanetGenerator.Instance?.ReplenishResources()` from `StartNextGeneration()` (line 429)
- Remove `PlanetGenerator.Instance?.ReplenishResources()` from `CompleteExpansion()` (line 478)
- Add new method `ReplenishResourcesInSector(SectorManager.Sector sector)` to [`PlanetGenerator.cs`](Assets/Scripts/Environment/PlanetGenerator.cs) that scatters resources only within a specific sector's bounds (does NOT destroy existing resources)
- Call `ReplenishResourcesInSector()` when a sector is explored for the first time

### Step 2: DiscoverySystem — Resource Type Tracker
**New file:** `Assets/Scripts/Environment/DiscoverySystem.cs`

Static class tracking which resource types have been discovered:
```csharp
public static class DiscoverySystem
{
    private static HashSet<string> discoveredTypes = new() { "Minerals", "Gas" };
    
    public static bool IsTypeDiscovered(string typeName);
    public static void RevealResourceType(string typeName);
    public static HashSet<string> GetDiscoveredTypes();
    public static HashSet<string> GetResourceTypesInExploredSectors();
    public static void Reset();
}
```

### Step 3: New Card Types in BlueprintCard.cs
**File:** [`BlueprintCard.cs`](Assets/Scripts/Player/BlueprintCard.cs)

Add three new ScriptableObject card types:

**A) `DiscoveryCardSO`** — Reveals a resource type on the map
- Fields: `DiscoveryType` enum (IronVein, GasPocket, RegolithField, MineralSurvey, DeepCoreScan, DebrisField), `bonusMaterials`
- `Apply()`: Calls `DiscoverySystem.RevealResourceType()` + grants bonusMaterials if any

**B) `ScoutingCardSO`** — Exploration/scouting actions
- Fields: `ScoutingType` enum (OrbitalScan, PipelineBoost, SurveyDrone, EmergencyCaches), `materialsAmount`
- `Apply()`: OrbitalScan → `ExplorationManager.InstantExplore()`, PipelineBoost → boost exploration speed, SurveyDrone → spawn probe, EmergencyCaches → grant materials

**C) `DrillBreakthroughCardSO`** — Temporary gather speed boost
- Fields: `gatherSpeedMultiplier` (1.5, 2.0, or 3.0)
- `Apply()`: Multiplies `BlueprintDraftManager.GatherSpeedMultiplier` for the round

### Step 4: SectorManager — Add Exploration Tracking
**File:** [`SectorManager.cs`](Assets/Scripts/Environment/SectorManager.cs)

- Add `IsExplored` bool to `Sector` class
- Add `ExploreNextSector()` — marks next locked sector as explored, fires `OnSectorExplored` event
- Add `ExploreSector(int index)` — explores a specific sector
- Modify `UnlockNextSector()` — only unlocks if sector `IsExplored` is true
- Add `OnSectorExplored` event (separate from `OnSectorUnlocked`)
- `InitializeSectors()`: Sector 0 starts as explored + unlocked

### Step 5: ExplorationManager — Central Exploration Controller
**New file:** `Assets/Scripts/Environment/ExplorationManager.cs`

```csharp
public class ExplorationManager : MonoBehaviour
{
    public static ExplorationManager Instance;
    public bool IsExploring { get; private set; }
    public float ExplorationProgress { get; private set; } // 0-1
    
    public void InstantExplore(); // Orbital Scan — immediately explores next sector
    public void BoostExplorationSpeed(float multiplier, float duration); // Pipeline Boost
    public void DeploySurveyDrone(); // Spawns disposable probe
}
```

- `InstantExplore()`: Calls `SectorManager.Instance.ExploreNextSector()`, then `UnlockNextSector()`, then `PlanetGenerator.ReplenishResourcesInSector()`
- `DeploySurveyDrone()`: Spawns a fast probe unit that scouts ahead

### Step 6: GenerationManager — Remove Auto-Unlock
**File:** [`GenerationManager.cs`](Assets/Scripts/Player/GenerationManager.cs)

- `StartNextGeneration()`: Remove `SectorManager.Instance.UnlockNextSector()` (lines 423-426)
- `CompleteExpansion()`: Remove `SectorManager.Instance.UnlockNextSector()` (lines 392-395)
- Sectors now only unlock via `ExplorationManager` → `SectorManager.ExploreNextSector()` → `UnlockNextSector()`

### Step 7: HiddenResource — Wire to Discovery Types
**File:** [`HiddenResource.cs`](Assets/Scripts/Environment/HiddenResource.cs)

- Add `resourceTypeName` string field (set during scatter: "Iron", "Regolith", "Minerals", "Gas")
- `Discover()`: Check `DiscoverySystem.IsTypeDiscovered(resourceTypeName)` — if type not discovered, resource stays hidden
- Add `ForceDiscover()` for starting sector resources (bypasses discovery check)

### Step 8: PlanetGenerator — Assign Resource Types During Scatter
**File:** [`PlanetGenerator.cs`](Assets/Scripts/Environment/PlanetGenerator.cs)

- `ScatterResources()`: Set `resourceTypeName` on each spawned `HiddenResource` based on prefab name
- `ScatterFuelResources()`: Set "Iron" or "Regolith" based on SupplySO
- `ReplenishResourcesInSector()`: New method — scatters resources only within a sector's world-space bounds

### Step 9: CardDeckController — Curated Drafts
**File:** [`CardDeckController.cs`](Assets/Scripts/Player/CardDeckController.cs)

- `TriggerDraft()`: Draw 3 cards from curated pool + always include Emergency Caches as 4th option
- Curation: Filter discovery cards to only show resource types present in explored sectors
- If sectors remain locked, guarantee at least 1 scouting card in the 3-card hand
- Emergency Caches is always the 4th card (weakest option, always available)

### Step 10: PlanetGenerator — Per-Sector Minimum Deposits
**File:** [`PlanetGenerator.cs`](Assets/Scripts/Environment/PlanetGenerator.cs)

- After `ScatterResources()` + `ScatterFuelResources()`, verify each sector has ≥2 deposits with ≥2 resource types
- If a sector is below minimum, force-place additional resource nodes in that sector
- No sector is ever completely barren

### Step 11: Supplies — Grace Floor / Panic Mode
**File:** [`Supplies.cs`](Assets/Scripts/Player/Supplies.cs)

- Add `OnMaterialsDepleted` event (fires when Materials hits 0)
- Add `OnPanicMode` event (fires when Materials < 50 and all buildings degraded)
- Add `IsPanicMode` static property

### Step 12: BuildingUpkeepManager — Materials Upkeep Tax
**New file:** `Assets/Scripts/Player/BuildingUpkeepManager.cs`

```csharp
public class BuildingUpkeepManager : MonoBehaviour
{
    public static BuildingUpkeepManager Instance;
    [SerializeField] private float tickRate = 1f;
    [SerializeField] private float baseUpkeepPerBuilding = 0.5f;
    [SerializeField] private int panicThreshold = 50;
    
    public void RegisterBuilding(BaseBuilding building);
    public void UnregisterBuilding(BaseBuilding building);
    public bool IsDegraded(BaseBuilding building);
    private IEnumerator UpkeepLoop();
}
```

- Each tick: sum upkeep from all completed buildings, deduct from Materials
- Materials hits 0 → buildings enter degraded state
- Grace floor: if Materials < panicThreshold AND all buildings degraded → pause upkeep (panic mode)
- When Materials recovers above panicThreshold, buildings auto-recover

### Step 13: BaseBuilding — Degraded State
**File:** [`BaseBuilding.cs`](Assets/Scripts/Units/BaseBuilding.cs)

- Add `IsDegraded` property
- Register/unregister with `BuildingUpkeepManager` in `OnEnable()`/`OnDisable()`
- When degraded: building operates at 50% efficiency (affects production, power gen, life support)
- Visual: swap to yellow-tinted material or adjust emission color

---

## Dependency Graph

```
Step 1 (Persistent Depletion) ─────────────────────────────────────────┐
Step 2 (DiscoverySystem) ──────────────────────────────────────────────┤
Step 3 (New Card Types) ───────────────────────────────────────────────┤
Step 4 (SectorManager Exploration) ────────────────────────────────────┤
Step 5 (ExplorationManager) ── depends on Step 4 ──────────────────────┤
Step 6 (GenerationManager Decouple) ── depends on Step 5 ──────────────┤
Step 7 (HiddenResource Wiring) ── depends on Step 2 ───────────────────┤
Step 8 (PlanetGenerator Typing) ── depends on Step 2 ──────────────────┤
Step 9 (Card Curation) ── depends on Steps 2, 3 ───────────────────────┤
Step 10 (Map Guarantees) ── depends on Step 8 ─────────────────────────┤
Step 11 (Grace Floor) ── depends on Step 12 ───────────────────────────┤
Step 12 (BuildingUpkeepManager) ───────────────────────────────────────┤
Step 13 (BaseBuilding Degradation) ── depends on Step 12 ──────────────┘
```

Steps 1-4 and 12 can be done in parallel. Steps 5-6 chain from 4. Steps 7-10 chain from 2-3. Steps 11, 13 chain from 12.

---

## Game Flow After Phase 1

```
ROUND START
  │
  ├─► Draft Phase (3 discovery/scouting cards + Emergency Caches)
  │     Pick 1 card → deposits appear on map (if discovery) or sector explored (if scouting)
  │
  ├─► Active Round
  │     Mine discovered deposits → earn Materials
  │     Upkeep drains Materials pool
  │     Build buildings to meet milestone
  │     Degraded buildings at 50% if Materials hits 0
  │
  ├─► Milestone Met → Round Ends
  │     Materials → Terra-Coins
  │     Depleted deposits stay gone
  │     Next sector NOT auto-unlocked
  │
  └─► Next Draft → must explore to unlock next sector
        If no scouting card chosen, stuck in current sector
        Emergency Caches always available as fallback
```

---

## Files Summary

| Action | File |
|--------|------|
| **MODIFY** | [`GenerationManager.cs`](Assets/Scripts/Player/GenerationManager.cs) — Remove auto-unlock + auto-replenish |
| **MODIFY** | [`SectorManager.cs`](Assets/Scripts/Environment/SectorManager.cs) — Add IsExplored, ExploreNextSector |
| **MODIFY** | [`BlueprintCard.cs`](Assets/Scripts/Player/BlueprintCard.cs) — Add 3 new card types |
| **MODIFY** | [`CardDeckController.cs`](Assets/Scripts/Player/CardDeckController.cs) — Curated drafts + 4th slot |
| **MODIFY** | [`HiddenResource.cs`](Assets/Scripts/Environment/HiddenResource.cs) — Wire to discovery types |
| **MODIFY** | [`PlanetGenerator.cs`](Assets/Scripts/Environment/PlanetGenerator.cs) — Resource typing + per-sector replenish + minimum guarantees |
| **MODIFY** | [`Supplies.cs`](Assets/Scripts/Player/Supplies.cs) — Panic mode events |
| **MODIFY** | [`BaseBuilding.cs`](Assets/Scripts/Units/BaseBuilding.cs) — Degraded state |
| **CREATE** | `Assets/Scripts/Environment/DiscoverySystem.cs` — Resource type tracker |
| **CREATE** | `Assets/Scripts/Environment/ExplorationManager.cs` — Exploration controller |
| **CREATE** | `Assets/Scripts/Player/BuildingUpkeepManager.cs` — Materials upkeep tax |
