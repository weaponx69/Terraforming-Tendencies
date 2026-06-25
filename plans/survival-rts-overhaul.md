# Survival RTS Pivot — "Terraformers-Style" Survival Overhaul Plan

## Vision
Transform Terriforming Tendencies from a comfortable roguelite into a tense survival RTS. Cards unlock mining permits and exploration abilities — not free resources. Sectors unlock through active exploration, revealing new deposits, threats, and opportunities. Every round you scrape by with what you discovered. Failure is inevitable — the game is about triage, not optimization.

---

## Phase 1: Exploration-Based Sector Unlocking & Mining Permits

### 1.1 Sector Unlocking Through Exploration (Not Automatic)
- **Current:** Sectors unlock automatically when all milestones in a generation are completed.
- **Change:** Sectors are locked behind **exploration**. The player actively sends Probe drones or uses scouting cards to reveal adjacent sectors. Once a sector is scouted, it becomes available for colonization.
- Card types that enable exploration:
  - *"Survey Drone"* — Deploy a fast Probe unit that can scout one adjacent sector
  - *"Orbital Scan"* — Instantly reveals an adjacent sector (no drone needed)
  - *"Deep Radar"* — Reveals hidden resource deposits in all explored sectors
  - *"Satellite Uplink"* — Permanently increases Probe scan radius by 50%
- Exploration reveals: new resource deposits (Iron, Gas, Regolith), hidden hazards, and terrain suitable for expansion.
- **Files:** [`SectorManager.cs`](Assets/Scripts/Environment/SectorManager.cs), [`GenerationManager.cs`](Assets/Scripts/Player/GenerationManager.cs:27), [`BlueprintDraftManager.cs`](Assets/Scripts/Player/BlueprintDraftManager.cs)

### 1.2 Cards Grant Mining Permits (Not Free Resources)
- **Current:** Cards grant flat resources (e.g., +250 Biomass) or unlock buildings.
- **Change:** Cards unlock **mining permits** for what you can gather this round:
  - *"Iron Prospecting"* — Drones can mine Iron deposits
  - *"Gas Extraction Rights"* — Drones can mine Gas deposits
  - *"Regolith License"* — Drones can mine Regolith deposits
  - *"Emergency Reserves"* — Flat +200 Materials immediately (no mining)
  - *"Deep Core Scanner"* — Reveals hidden deposits + one-time bonus
  - *"Drill Efficiency I/II/III"* — Drones gather 50%/100%/200% faster
  - *"Salvage Rights"* — Drones collect debris from destroyed buildings/meteors
- Drones **refuse to mine** resources the player has no permit for.
- **Files:** [`BlueprintDraftManager.cs`](Assets/Scripts/Player/BlueprintDraftManager.cs), [`BlueprintCard.cs`](Assets/Scripts/Player/BlueprintCard.cs), [`Worker.cs`](Assets/Scripts/Units/Worker.cs), [`GatherSuppliesAction.cs`](Assets/Scripts/Behavior/GatherSuppliesAction.cs)

### 1.3 Persistent Depletion (No Replenishment Between Rounds)
- **Current:** `PlanetGenerator.ReplenishResources()` refills all nodes between generations.
- **Change:** Resources do NOT replenish between generations — only when a new sector is explored and unlocked. Depleted deposits stay gone, forcing adaptation.
- **Files:** [`PlanetGenerator.cs`](Assets/Scripts/Environment/PlanetGenerator.cs)

### 1.4 Building Upkeep Tax
- Every completed building consumes X Materials/second. Pool hits 0 → buildings enter **degraded state** (50% output, visual damage). Scales with colony size.
- **Files:** New `BuildingUpkeepManager.cs` or extend [`GlobalDecayManager.cs`](Assets/Scripts/Environment/GlobalDecayManager.cs:10)

### 1.5 The Full Economic Loop
```
Explore Sector → Reveal Deposits → Draft Mining Permit → Mine → Spend on Survival → Explore Next Sector
```

---

## Phase 2: Enable & Escalate Natural Threats

### 2.1 Activate NaturalEventManager
- **Current:** `autoStart = false` — waves never fire.
- **Change:** `BeginAssault()` called when first generation starts. Waves escalate per sector:
  - Sector 0: 2 events/wave, 45s gap, 15 dmg, 3f radius
  - Sector 1: 3 events/wave, 35s gap, 25 dmg, 4f radius
  - Sector 2: 4 events/wave, 25s gap, 40 dmg, 5f radius
  - Sector 3+: 5 events/wave, 20s gap, 60 dmg, 6f radius
- **Files:** [`NaturalEventManager.cs`](Assets/Scripts/Environment/NaturalEventManager.cs:13), [`GenerationManager.cs`](Assets/Scripts/Player/GenerationManager.cs:27)

