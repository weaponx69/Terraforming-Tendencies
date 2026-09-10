### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md) — lore only; **this file wins** on run rules
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Authoritative Run Model — Combolands Colony Acts

**One-sentence game:** Draw building **tiles** → place on the ground → spend **weeks** (cost varies by card) → earn **Colony Score** (base + adjacency) **and** push **Temp / Atmos / Water from the current sector** → clear one Act per sector before weeks run out → win the planet.

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
| Pause Music / SFX sliders | [`PauseMenuUI`](Assets/Scripts/UI/PauseMenuUI.cs) (runtime under `Menu Panel`; prefs `tt_music_volume` / `tt_sfx_volume`) |

---

## 0.1 What is NOT the game (do not reintroduce)

| Retired idea | Status |
|--------------|--------|
| Win = Temp + Atmos + Water **alone** (no score/weeks) | **Retired** |
| Hamster MVP one-round climate victory | **Retired** |
| Sector unlock / colonization as progression | **Retired** (locks/pads) — **Acts now = sectors** |
| Sector build lock / active-sector-only pads | **Retired** |
| Card play gated by Materials | **Active again** for Colony Acts card places (store economy); lean starting Materials |
| Card play gated by drones / reserved pads | **Retired** (cards self-construct) |
| Auto-discard “unplayable” hand cards | **Retired** |
| Force-seat Solar / climate / Mining Drone into hand | **Partial** — Solar **is** always seated; climate seats **only when that Act channel is unmet**; Mining Drone force-seat stays retired |
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
| First | Survive · Sector 1 | 40 | 16 |
| Middle | Settle / Expand · Sector N | 40 + 35×(N−1) | 14 |
| Last | Thrive · Sector N | … | 16 → **run victory** |

* **Card week costs vary** (not everything is 1): scout/discover **0**; power / climate / housing / life **1**; mines / Command / Spaceport **2**. Spend via `SpendWeeks`.
* **Act clear = Colony Score target AND Temp/Atmos/Water deltas** from that Act’s baselines (+15°C / +0.25 atm / +5%). These are **gains per sector**, not absolute planet floors (HUD shows `+gain / +need`).
* **Climate ticks** from powered Heat/Air/Water buildings in the **current focus sector** only (auto-link to nearby generators within 4 tiles). Unpowered consumers do nothing.
* **Power cards:** Solar Panel, Geothermal, Magnetic Shield — hand always keeps at least one generator; draw pile has extra Solar + Geothermal copies.
* **Climate cards:** if a climate channel is still unmet for this Act, hand keeps at least one matching card (need-based seat — no Water famine). Draw pile extras: +5 Aquifer / +3 Subglacial / +3 Heat / +3 Air. Heat+Air in sector still queues a Water combo offer once per Act.
* **Hand size 24** (scrollable); deck excludes combat clutter (**Barracks**, Infantry School) and **shipment** cards (instant Materials/Biomass dumps). Spaceport and Deploy Engineer stay.
* **Mine tiles** only enter the hand after a matching deposit is **discovered**; clicking the card **auto-builds on that deposit’s tile**.
* **Climate card colors:** Heat=amber, Atmos=fuchsia, Water=**blue**. Heat badges say **Play w/ Atmos → Water card**; Atmos badges say **Play w/ Heat → Water card**.
* On clear: camera pans to the next sector; climate baselines reset; ~**25%** score (+ excess) carries.
* Weeks hit 0 without both requirements → **Act fail / run loss**.
* Final sector Act clear → victory.

---

## 0.3 Loop (how a play works)

1. **Hand (up to 24 cards, scrollable)** — at least one **power generator** is always seated (Solar Panel, Geothermal, Magnetic Shield, …). Playing uses a card up; draw fills remaining slots. Cards keep a fixed playing-card size; hover the hand and use the **mouse wheel** to scroll when more than ~5 cards are held. The draw pile is **not** capped at 5 — extras of Solar/Geothermal keep power available.
2. **Week** — card plays spend a **variable** week cost (`GetWeekCost` / `SpendWeeks`). Scout **0**; climate/power/housing **1**; mines/Command/Spaceport **2**.
3. **Placement gates**
   * Power: if `PowerUpkeep > 0` and board generation cannot cover **board upkeep + this tile**, placement is blocked.
   * Materials: card buildings spend Materials on place (store / roguelike). Start ~250; Colony Acts prices are scaled (~35% of old RTS costs). Demolish refunds 50%.
   * Mines: require a discovered matching deposit and placement **on that deposit’s tile**.
   * Generators / zero-upkeep tiles always place (power-wise).
   * **Demolish:** right-click a building → context menu (**Demolish** / **Repair**). Delete/Backspace also works. Refunds Materials (50% completed / 75% under construction).
   * Health bars sit under every building so repair state is always visible.
4. **Free tile placement** — card ghost snaps to the 12 m grid under the cursor (sticky cell). Click places; building **completes instantly** (**no drone** / no rise timer). Score deferred until after the week spend; climate applies on complete. Ghost snap + place play short SFX.
5. **Score** — on complete: **Base Score + adjacency** (+ Habitability for climate tags). Completing a building also **queues its old RTS production options as hand cards** for the next fill (no build menu on the structure).
6. **Climate** — powered Heat/Air/Water buildings in the **current Act focus sector** tick Temp / Atmos / Water (rates tuned so one tile takes ~1–2 minutes to clear a channel, not seconds). Auto power-link reaches generators within **4** tiles. Unpowered = no climate. Prior-sector buildings stop pushing meters. Act clear needs **gains** of +15°C / +0.25 atm / +5% from that Act’s baselines. Placing a Heat/Air/Water tile next to another climate-pair neighbor **offers the missing third** as a hand card (once per Act).
7. Clear Act when **score AND climate gains** are both met before weeks run out → next sector (or win).

**Power note (vs Combolands):** Combolands has no power grid — adjacency is for score. Here, consumers need watts; place them on the same auto-linked cluster as Solar/Geothermal (within a few tiles is enough; they do not have to share an edge with Solar if another powered neighbor bridges).

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
