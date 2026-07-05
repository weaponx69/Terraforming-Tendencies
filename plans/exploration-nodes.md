# Full Exploration & Resource Node System v3

## Complete Node Types
When you explore a sector, these node types are revealed:

| Node Type | What It Is | What It Does |
|-----------|-----------|--------------|
| **Resource** | Minerals, Gas, Iron, Regolith deposits | Drones can mine these for materials |
| **Feature** | Lava Tube, Fault Line, Water Deposit | Enables placement of special buildings that require that feature |
| **Nexus** | Connection point to next sector | Shows "???" markers in the next sector |
| **Climate Bonus** | Thermal Surge, Atmospheric Compression, etc. | Grants immediate climate rewards |

## How Sector Exploration Works

```
Sector is Locked → Play exploration card → Sector becomes Explored
                                              ↓
                                     Reveal all nodes in sector:
                                       - 2x Minerals
                                       - 2x Gas
                                       - 1-2x Iron
                                       - 1-2x Regolith
                                       - 1x Feature (LavaTube / FaultLine / WaterDeposit / None)
                                       - 1x Nexus (connects to next sector)
                                       - 1-3x Climate Bonuses (random)
                                              ↓
                                     Nexus reveals "???" in next sector
                                     Climate bonuses are applied immediately
```

## Feature Node Mechanics
- Each sector has a pre-assigned `SectorFeature` (LavaTube, FaultLine, WaterDeposit, or None)
- This feature is hidden until the sector is explored
- When revealed, a special marker appears on the map: "Lava Tube Detected", "Fault Line Found", etc.
- Buildings that require that feature (e.g., Lava Tube Outpost, Subterranean Apartment Block) check `IsGateMet()` against the sector's feature — they can only be built in sectors where the feature was discovered
- The card still unlocks the blueprint, but placement is restricted to matching sectors

## Chain Exploration Flow

```
Sector 0 ──[nexus]──► Sector 1 ──[nexus]──► Sector 2 ──[nexus]──► Sector 3
   │                     │                     │
   ├─ Minerals            ├─ ???                ├─ ???
   ├─ Gas                 ├─ ???                ├─ ???
   ├─ Iron                ├─ ???                ├─ ???
   ├─ Regolith            ├─ ???                ├─ ???
   ├─ LavaTube ◄── feature├─ ???                ├─ ???
   └─ Nexus ──────────────┘                     └─ ???
             │
             └──► Sector 1 shows "???" markers
                  (Discovered state — not fully explored)
```

## Changes Required

### 1. New Files (4)
- `Assets/Scripts/Environment/ExplorationNodeSO.cs` — Node definitions (resource, feature, climate bonus)
- `Assets/Scripts/Environment/ExplorationNodeDatabase.cs` — Node pool + weighted selection
- `Assets/Scripts/UI/Containers/ExplorationDiscoveryUI.cs` — Discovery overlay
- `Assets/Scripts/Environment/SectorNode.cs` — Node class with position, type, connected sector

### 2. PlanetGenerator — Node-Based Placement
Replace `ScatterResources()` with structured node placement per sector:
```csharp
void PlaceSectorNodes(Sector sector, int sectorIndex)
{
    // Place 2 Minerals nodes
    // Place 2 Gas nodes
    // Place 1-2 Iron nodes
    // Place 1-2 Regolith nodes
    // Place 1 Feature node (based on sector's assigned feature)
    // Place 1 Nexus node (connects to sectorIndex + 1 if not last sector)
    // All get HiddenResource component
}
```

### 3. ExplorationManager — Chain Exploration
```csharp
void InstantExplore()
{
    int sectorIndex = SectorManager.Instance.GetNextLockedSectorIndex();
    if (sectorIndex < 0) return;
    
    // 1. Fully explore this sector
    SectorManager.Instance.FullyExploreSector(sectorIndex);
    
    // 2. Reveal all physical nodes in this sector
    RevealResourceNodes(sectorIndex);
    RevealFeatureNode(sectorIndex);
    
    // 3. Check nexus for chain connection
    int nextSectorIndex = sectorIndex + 1;
    if (nextSectorIndex < SectorManager.Instance.Sectors.Count)
    {
        SectorManager.Instance.DiscoverSector(nextSectorIndex);
        // Shows "???" markers at node positions in next sector
    }
    
    // 4. Grant climate bonuses
    GrantExplorationBonuses(sectorIndex);
    
    // 5. Show discovery UI
    ShowDiscoveryUI(sectorIndex);
}
```

