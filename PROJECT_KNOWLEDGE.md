### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md) — lore only; **this file wins** on run rules
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Authoritative Run Model — Combolands Colony Acts

**One-sentence game:** Draw building **tiles** → place on procedural geology for **Colony Score** (map `+N` popups) and **terraforming resources** → spend **weeks** → clear a fixed Act ladder → spend **Terra-Coins** on between-Act upgrades → expand with **Command Posts** → terraform **every** sector to win.

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) — place tiles for score under a turn budget; position and stacking matter. **Acts are independent of sectors.** Geography expands when you place Command Posts.

### Owner scripts
| Role | Script |
|------|--------|
| Acts, weeks, score, win/fail | [`ColonyActManager`](Assets/Scripts/Player/ColonyActManager.cs) |
| Tile grid / join snap | [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) |
| Hand draw / consume card | [`CardDeckController`](Assets/Scripts/Player/CardDeckController.cs) |
| Free ground place (cards) | [`BuildBuildingCommand`](Assets/Scripts/Commands/BuildBuildingCommand.cs) + [`BottomBarActionsUI`](Assets/Scripts/UI/Containers/BottomBarActionsUI.cs) |
| Power place-gate | **Retired** — place freely; power raises production to full rate |
| Power efficiency | [`BaseBuilding.ProductionEfficiency`](Assets/Scripts/Units/BaseBuilding.cs) — unpowered **20%**, powered **100%** |
| Between-Act shop | [`BetweenActShopUI`](Assets/Scripts/UI/BetweenActShopUI.cs) — **Terra-Coin upgrades** (carry) |
| Weeks left (left HUD) | [`WeeksLeftUI`](Assets/Scripts/UI/Containers/WeeksLeftUI.cs) |
| Sector travel / names | [`SectorTravelUI`](Assets/Scripts/UI/Containers/SectorTravelUI.cs) + **Q/E** in [`PlayerInput`](Assets/Scripts/Player/PlayerInput.cs) |
| Tile snap / place SFX | [`AudioManager`](Assets/Scripts/Audio/AudioManager.cs) (`PlayTileSnapSound` / `PlayPlaceClickSound`) |
| Pause Music / SFX sliders | [`PauseMenuUI`](Assets/Scripts/UI/PauseMenuUI.cs) (runtime under `Menu Panel`; prefs `tt_music_volume` / `tt_sfx_volume`) |

---

## 0.1 What is NOT the game (do not reintroduce)

| Retired idea | Status |
|--------------|--------|
| Win = Temp + Atmos + Water **alone** (no score/weeks) | **Retired** |
| Hamster MVP one-round climate victory | **Retired** |
| Sector unlock / colonization as Act progression | **Retired** — Acts ≠ sectors; CP expands map |
| Act count = sector count | **Retired** — fixed 5-Act ladder |
| Sector build lock / active-sector-only pads | **Retired** |
| Card play gated by Materials | **Retired** under Colony Acts — placement is free; **Terra-Coins** are shop-only |
| Combat / hazard HP damage | **Retired** — [`DamageRules.Enabled`](Assets/Scripts/Units/DamageRules.cs) is false; TakeDamage is a no-op |
| Power required to place / hard-gate climate | **Retired** — unpowered still crawls at 20%; power restores full rate + score boost |
| Materials between-Act tile shop | **Retired** — shop is Terra-Coin **roguelike upgrades** |
| Card play gated by drones / reserved pads | **Retired** (cards self-construct) |
| Auto-discard “unplayable” hand cards | **Retired** |
| Force-seat Solar / climate / Mining Drone into hand | **Partial** — Solar always seated; Continue grants Solar only; climate seats when unmet; free Mining Drone is an **in-world unit** per CP sector |
| Continue grants Command Post for next sector | **Retired** — player plays CP when affordable |
| Climate soft-gates blocking card draw/select | **Retired** |
| Oxygen / Power / Pop as win primaries | **Retired** |
| Strategic fog / hex shroud | **Retired** — full planet visible (Combolands) |
| Mining / materials depletion as run loss | **Retired** (while Colony Acts are active) |
| Minimap Camera / live RT | **Never shipped** — only empty Bottom Bar shell; schematic [`MinimapUI`](Assets/Scripts/UI/Containers/MinimapUI.cs) is the implementation |

Climate tickers **do** count for Act clear (with Colony Score). They are not the *only* win meter.

---

## 0.2 Acts (milestones) — fixed ladder (≠ sectors)

**Five Acts** regardless of map size:

| Act | Name | Score | Weeks |
|-----|------|------:|------:|
| 1 | Establish | 30 | 18 |
| 2 | Survive | 140 | 16 |
| 3 | Settle | 220 | 16 |
| 4 | Expand | 300 | 16 |
| 5 | Thrive | 400 | 18 |

