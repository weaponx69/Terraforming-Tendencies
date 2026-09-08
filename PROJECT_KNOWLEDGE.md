### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md) — lore only; **this file wins** on run rules
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Authoritative Run Model — Combolands Colony Acts

**One-sentence game:** Draw building **tiles** → place on the ground → spend **1 week** → earn **Colony Score** (base + adjacency) **and** push **Temp / Atmos / Water from the current sector** → clear one Act per sector before weeks run out → win the planet.

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) — place tiles for score under a turn budget; position and stacking matter. Sector count drives how many Acts the run has.

### Owner scripts
| Role | Script |
|------|--------|
| Acts, weeks, score, win/fail | [`ColonyActManager`](Assets/Scripts/Player/ColonyActManager.cs) |
| Tile grid / join snap | [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) |
| Hand draw / consume card | [`CardDeckController`](Assets/Scripts/Player/CardDeckController.cs) |
| Free ground place (cards) | [`BuildBuildingCommand`](Assets/Scripts/Commands/BuildBuildingCommand.cs) + [`BottomBarActionsUI`](Assets/Scripts/UI/Containers/BottomBarActionsUI.cs) |
| Power place-gate | [`PowerGridManager`](Assets/Scripts/Environment/PowerGridManager.cs) (`CanPlayBuildingForPower`) |
| Look / fog / flora | [`ClimateVisualStages`](Assets/Scripts/Environment/ClimateVisualStages.cs), [`VegetationManager`](Assets/Scripts/Environment/VegetationManager.cs) |
| Objectives HUD | [`ActiveObjectivesUI`](Assets/Scripts/UI/Containers/ActiveObjectivesUI.cs) |
| Weeks left (left HUD) | [`WeeksLeftUI`](Assets/Scripts/UI/Containers/WeeksLeftUI.cs) |
| Tile snap / place SFX | [`AudioManager`](Assets/Scripts/Audio/AudioManager.cs) (`PlayTileSnapSound` / `PlayPlaceClickSound`) |

---

## 0.1 What is NOT the game (do not reintroduce)

| Retired idea | Status |
|--------------|--------|
| Win = Temp + Atmos + Water **alone** (no score/weeks) | **Retired** |
| Hamster MVP one-round climate victory | **Retired** |
| Sector unlock / colonization as progression | **Retired** (locks/pads) — **Acts now = sectors** |
| Sector build lock / active-sector-only pads | **Retired** |
| Card play gated by Materials | **Retired** (cards) |
| Card play gated by drones / reserved pads | **Retired** (cards self-construct) |
| Auto-discard “unplayable” hand cards | **Retired** |
| Force-seat Solar / climate / Mining Drone into hand | **Partial** — Solar **is** always seated (power gate); climate/drone force-seat stays retired |
| Climate soft-gates blocking card draw/select | **Retired** |
| Oxygen / Power / Pop as win primaries | **Retired** |
| Fixed 4 Acts independent of map size | **Retired** — Act count = sector count |
| Mining / materials depletion as run loss | **Retired** (while Colony Acts are active) |

Climate tickers **do** count for Act clear (with Colony Score). They are not the *only* win meter.

---

## 0.2 Acts (milestones) — one per sector

**Act count = sector count** on the generated planet (e.g. 4 sectors → 4 Acts; 9 → 9).

| Act slot | Typical name | Score | Weeks |
|----------|--------------|------:|------:|
| First | Survive · Sector 1 | 40 | 8 |
| Middle | Settle / Expand · Sector N | 40 + 35×(N−1) | 8 |
| Last | Thrive · Sector N | … | 10 → **run victory** |

* **1 successful card play = 1 week.**
* **Act clear = Colony Score target AND Temp/Atmos/Water deltas** from that Act’s baselines (+15°C / +0.25 atm / +5%).
* **Climate ticks only from the current focus sector** — Heat / Air / Water buildings elsewhere do not advance this Act. You must plant climate infrastructure in each region.
* On clear: camera pans to the next sector; climate baselines reset; ~**25%** score (+ excess) carries.
* Weeks hit 0 without both requirements → **Act fail / run loss**.
* Final sector Act clear → victory.

---

## 0.3 Loop (how a play works)