### 4. SectorManager — New Sector States
```csharp
public class Sector
{
    // Existing
    public Vector3 Center;
    public bool IsOccupied;
    public bool IsLocked = true;
    public bool IsExplored = false;
    public SectorFeature Feature = SectorFeature.None;
    
    // NEW
    public bool IsDiscovered = false;  // "???" state
    public List<SectorNode> Nodes;     // All nodes in this sector
}

// New methods
public void DiscoverSector(int index) { /* Shows "???" markers */ }
public void FullyExploreSector(int index) { /* Reveals everything */ }
```

### 5. Building Placement — Feature Checking
Buildings that require a sector feature (Lava Tube Outpost, etc.) should check:
- The sector they're being placed in must have the matching feature
- The feature must have been discovered (sector must be explored)
- `BuildBuildingCommand.AllRestrictionsPass()` checks sector feature

### 6. Remove Climate Boost Cards from Deck
Remove these 5 cards from `InitializeDefaultPool()`:
- Thermal Surge Injectors
- Atmospheric Compression
- CO₂ Comet Redirect
- Subsurface Water Surge
- Cometary Ice Harvest

## Complete Node Pool

### Resource Nodes (placed per sector)
| Type | Count per Sector | Visual |
|------|-----------------|--------|
| Minerals | 2 | White crystal |
| Gas | 2 | Green gas vent |
| Iron | 1-2 | Grey rock |
| Regolith | 1-2 | Brown rock |

### Feature Nodes (placed per sector, based on sector's feature)
| Feature | Marker Label | Enables Buildings |
|---------|-------------|-------------------|
| LavaTube | "Lava Tube Detected" | Lava Tube Outpost, Subterranean Apartment Block |
| FaultLine | "Fault Line Found" | Sector Command Center, Magnetic Shield Generator |
| WaterDeposit | "Water Deposit Located" | Subglacial Water Extractor, Biosphere Center |
| None | (no feature node) | — |

### Climate Bonus Rewards (granted on explore)
| Bonus | Effect | Weight |
|-------|--------|--------|
| Thermal Surge | +8°C Temp | 1.0 |
| Atmospheric Compression | +0.12 atm | 1.0 |
| CO₂ Comet Trail | +0.15 atm | 0.8 |
| Subsurface Water Surge | +6% Water | 1.0 |
| Cometary Ice Harvest | +8% Water | 0.8 |
| Rich Mineral Vein | +400 Materials | 1.5 |
| Bio-Matter Cache | +100 Biomass | 1.2 |
| Abandoned Drone | Spawn Mining Drone | 1.0 |

## Files to Create (4)
- `Assets/Scripts/Environment/SectorNode.cs` — Node data class
- `Assets/Scripts/Environment/ExplorationNodeSO.cs` — Bonus reward definition
- `Assets/Scripts/Environment/ExplorationNodeDatabase.cs` — Bonus pool manager
- `Assets/Scripts/UI/Containers/ExplorationDiscoveryUI.cs` — Discovery overlay

## Files to Modify (6)
- `Assets/Scripts/Environment/PlanetGenerator.cs` — Node-based placement
- `Assets/Scripts/Environment/ExplorationManager.cs` — Chain exploration flow
- `Assets/Scripts/Environment/SectorManager.cs` — Add Discovered state, nexus
- `Assets/Scripts/Commands/BuildBuildingCommand.cs` — Feature checking
- `Assets/Scripts/UI/Containers/BlueprintDraftUI.cs` — Remove 5 climate cards
- `Assets/Scripts/Environment/DiscoverySystem.cs` — Handle node revealing