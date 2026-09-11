### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md) — lore only; **this file wins** on run rules
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Authoritative Run Model — Combolands Colony Acts

**One-sentence game:** Draw building **tiles** → place on the ground → spend **weeks** → earn **Colony Score** (base + adjacency) **and** push planet **Temp / Atmos / Water** → clear a fixed Act ladder → expand by playing **Command Posts** into new sectors → terraform **every** sector to win.

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) — place tiles for score under a turn budget; position and stacking matter. **Acts are independent of sectors.** Geography expands when you afford Command Posts.

### Owner scripts
| Role | Script |
|------|--------|
| Acts, weeks, score, win/fail | [`ColonyActManager`](Assets/Scripts/Player/ColonyActManager.cs) |
| Tile grid / join snap | [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) |
| Hand draw / consume card | [`CardDeckController`](Assets/Scripts/Player/CardDeckController.cs) |
| Free ground place (cards) | [`BuildBuildingCommand`](Assets/Scripts/Commands/BuildBuildingCommand.cs) + [`BottomBarActionsUI`](Assets/Scripts/UI/Containers/BottomBarActionsUI.cs) |
| Power place-gate | [`PowerGridManager`](Assets/Scripts/Environment/PowerGridManager.cs) (`CanPlayBuildingForPower`) |
| Look / flora (no FoW) | [`ClimateVisualStages`](Assets/Scripts/Environment/ClimateVisualStages.cs), [`VegetationManager`](Assets/Scripts/Environment/VegetationManager.cs) |
| Objectives HUD | [`ActiveObjectivesUI`](Assets/Scripts/UI/Containers/ActiveObjectivesUI.cs) |
| Between-Act shop | [`BetweenActShopUI`](Assets/Scripts/UI/BetweenActShopUI.cs) (permanent; Materials offers) |
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
| Card play gated by Materials | **Active again** for Colony Acts card places (store economy); lean starting Materials |
| Card play gated by drones / reserved pads | **Retired** (cards self-construct) |
| Auto-discard “unplayable” hand cards | **Retired** |
| Force-seat Solar / climate / Mining Drone into hand | **Partial** — Solar always seated; Continue grants Solar only; climate seats when unmet; free Mining Drone is an **in-world unit** per CP sector |
| Continue grants Command Post for next sector | **Retired** — player plays CP when affordable |
| Climate soft-gates blocking card draw/select | **Retired** |
| Oxygen / Power / Pop as win primaries | **Retired** |
| Strategic fog / hex shroud | **Retired** — full planet visible (Combolands) |
| Mining / materials depletion as run loss | **Retired** (while Colony Acts are active) |
| Emergency Caches free Materials card | **Retired** from Colony Acts deck |

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

* **Act clear = Colony Score target AND Temp/Atmos/Water deltas** from Act baselines (+15°C / +0.25 atm / +5%). Planet-wide gains — climate buildings in **any** sector tick.
* **Run win** = all Acts cleared **and** every planet sector terraformed (player CP + Heat/Air/Water trio in that sector). Final Act also requires full-sector terraform.
* **Command Posts** (when Materials allow) **auto-claim the next free sector** (repeatable). Acts do not unlock sectors.
* **Q / E** page sectors (CP if present, else center). On-screen sector name labels are clickable.
* **No FoW / shroud** — full map visible; climate mood fog only.
* **Card week costs vary** (not everything is 1): scout/discover **0**; power / climate / housing / life **1**; mines / Command / Spaceport **2**. Spend via `SpendWeeks`.
* **Power cards:** Solar Panel, Geothermal, Magnetic Shield — hand always keeps at least one generator; draw pile has extra Solar + Geothermal copies.
* **Climate cards:** if a climate channel is still unmet for this Act, hand keeps at least one matching card. Draw pile extras: +5 Aquifer / +3 Subglacial / +3 Heat / +3 Air.
* **Hand size 24** (scrollable; ~**2.5 cards per mouse-wheel notch**); deck excludes combat clutter (**Barracks**, Infantry School), **shipment** cards, and **Emergency Caches**.
* **Mine / geology tiles** enter the hand after matching deposit or sector feature is discovered.
* On Act clear: **between-Act Supply Depot shop** (Materials → tile offers / reroll) is **mandatory**. Continue grants **Solar** (not Command Post); ~**25%** score (+ excess) carries.
* **One free working Mining Drone unit** spawns at each sector's Command Post (in-world).
* **Oxygen** is flavor / life support — **not** an Act-clear meter.
* Weeks hit 0 without Act requirements → **Act fail / run loss**.

### Permanent UX (do not regress)
| Feature | Rule |
|---------|------|
| Between-Act shop | Always pause on Act clear (non-final); [`BetweenActShopUI`](Assets/Scripts/UI/BetweenActShopUI.cs) |
| Act Continue bootstrap | Solar only via `GrantSectorTransitionBootstrap` |
| Free sector Mining Drone | **In-world unit** at Command Post via [`SectorMiningDroneBootstrap`](Assets/Scripts/Utilities/SectorMiningDroneBootstrap.cs) |
| Sector travel | **Q/E** + [`SectorTravelUI`](Assets/Scripts/UI/Containers/SectorTravelUI.cs) names |
| No FoW | Hex shroud fully revealed on planet gen |
| Hand scroll | Wheel/trackpad scrolls the strip; **offset is preserved** across hand refreshes/layout (do not reset to 0 on `OnHandChanged`). Re-apply after layout. ~2.5 cards/wheel-notch (`BottomBarActionsUI.scrollCardsPerNotch`); trackpad uses gentler scaling |
| Top resource strip | Fixed-width metric boxes; **Materials** forced visible/left (`EnsureMaterialsMetricVisible`) |
| Emergency Caches | Excluded from Colony Acts deck |

---

## 0.3 Loop (how a play works)

1. **Hand (up to 24 cards, scrollable)** — at least one **power generator** is always seated. Playing uses a card up; draw fills remaining slots.
2. **Week** — card plays spend a **variable** week cost (`GetWeekCost` / `SpendWeeks`).
3. **Placement gates** — Power, Materials, mines as before. **Command Post** cards auto-seat in the next free sector when affordable (repeatable).
4. **Free tile placement** — non-CP cards snap to the 12 m grid; complete instantly.
5. **Score** — on complete: **Base Score + adjacency** (+ Habitability for climate tags).
6. **Climate** — powered Heat/Air/Water buildings **anywhere** tick Temp / Atmos / Water toward Act gains. Sector terraform (for win) still needs the climate trio **in each sector**.
7. Clear Act when **score AND climate gains** met (Thrive also needs all sectors terraformed) → shop → next Act (or win).
8. **Q/E** jump between sectors; click sector name labels to focus.

**Power note (vs Combolands):** consumers need watts; place them on the same auto-linked cluster as Solar/Geothermal.

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

*Last rewritten: 2026-09-08 — Acts require Colony Score + Temp/Atmos/Water deltas; Combolands placement rules unchanged.*