### 2.2 New Event Types
- **Solar Flare:** Disables Solar Panels for 10-20s. 5s warning. Cascading failure risk.
- **Toxic Storm:** Moving 15f AoE, 10 dmg/sec to units, moves over 20s. Drones can flee.
- **Seismic Tremor:** Damages oldest/isolated building. Encourages clustered bases.
- **Files to create:** `SolarFlareEvent.cs`, `ToxicStormEvent.cs`, `SeismicTremorEvent.cs`

### 2.3 Decay Now Affects Everything
- LifeSupport reduces decay by 50% instead of negating it. Decay scales: `baseDecayRate * (1 + totalBuildings * 0.05)`.
- **Files:** [`GlobalDecayManager.cs`](Assets/Scripts/Environment/GlobalDecayManager.cs:10), [`LifeSupportNode.cs`](Assets/Scripts/Environment/LifeSupportNode.cs:6)

---

## Phase 3: Random Failures & Emergencies

### 3.1 Building Failure System
- **New:** `BuildingIntegrityManager.cs` — buildings accumulate stress from age, events, degraded state, proximity to failed buildings. Threshold hit → building breaks, needs Repair (Materials + drone time).
- **Files to create:** `BuildingIntegrityManager.cs`, `BuildingFailureEvent.cs`

### 3.2 Mine Blowouts
- Small % chance per gather tick: resource destroyed, drone takes 50% HP, poison cloud spawns, partial refund. More volatile on nearly-empty nodes.
- **Files to create:** `MineBlowoutEvent.cs`, modify [`GatherSuppliesAction.cs`](Assets/Scripts/Behavior/GatherSuppliesAction.cs)

### 3.3 Emergency Response Cards
- On failure/blowout → emergency draft: "Repair" (costs TC), "Evacuate Sector" (refund 50%), "Scrap for Parts" (gain 75% cost), "Do Nothing" (+5 TC).
- **Files:** [`BlueprintDraftManager.cs`](Assets/Scripts/Player/BlueprintDraftManager.cs), [`BlueprintCard.cs`](Assets/Scripts/Player/BlueprintCard.cs)

---

## Phase 4: Colony Collapse Cascade

### 4.1 LifeSupport Domino
- LifeSupportNode destroyed → buildings in its exclusive radius take 40% integrity damage. Can cascade.
- **Files:** [`LifeSupportNode.cs`](Assets/Scripts/Environment/LifeSupportNode.cs:6)

### 4.2 Power Grid Brownouts
- Generation < demand → farthest buildings shut off first: no LifeSupport, no production, 2x decay. Command Post offline = all queues pause.
- **Files:** [`PowerGridManager.cs`](Assets/Scripts/Environment/PowerGridManager.cs), [`PowerNode.cs`](Assets/Scripts/Environment/PowerNode.cs)

### 4.3 Tightened Game Over
- Grace period 30s → 5s. Remove 10% biomass safety net. New: "3+ failures in 10s" = collapse. New: "No Functional Buildings" = instant loss.
- **Files:** [`GameOverManager.cs`](Assets/Scripts/Player/GameOverManager.cs:19)

---

## Phase 5: UI & Feedback

### 5.1 Building Status Visuals
- Degraded: yellow tint. Damaged: cracks/smoke. Failed: sparks/red. Labels: green → yellow → orange → red → flashing red.

### 5.2 Tension HUD
- Stability Meter (colony health, red vignette at low). Mining Permit display with remaining deposit counts. Threat countdown timer.

### 5.3 Emergency Alerts
- Toast popups with one-click repair. Minimap markers. Audio: alarm on failure, shifting ambient tension.

---

## Implementation Order

| Order | Phase | Rationale |
|-------|-------|-----------|
| 1 | **Phase 1** (Exploration + Mining Permits) | Foundation — exploration unlocks sectors + cards gate mining |
| 2 | **Phase 2** (NaturalEventManager + Escalation) | Quick win — system exists, needs activation + tuning |
| 3 | **Phase 3** (Random Failures) | Core new mechanic — breakdowns, blowouts, emergencies |
| 4 | **Phase 4** (Collapse Cascade) | Makes failures consequential |
| 5 | **Phase 5** (UI/Feedback) | Polish |

---

## Key Design Principles
1. **Explore to expand** — sectors unlock through scouting, not automatic milestones
2. **Cards unlock access, not grants** — you mine what you draft; nothing is free
3. **Resources are finite per sector** — no replenishment without new exploration
4. **Failure is inevitable** — triage what to save
5. **No random sudden death** — failures give warnings

---

## Round Flow (Player Experience)

```
DRAFT: 3 cards → pick 1 (mining permit, exploration ability, or flat grant)
  ↓
EXPLORE: Use scouting cards/drones to reveal adjacent sectors & deposits
  ↓
MINE: Send drones to permitted resource nodes (others are locked out)
  ↓
SURVIVE: Natural events strike, upkeep drains materials, buildings may fail
  ↓
COMPLETE: Milestone met → Materials liquidate to Terra-Coins
  ↓
REPEAT: Depleted deposits stay gone. Draft adapts to remaining resources.
```
