# Node-by-Node Exploration System (Terraformers-Style)

## Core Problem
The current implementation reveals entire sectors at once. The user wants **node-by-node** exploration:
- Each node connects to adjacent nodes (within and across sectors)
- Exploring one node only reveals that node + shows "?" on connected nodes
- The player follows node chains outward from the UCC

## How It Should Work

```
Starting State:
    UCC ── [?] ── [?]
                   │
                  [?]

Play Orbital Scan on UCC → reveals its connections:
    UCC ── [Minerals] ── [?]
                         │
                        [?]

Click [?] → explore that node → reveals it + its connections:
    UCC ── [Minerals] ── [Gas] ── [?]
                         │
                        [Iron]
```

## Changes Required

### 1. SectorNode.cs — Add Connections
Each node needs to know which nodes it connects to:

```csharp
public class SectorNode
{
    public NodeType type;
    public Vector3 position;
    public bool isRevealed;
    public bool isDiscovered;     // "?" state — adjacent revealed this node
    public bool isExplored;       // Fully explored (player chose to explore it)
    public string labelOverride;
    public string flavorText;
    public GameObject visualGO;
    public List<SectorNode> connections;  // Nodes this node connects to
    public int connectedSectorIndex;
}
```

### 2. PlanetGenerator — Build Node Graph
When placing nodes, connect them in a graph:

```
Sector 0 nodes connect to Sector 1 nodes via Nexus
Each node connects to 2-3 nearby nodes (within same sector)
Some nodes connect across sector boundaries
```

### 3. Node Visuals — Simple Dots
Replace colored primitives with simple dots on the ground:

| Node Type | Color | Size | Shape |
|-----------|-------|------|-------|
| Minerals | Blue | Tiny | Circle |
| Gas | Green | Tiny | Circle |
| Iron | Grey | Tiny | Circle |
| Regolith | Brown | Tiny | Circle |
| Feature | Orange | Small | Star |
| Nexus | Purple | Small | Diamond |

Unexplored but discovered ("?") nodes show a **pulsing question mark** instead of the dot.

### 4. Exploration Flow
Instead of `FullyExploreSector`, the flow is:

```
Play Orbital Scan / Survey Drone
    ↓
Find the nearest unexplored node connected to an explored node
    ↓
Reveal that node (show its dot + label)
    ↓
Show "?" markers on all nodes connected to this newly revealed node
    ↓
Player clicks a "?" node to explore it next
    ↓
OR play another scan card to auto-reveal the nearest "?" node
```

### 5. Click-to-Explore on "?" Nodes
When the player clicks a "?" node marker:
- If they have an exploration card available → auto-play it on that node
- The node becomes explored, reveals its connections
- The process chains

### 6. Sector Unlock
Sectors unlock when the player has explored any node within them.
- Previously: sector fully unlocks on explore
- Now: sector unlocks when first node in that sector is explored
- Subsequent nodes in same sector are just more dots to reveal

## Implementation Plan

### Phase 1: Fix Node Visuals (small dots)
- Replace primitive shapes with flat circular sprites (like TM tiles)
- Use billboarding so they always face camera
- Add "?" sprite for discovered-but-unexplored nodes

### Phase 2: Add Node Connections
- In `PlaceSectorResourceNodes()`, after creating all nodes, connect nearby nodes
- Each node gets 2-4 connections to other nodes
- At least one connection leads to a node in the next sector

### Phase 3: Change Exploration Logic
- `InstantExplore()` now reveals ONE node, not the whole sector
- Find connected-but-unrevealed nodes → show them as "?"
- Player clicks "?" nodes to chain-explore

### Phase 4: UI Updates
- Discovery UI shows the single node + its connections
- "?" markers have click handlers that trigger exploration

## Files to Modify

| File | Changes |
|------|---------|
| `SectorNode.cs` | Add `connections`, `isDiscovered`, `isExplored` fields |
| `PlanetGenerator.cs` | Build node connection graph, use small dot visuals |
| `ExplorationManager.cs` | Per-node exploration instead of per-sector |
| `SectorManager.cs` | Remove `FullyExploreSector`, simplify to node tracking |
| `ExplorationDiscoveryUI.cs` | Show single node + connections