1. **Hand (up to 10 cards, scrollable)** — Solar is **always** seated. Playing uses a card up; draw fills remaining slots. Cards keep a fixed playing-card size; hover the hand and use the **mouse wheel** (or trackpad horizontal scroll) to scroll the strip left/right when more than ~5 cards are held.
2. **Week** — commit spends **1 week** on the Act clock (`SpendWeek` on consume).
3. **Placement gate = power only**
   * If `PowerUpkeep > 0` and board generation cannot cover **board upkeep + this tile**, placement is blocked.
   * Generators / zero-upkeep tiles always place (power-wise).
   * No Materials / drone / pad requirement on **card** plays.
4. **Free tile placement** — card ghost snaps to the 12 m grid under the cursor (sticky cell). Click places; ghost rises (**no drone**). Score / climate apply when construction finishes.
5. **Score** — on complete: **Base Score + adjacency** (+ Habitability for climate tags). Completing a building also **queues its old RTS production options as hand cards** for the next fill (no build menu on the structure).
6. **Climate** — powered climate buildings in the **current focus sector** tick Temp / Atmos / Water. Other sectors do not help this Act.
7. Clear Act when **score AND climate** are both met before weeks run out → next sector (or win).

**Build source:** cards / tiles only. Selecting a building does **not** open an RTS build/train panel.

---

## 0.4 Tile tags, base score, Habitability

| Tag | Examples | Base Score | Habitability |
|-----|----------|------------|--------------|
| Anchor | Command Post, housing | 12 / 10 | — |
| Power | Solar | 4 | — |
| Labor | Mining Drone (non-building card) | 3 | — |
| Industry | Mines | 8 | — |
| Heat | GHG / geothermal / heat buildings | 10 | +8 |
| Air | Condenser / import | 10 | +8 |
| Water | Aquifer / water buildings | 10 | +8 |
| Life | Oxygen Processor | 6 | +3 |
| Other | default | 5 | — |

Non-building cards grant a small flat score on play (no adjacency).

---

## 0.5 Adjacency (stacking)