* **Act clear = Colony Score target AND Temp/Atmos/Water deltas** from Act baselines (+15°C / +0.25 atm / +5%). Each sector may contribute at most **1/N** of those deltas (no single Air farm clears multiple sectors' worth). Powered = full rate; unpowered = 20%.
* **Oxygen** (flavor HUD) also capped at **100/N %** per sector.
* **Run win** = all Acts cleared **and** every planet sector terraformed (player CP + Heat/Air/Water trio in that sector).
* **Command Posts** claim the **sector you are viewing** (Q/E or minimap) — not the first free sector on the map. Ghost snaps to that sector's CP pad; already-claimed sectors toast an error.
* **Terra-Coins** earn on Act clear: `15 + floor(score/10) + floor(excess/5)` and **carry** for the run. Shop sells upgrades (+weeks, +score %, geology bonus, adjacency, power score, climate pack).
* **Placement score** shows as world `+N` popups. Geology matches (mine on deposit, aquifer on WaterDeposit, Subglacial on Glacier, Geothermal on Volcano, Lava Tube on LavaTube, Magnetic Shield / Sector Command on FaultLine) add bonus score + climate resource pulses.
* **Geology hard locks** (red ghost + toast outside the feature):

| Card / building | Required feature |
|-----------------|------------------|
| Water Ice Aquifer | `WaterDeposit` |
| Subglacial Water Extractor | `Glacier` |
| Geothermal Generator | `Volcano` |
| Lava Tube Outpost / Subterranean Apartment | `LavaTube` |
| Magnetic Shield / Sector Command Center | `FaultLine` |
| Deep-Core Mining Laser | Minerals deposit tile |
| Basalt Strip-Mine | Regolith deposit tile |

* **Mine deposits** show large colored discs + type labels once discovered (Minerals/Gas from start). Old node-shroud no longer hides them.
* **Q / E** page sectors; on-screen sector names; **no FoW**.
* **Card week costs vary** (0–2). Spend via `SpendWeeks`.
* **Power** boosts production efficiency (and still grants placement score); not a place gate.
* On Act clear: **upgrade depot** (Terra-Coins) is mandatory; Continue grants **Solar**; ~**25%** score (+ excess) carries.
* Weeks hit 0 without Act requirements → **Act fail / run loss**.

### Permanent UX (do not regress)
| Feature | Rule |
|---------|------|
| Between-Act shop | Terra-Coin upgrades; [`BetweenActShopUI`](Assets/Scripts/UI/BetweenActShopUI.cs) |
| Act Continue bootstrap | Solar only via `GrantSectorTransitionBootstrap` |
| Free sector Mining Drone | In-world at Command Post via [`SectorMiningDroneBootstrap`](Assets/Scripts/Utilities/SectorMiningDroneBootstrap.cs) |
| Sector travel | **Q/E** + [`SectorTravelUI`](Assets/Scripts/UI/Containers/SectorTravelUI.cs) |
| Minimap | Schematic overlay in scene [`Minimap Container`](Assets/Scripts/UI/Containers/MinimapUI.cs) (user-placed); click to jump |
| No FoW | Hex shroud fully revealed on planet gen |
| Hand scroll | Offset preserved; ~2.5 cards/wheel-notch |
| Top strip | Shows **Terra-Coins** during Colony Acts (`EnsureMaterialsMetricVisible`) |
| Map score FX | [`PlacementScorePopup`](Assets/Scripts/UI/PlacementScorePopup.cs) |
| Emergency Caches | Excluded from Colony Acts deck |

---

## 0.3 Loop (how a play works)

1. **Hand** — place tiles freely (no Materials). Weeks spend on play.
2. **Geology** — mines on deposit discs; Aquifer→WaterDeposit; Subglacial→Glacier; Geothermal→Volcano; Lava Tube/Subterranean→LavaTube; Magnetic Shield/Sector Command→FaultLine. Wrong place shows a toast + banner.
3. **Score / climate** — adjacency + power score; climate/production at **20%** until grid-powered, then full.
4. **Act clear** → Terra-Coins awarded → upgrade shop → next Act (or win).
5. **Q/E** between sectors; Command Posts expand the map.

**Power note:** Generators restore full production on connected tiles and still grant placement score. Nothing is blocked from placing or crawling unpowered.

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
| Climate pair Heat↔Air, Air↔Water, Water↔Heat | +4 **and** queues missing third climate card (also when both partners exist in focus sector) |
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
| Card UI | Lower-left hand (~5-card viewport + `RectMask2D`); hover lights the dock; wheel scrolls horizontally and **stays put** (no snap-back on hand seating); cost chip shows week cost |
| Instant card place | Completes immediately; week spent on consume; score deferred until after week when needed |

---

## 3. Support systems (keep, demoted)

### 3.1 UI chrome
* Hand lower-left; selection info far right; top resource strip for readability with **fixed-width** metric boxes (no digit jitter).
* Goal colors still tint Heat / Air / Water card accents — cosmetic.

### 3.2 Deck
* FIFO draw pile / discard; hand size 5.
* No climate force-seat; no purge for soft gates.

### 3.3 Colony integrity / UCC
* **UCC deleted from the play scene** (was `GlobalCommander` / Universal Command Center). Do not re-place it. Colony status lives in **Active Objectives** / top HUD; bases are **Command Posts**. Integrity still inactive until first real `(Clone)` building.

### 3.4 Prefabs / ghosts
* Prefer `BaseBuilding` variants; card ghost from command template when available.
* Site-marker ghosts must not steal pads or complete as real buildings.

---

## 4. Condensed fix memory (do not reintroduce)

* Climate-trio / `TriggerMvpVictory` as win  
* Sector lock or UnlockNextSector on Act clear  
* Selectable Universal Command Center (UCC / `GlobalCommander`) hub with health UI  
* Auto-discard hand for Materials / pads / climate gates  
* Force-seat Water / drone reshuffling the hand (Solar + Command Post **are** granted on between-Act Continue)  
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

*Last rewritten: 2026-09-12 — Command Posts / drones use focused sector (Q/E); full geology card↔feature map.*