**Grid:** card buildings snap to a **12 m square tile grid** ([`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs)). Only **orthogonal edge** neighbors count (N/E/S/W) — same cells the placement magnet snaps to.

### Placement feedback (ghost)
* Semi-transparent **tile footprint** under the ghost (locks to the snap cell immediately).
* Ghost mesh stays a stable blue/red valid tint (no green strobe on fresnel).
* **Green footprint + join lines** show adjacency; ghost is removed from `ActiveBuildings` so it cannot occupy its own cell.

### Score bonuses (shipped — per edge neighbor, soft-capped +20)
| Relationship | Bonus |
|--------------|------:|
| Any neighbor | +2 |
| Same tag | +4 |
| Power next to a consumer (upkeep &gt; 0) | +5 |
| Anchor next to anything | +3 |
| Climate pair Heat↔Air, Air↔Water, Water↔Heat | +4 |
| Life next to Water or Anchor | +4 |

* HUD / tooltip should explain stacking; placement popcorn (`+Score`) is backlog.

---

## 0.6 Habitability & planet look

* Habitability accumulates from climate-tagged tiles (Heat / Air / Water / Life).
* [`ClimateVisualStages`](Assets/Scripts/Environment/ClimateVisualStages.cs): Barren → Thaw → Wet → Living from Habitability — **ground tint**, light **linear fog**, **ambient** (Exp² haze retired — was a dust wall when zoomed out).
* [`VegetationManager`](Assets/Scripts/Environment/VegetationManager.cs): flora density from Habitability (not Oxygen, not Atmos supplies).
* **Not** driven by Temp/Atmos/Water win deltas.

---

## 0.7 Board, power, sectors

* **Board:** whole planet open for tile placement; cards snap to [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) (12 m cells). **Acts** advance sector-by-sector.
* **Power:** only hard **placement** gate for cards (`PowerGridManager.CanPlayBuildingForPower`). Hand may hold cards the player cannot place yet. **Adjacent tiles auto-link** on the power graph when construction completes (`BaseBuilding.AutoConnectAdjacentPowerNodes`) — no manual Connect Power.
* **Sectors:** map-gen count drives **Act count**. Each Act focuses terraforming on one sector (`SectorManager.BeginTerraformingOn`). No old unlock/pad lockdown.
* Reserved pads / drones may still exist in the scene for legacy systems; **card plays ignore them**.
* Non-card builds (legacy drone path) do **not** force the tile grid.

---

## 1. Status Board

| Piece | Status | Notes |
|-------|--------|-------|
| Colony Acts + week clock | **Done** | **1 Act per sector**; climate gated to focus sector |
| Free hand pick + consume on play | **Done** | Solar always seated; hand up to 10 + scroll |
| Card-only builds (no RTS build menus) | **Done** | Building production → next-hand offers |
| Free ground placement | **Done** | Click-to-place cards |
| Power-only place gate | **Done** | |
| Base score + adjacency (tile-edge) | **Done** | Ortho grid + §0.5 bonuses / soft cap |
| Tile snap + join ghost feedback | **Done** | Footprint, green tint, join lines |
| Objectives: Act / Score / Weeks | **Done** | Explicit WIN/LOSE + at-risk strip |
| Habitability → look + flora | **Done** | |
| Sector progression | **Retired** | |
| Climate-trio victory | **Retired** | |

**Next polish**
1. Placement +Score / +adj float text  
2. Soft-fail / extra weeks (Combolands-like)  
3. Retarget CLI bots to Colony Score Acts  
4. Optional stronger tile “seam” mesh between joined buildings

---

## 2. Code map (honest)

| Concern | Behavior |
|---------|----------|
| Win / lose Acts | `ColonyActManager` |
| Legacy `GenerationManager` | `MaxGenerations = 1` shell; victory via `NotifyColonyActVictory` — **not** climate progress |
| `DoesBuildingCountForActiveClimate` | True only for buildings in the **current Act focus sector** |
| Card UI | Lower-left hand (~5-card viewport + `RectMask2D`); hover lights the dock; wheel scrolls horizontally; cost chip shows **1 Week** |
| Instant card place | Completes immediately; week spent on consume; score deferred until after week when needed |

---

## 3. Support systems (keep, demoted)

### 3.1 UI chrome
* Hand lower-left; selection info far right; top resource strip for readability.
* Goal colors still tint Heat / Air / Water card accents — cosmetic.

### 3.2 Deck
* FIFO draw pile / discard; hand size 5.
* No climate force-seat; no purge for soft gates.

### 3.3 Colony integrity / UCC
* Integrity inactive until first real `(Clone)` building; UCC invulnerable / excluded from integrity math — keep unless it blocks card plays.

### 3.4 Prefabs / ghosts
* Prefer `BaseBuilding` variants; card ghost from command template when available.
* Site-marker ghosts must not steal pads or complete as real buildings.

---

## 4. Condensed fix memory (do not reintroduce)

* Climate-trio / `TriggerMvpVictory` as win  
* Sector lock or UnlockNextSector on Act clear  
* Auto-discard hand for Materials / pads / climate gates  
* Force-seat Solar / Water / drone reshuffling the hand  
* Materials or drone as card placement requirements  
* Hand power-budget trim that removes cards the player wanted to keep  
* Act length tied to sector count  

---

## 5. Backlog (after feel is right)

1. Richer adjacency (§0.5 design target) + soft cap  
2. Placement popcorn (+base / +adj floats)  
3. Neighbor “echo” re-score (optional Combolands cascade lite)  
4. Soft-fail / extra weeks  
5. Guilds / heirlooms / councillors — **out of scope** until Acts + stacking feel good  
6. Retarget `./tools/sector-win-cli.sh` → Colony Act bot  

---

## 10. Unity CLI

See [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md). **Unity MCP deprecated — CLI only.**

Safe while Editor is open: `unity status`, `unity command …`, `unity menu`, `unity eval`.  
Avoid second-Editor paths (`unity test` / `unity build` / `unity run` / `-batchmode`) on this machine.

---

## 11. Leave alone until Combolands loop is fun

Colonists/tubes as required systems, deep tech trees, combat, AI opponents, weather particles, lakes/oceans — do not expand unless they block Acts, free place, power gate, or adjacency.

---

*Last rewritten: 2026-09-08 — Acts require Colony Score + Temp/Atmos/Water deltas; Combolands placement rules unchanged.*
