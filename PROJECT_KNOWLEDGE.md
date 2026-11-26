### Terraforming Tendencies — Project Knowledge & Architecture Notes

**Agents:** Do not read this file end-to-end. Start at [`AGENTS.md`](AGENTS.md) (task router). Default skim: **§0** + **§2** only. Cursor rules (`vertical-slice`, `player-verify`, `rts-perf-budget`) load automatically.

**📚 Central Hub Documentation**
* **Agent entry:** [`AGENTS.md`](AGENTS.md)
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md) — lore only; **this file wins** on run rules
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **RTS / main-thread FPS guardrails:** [`.cursor/rules/rts-perf-budget.mdc`](.cursor/rules/rts-perf-budget.mdc) (summary **§4.1**)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

**Visible files:** New designs go in `docs/specs/`. New plans go in `docs/plans/`. Do not write them under a folder or file whose name starts with `.` (`.summer/`, `.cursor/`, and the rest) except maintaining [`.cursor/rules/`](.cursor/rules/). See [`AGENTS.md`](AGENTS.md).

---

## 0. Authoritative Run Model — vertical slice (today: “Prologue”)

**The Prologue is not a separate mini-game or permanent “Prologue module.”** It is the **vertical slice** of Terraforming Tendencies: the same loop the full game will ship (planet surface, sector deposits, survey-as-puzzle, typed resources, cards, placement, build weeks, End Week, corporate pressure). Scripts and types still live under `Prologue/` and `PrologueController` because that slice shipped first — **new work should generalize those patterns**, not fork parallel “Acts-only” or “prologue-only” mechanics unless explicitly archiving legacy code.

**Agents:** [`AGENTS.md`](AGENTS.md) router; [`vertical-slice-not-module.mdc`](.cursor/rules/vertical-slice-not-module.mdc) (`alwaysApply`). In owner chat, call this **the game / main run**, not “prologue work”; `Prologue*` paths are legacy names for the authoritative run.

**One-sentence game:** Frostpunk 2–like opening contract on a flat hex board — find ice, drill it, power an Oxygen Processor, house the shuttle, survive the dust storm — via **cards** and **End Week** (O2 / beds / power pressure). Survey exploration should play as a **spatial puzzle** (Blue Prince–like), not casual fog-clear. **Future:** same run model widens (more sectors, planet pick at start); the slice is the prototype, not an isolated tutorial.

**Verification:** Player-style runs through this slice ([`docs/specs/2026-11-23-player-verification-policy.md`](docs/specs/2026-11-23-player-verification-policy.md), [`tools/prologue-ui-playthrough.sh`](tools/prologue-ui-playthrough.sh)) count as verifying **the game**, not a optional side quest. **Agents resuming automation:** read **§10.1** before changing the bot or claiming until-win is done.

**Source of truth:** Unreal `TerraformTendencies` Prologue (`TTStory` / `TTColony`). Spec: [`docs/specs/2026-11-18-unity-unreal-prologue-clone-design.md`](docs/specs/2026-11-18-unity-unreal-prologue-clone-design.md). Plan: [`docs/plans/2026-11-18-unity-unreal-prologue-clone.md`](docs/plans/2026-11-18-unity-unreal-prologue-clone.md).

**Colony Acts (Combolands Materials quotas / adjacency combos / Terra-Coins) is ARCHIVED** — code may remain but must not `BeginRun` when [`PrologueController`](Assets/Scripts/Prologue/PrologueController.cs) is active.

### Owner scripts (Prologue)
| Role | Script |
|------|--------|
| Prologue sim (week, O2, steps) | [`PrologueColony`](Assets/Scripts/Prologue/PrologueColony.cs) |
| Scene boot / board sync | [`PrologueController`](Assets/Scripts/Prologue/PrologueController.cs) |
| Constants / buildings | [`PrologueData`](Assets/Scripts/Prologue/PrologueData.cs) |
| Research projects (week costs / unlocks) | [`PrologueResearch`](Assets/Scripts/Prologue/PrologueResearch.cs) on `colony.Research` |
| Card ↔ building map | [`PrologueCardMap`](Assets/Scripts/Prologue/PrologueCardMap.cs) |
| Survey paint trail | [`PrologueDroneScan`](Assets/Scripts/Prologue/PrologueDroneScan.cs) |
| Survey Team day job + anim | [`PrologueSurveyMission`](Assets/Scripts/Prologue/PrologueSurveyMission.cs) |
| Typed resource stocks + mine harvest | [`PrologueResourceId`](Assets/Scripts/Prologue/PrologueResourceId.cs), [`PrologueMineExtractor`](Assets/Scripts/Prologue/PrologueMineExtractor.cs), [`PrologueHarvestDrones`](Assets/Scripts/Prologue/PrologueHarvestDrones.cs); extractors: Deep-Core / Basalt / **Gas Well** / **Iron Ore Extractor** |
| Objective / AIR / End Week HUD | [`PrologueHUD`](Assets/Scripts/UI/PrologueHUD.cs) |
| Hex snap (shared) | [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) |
| Prologue-gated hand | [`CardDeckController.RebuildPrologueHand`](Assets/Scripts/Player/CardDeckController.cs) |

**v1 delta vs Unreal:** Steel/Concrete/builder pool not ported — builds spend **Minerals** (`PrologueData.MineralCost` / `MineMineralCost`) and take **BuildWeeks** plus haul weeks from the nearest finished building (`PrologueData.HaulWeeks`, `TryPlace` + End Week). A closer finished building shortens a job already underway. Landing seeds `StartingMinerals` (72 — two Aquifers, Oxygen, two Habitats). Storm power is the **Geothermal Generator** card and mesh, placed beside the Oxygen Processor. **No Solar card** in Prologue — camp is a virtual energy source until the dust storm; after that, NeedsPower buildings need a **connected** path (Tier‑1 pressurized tubes, Dist≤1) to a generator. Finished Prologue builds get `PowerNode` auto-links (`NotifyProloguePowerLink` → tubes). **Camp energy pool:** each End Week refills `colony.Energy` from camp generation (`CampEnergyGeneration`, cut when storm-darkened) plus finished generators' `PowerOutput`. HUD shows camp/grid energy status (not the old camp/geothermal/NONE-only string). Mines are **ghost-placed** on deposits (never auto-build).

**Planet surface (2026-11-22):** Easy shape is still `PlanetConfig.Heightmap`. Hills and valleys are the terrain photo `Albedo.jpg` plus `NormalFromDEM.png` (assigned, not the soft `GroundMacro` tint). Dust is the repeating regolith `DetailAlbedo` / `DetailNormal` at `DetailTiling` 32. The dust normal scale is 0.35 so grit does not replace those landforms. **Import cap 2048** on hero albedo/normal; **global texture streaming** on PC/Mobile quality (planet maps flagged in importer — see `Assets/Art/Planets/Easy/SOURCES.md`). Spec: [`docs/specs/2026-11-21-planet-surface-dem-design.md`](docs/specs/2026-11-21-planet-surface-dem-design.md). Plan: [`docs/plans/2026-11-21-planet-surface-dem.md`](docs/plans/2026-11-21-planet-surface-dem.md). Grounding: `ColonyTileGrid.SampleGroundY` / `ApplyGroundY` / `CellToWorldOnGround`.

**Playability (2026-11-18 blind playtest):** Prologue completes step 0→6 with End Week. HUD owns the top of the screen. The old Acts supplies bar, top resource strip, weeks panel, objectives panel, and center debug plate are gone from the scene. The hand-built card slots, menu button, and minimap frame stay in the scene because play still uses them, and they are saved inactive so they do not draw before Play. Prologue HUD is built in code. Board seeds ≥2 ice pads; step-4 aquifer must reach the *new* ice. Screenshots under `Assets/Playtest/` (visible folder; not under `.summer/`).

**Survey Team camera:** Playing the **Survey Team** card must **not** auto-pan the camera (player pans manually). Other cards unchanged. [`.cursor/rules/survey-team-no-camera-fly.mdc`](.cursor/rules/survey-team-no-camera-fly.mdc).

**Time-lapse:** [`PrologueTimeLapse`](Assets/Scripts/Prologue/PrologueTimeLapse.cs) drives `Time.timeScale` (default **paused** = 0; 1× ≈ 20s/week) so anims/builds/weeks share one clock. Banner `II 0.5x 1x 2x 4x`; **Space** toggles pause. **Survey Team** card: stays in the hand whenever the team is free (not only on step 0 / step 4). Play → paint up to **5** tiles → green ✓ → **3 colony days** tick, then reveal. While the team is searching the card leaves the hand; if time is paused the days do not move — hit **1x** or **Space**. Paint clicks use the player camera. A new stroke can start on the camp or on any tile of a path already surveyed, and the next tile of that stroke still has to touch the last one. **Orbital Scan** (company hire, **`OrbitalScanEnergyCost` energy** from the camp pool — not minerals) seats while unrevealed ice remains: it pings the nearest ice with a pulsing yellow ? and a direction from camp; it does not clear fog. **Research** card seats when no project is Active and a Tier‑2 id is still locked: play opens the pause **research ladder** to pick a project (week costs via `PrologueData.ResearchWeeks`); Tier‑1 Connected Power starts unlocked. Pause (Esc / Menu) always shows the ladder in Prologue — active + weeks remaining, locked Tier‑2+, and Begin when no Active. Completions tick on End Week (`Research.TickWeek`). Walking crew and harvest drones are hidden for now; the build-over-time and the dirt road stay. ([`PrologueSurveyMission`](Assets/Scripts/Prologue/PrologueSurveyMission.cs); spec [`docs/specs/2026-11-19-prologue-survey-team-design.md`](docs/specs/2026-11-19-prologue-survey-team-design.md)). Card stays in hand until ✓. Searched hexes stay dark; a dirt road runs from the Command Post along the trail ([`PrologueSearchedTiles`](Assets/Scripts/Prologue/PrologueSearchedTiles.cs)). Deposits stay **?** until searched ([`PrologueResourceFog`](Assets/Scripts/Prologue/PrologueResourceFog.cs)); a surveyed deposit then shows a see-through low-poly silhouette of the building card that belongs there (cached site mesh, not the full Tripo prefab), and that ghost leaves as soon as the building is placed (scaffolding stays until the weeks finish). The Oxygen Processor makes 12 air a week so the camp stays ahead of the 18 people after the shuttle (half a unit each) ([`PrologueRevealGhosts`](Assets/Scripts/Prologue/PrologueRevealGhosts.cs)). Playing that card builds it over its **BuildWeeks** on the colony clock ([`PrologueConstruction`](Assets/Scripts/Prologue/PrologueConstruction.cs)). **Every building uses scaffolding.** The mesh stays on the pad; orange scaffolding stands until the weeks (or the non-prologue build timer) finish, then comes down. Nothing rises out of the ground ([`ConstructionScaffold`](Assets/Scripts/Units/ConstructionScaffold.cs)). No construction crew walks out. Water Ice Aquifer keeps the drill-rig mesh ([`WaterIceAquiferMeshBuilder`](Assets/Scripts/Units/WaterIceAquiferMeshBuilder.cs)) when the build finishes, on the same pad it was placed. Command Post does **not** auto-spawn a gather drone. Aquifer places by ice range (not Acts WaterDeposit geology).

**Building meshes (2026-11-21):** Every themed building that was a Unity primitive (`Placeholder Visual`) now has a baked mesh on the prefab, under `Assets/Units/Buildings/Themed/SiteMeshes/`. The Deep-Core Mining Laser is a pit with a gantry, not a standing cylinder. The Command Post prefab uses that same low habitat mesh (`CommandPost.asset`); Building_02 is turned off. The Habitat card uses `HabitatDomes.asset`: a living dome joined to a smaller airlock, with cyan windows, a door, and an antenna, sized to one hex (about 3.7 m across) so a neighbor does not sit inside it. The old Tripo mesh on that prefab is turned off. The Habitat placement ghost uses that same dome mesh and a see-through material (`Habitat Ghost Variant`); the old solid Tripo ghost is turned off. The Oxygen Processor uses `OxygenProcessor.asset`: a plant hall, two cyan tanks, and a short stack, also inside one hex. Its placement ghost is that same mesh, see-through (`Oxygen Processor Ghost Variant`); the stretched Tripo monolith is turned off. Right-click or Escape puts a placement ghost away. Escape does not open pause while that ghost is up. [`ColonyGroundMesh`](Assets/Scripts/Units/ColonyGroundMesh.cs) leaves a child named `Building Mesh` alone.

**Colonist contract (2026-11-22):** Cards still aim a job at a tile. Colonists build it and run terraforming only while satisfaction is at least 40. The corporation cancels at satisfaction 0, faith 0, or a dust storm with no air. Faith falls on a week the current step does not advance. HUD shows satisfaction, whether the crew is working, and faith. A stats panel on the right lists the week's jobs and all six prologue goals, with beds, air, ice, and minerals on hand. A hand card that costs more minerals than are on hand stays visible and grey, and cannot be played, until the stock covers it. Aiming a card that can be paid shows whether that pad can be built and the mineral cost against the stock. A mine ghost locks to the surveyed deposit it is aimed at, and the click builds that same pad. At the start of the run Mara Voss comes on, names the first job, and the clock waits until Continue. After each End Week a person reports what moved and what is needed next, and the clock waits until Continue: Mara Voss when the crew worked, Ivo Pell when the week slipped, and Auditor Kline in a cutscene when the storm hits or the corporation cancels. Survey hints stay a corner note and do not bring that person back. Once the prologue is already complete, later weeks do not open the report again. A cancelled contract ends the run: the clock stays stopped, cards and End Week do nothing, and New game reloads the scene. Spec: [`docs/specs/2026-11-21-colonist-corp-contract-design.md`](docs/specs/2026-11-21-colonist-corp-contract-design.md).

**Design intent (2026-11-20):** Not casual — Frostpunk 2 tone. Survey Team ground capsules = **spacesuit people** (placeholder art). Survey = puzzle routing (Blue Prince–like). Resources stay **individual stocks**; **planet type** chooses which exist via `PlanetConfig` deny-list (`DenyMinerals` / `DenyGas` / `DenyIron` / `DenyRegolith`; missing = allow all); **future** pre-run planet select. Mine-on-deposit (`PrologueMineExtractor`) credits typed stocks on End Week (8/wk) + Survey-style harvest drones. Specs: [`docs/specs/2026-11-20-individual-resources-design.md`](docs/specs/2026-11-20-individual-resources-design.md), [`GDD.md`](GDD.md) § Current design decisions.

### Archived — Colony Acts (do not treat as run model)
| Role | Script |
|------|--------|
| Acts, weeks, score, win/fail | [`ColonyActManager`](Assets/Scripts/Player/ColonyActManager.cs) — **archived** |
| Tile grid / join snap | [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) |
| Hand draw / consume card | [`CardDeckController`](Assets/Scripts/Player/CardDeckController.cs) |
| Free ground place (cards) | [`BuildBuildingCommand`](Assets/Scripts/Commands/BuildBuildingCommand.cs) + [`BottomBarActionsUI`](Assets/Scripts/UI/Containers/BottomBarActionsUI.cs) |
| Power place-gate | **Retired** — place freely; power raises production to full rate |
| Power efficiency | [`BaseBuilding.ProductionEfficiency`](Assets/Scripts/Units/BaseBuilding.cs) — unpowered **20%**, powered **100%** |
| Between-Act shop | [`BetweenActShopUI`](Assets/Scripts/UI/BetweenActShopUI.cs) — congratulatory Terra-Coin depot (exact large fonts) |
| Card disasters | [`ColonyActDisaster`](Assets/Scripts/Player/ColonyActDisaster.cs) + [`NaturalEventManager.TriggerStrike`](Assets/Scripts/Environment/NaturalEventManager.cs) — stronger plays roll for meteors; `DamageRules` on during Acts |
| Weeks left (left HUD) | [`WeeksLeftUI`](Assets/Scripts/UI/Containers/WeeksLeftUI.cs) |
| Colony Acts panel (right) | [`ActiveObjectivesUI`](Assets/Scripts/UI/Containers/ActiveObjectivesUI.cs) — scrollable (wheel while hovered; camera zoom yields); Corp quota + terraform lead |
| Dev full-run demo | [`ColonyActsDevDemo`](Assets/Scripts/Player/ColonyActsDevDemo.cs) — Editor / DEVELOPMENT **cheat** smoke (grants quota/climate) |
| Dev player bot | [`ColonyActsPlayerBot`](Assets/Scripts/Player/ColonyActsPlayerBot.cs) — **teach + edge-case** bot (Watch / Stress); narrates plays to Console + Acts banner; CLI [`tools/colony-acts-player-bot-cli.sh`](tools/colony-acts-player-bot-cli.sh) |
| Building nameplates | [`BuildingNameHoverLabel`](Assets/Scripts/UI/Components/BuildingNameHoverLabel.cs) — 14pt, hover only |
| Sector travel / names | [`SectorTravelUI`](Assets/Scripts/UI/Containers/SectorTravelUI.cs) (~18pt labels + Q/E) + **Q/E** in [`PlayerInput`](Assets/Scripts/Player/PlayerInput.cs) |
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
| Free Materials shipments / Emergency Caches in Acts deck | **Excluded** — earn Mats via placement + week mining only |
| Starting Materials seed pile | **0** under Colony Acts |
| Realtime drone gather → Materials during Acts | **Retired** — mining ticks on **SpendWeeks** |
| Combat / hazard HP damage | **Active during Colony Acts** — card-tied disasters via [`ColonyActDisaster`](Assets/Scripts/Player/ColonyActDisaster.cs); wall-clock waves quiet while Acts run |
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
| Changing per-sector contribution share (1/N of reference deltas) | **Forbidden** — each sector may still add at most **reference Δ / N** (N = map sector count). Never switch to claimed-CP-only budgets. **Act clear climate need is also Δ/N** (one sector share) — more sectors → smaller Act climate bar. Reference constants stay +15°C / +0.25 atm / +5%; do not inflate a single sector's share to soft-lock-fix |

Climate tickers **do** count for Act clear (with Colony Score). They are not the *only* win meter.

---

## 0.2 Acts (milestones) — fixed ladder (≠ sectors)

**Five Acts** regardless of map size:

| Act | Name | Corp Quota (Mats) | Weeks |
|-----|------|------------------:|------:|
| 1 | Establish | 700 | 18 |
| 2 | Survive | 900 | 16 |
| 3 | Settle | 1100 | 16 |
| 4 | Expand | 1350 | 16 |
| 5 | Thrive | 1600 | 18 |

* **Act clear = Corp Materials quota (Materials bank) AND Temp/Atmos/Water gains** from Act baselines. On clear, **pay Corp** — deduct the full quota from the Materials bank (Combolands tax); leftover + ~25% of gross seeds the next Act. Need per Act climate = **one sector share**: reference (+15°C / +0.25 atm / +5%) **÷ N**. Each sector may contribute at most that same **1/N**. **Climate and mining produce on week spend** (not real-time): mines / Mining Drones yield ~25 Mats/week (deplete deposits); climate rates are **per week**, × power efficiency × adjacency combo.
* **Starting Materials = 0.** No shipment / Emergency Caches / Discovery bonus Mats during Acts.
* **Placement `+N`** awards **Materials** (bank + Act earned meter), not abstract score.
* **Oxygen** (flavor HUD) also capped at **100/N %** per sector.
* **Run win** = all Acts cleared **and** every planet sector terraformed (player CP + Heat/Air/Water trio in that sector).
* **Command Posts** claim the **sector you are viewing** (Q/E or minimap) — not the first free sector on the map. Ghost snaps to that sector's CP pad; already-claimed sectors toast an error.
* **Card disasters:** Playing cards during Acts rolls a strike chance (week cost / mines / climate / rarity). Prefer card `HazardEventPrefabs`, else fallback meteor. HP damage is **on** during Acts. Wall-clock NaturalEvent waves stay quiet while Acts run.
* **Card rarity (light hook):** reuses [`CardRarity`](Assets/Scripts/TechTree/CardRarity.cs) on [`BlueprintCardSO`](Assets/Scripts/Player/BlueprintCardSO.cs) — Common / Uncommon / Rare / Epic; multiplies disaster chance. Full weighted rare decks are follow-up.
* **Terra-Coins** earn on Act clear: `15 + floor(earnedMats/10) + floor(excess/5)` and **carry** for the run. Shop sells upgrades (+weeks, +score %, geology bonus, adjacency, power score, climate pack).
* **Geology hard locks** (red ghost + toast outside the feature):

| Card / building | Required feature |
|-----------------|------------------|
| Water Ice Aquifer | `WaterDeposit` |
| Subglacial Water Extractor | `Glacier` |
| Geothermal Generator | `Volcano` |
| Lava Tube Outpost / Subterranean Apartment | `LavaTube` |
| Magnetic Shield / Sector Command Center | `FaultLine` |
| Deep-Core Mining Laser | Minerals deposit tile |
| Gas Well | Gas deposit tile |
| Iron Ore Extractor | Iron deposit tile |
| Basalt Strip-Mine | Regolith deposit tile |

* **Mine deposits** show large colored discs + type labels once discovered (Minerals/Gas from start). A see-through building ghost marks the pad. Clicking that ghost places the matching mine on the pad. Prologue spends the mine's mineral stock for that place; it does not apply the Materials cost, which was painting every tile "Can't place".
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
| Sector-filtered hand | Bottom bar shows only cards relevant to **focused sector** (Q/E / minimap): CP on unclaimed; geology/mines matching that sector; other tiles only when claimed |
| Top strip | Shows **Materials**; during Colony Acts shows **Mats/Coins** (`materials / terraCoins`) |
| Map score FX | [`PlacementScorePopup`](Assets/Scripts/UI/PlacementScorePopup.cs) |
| Emergency Caches | Excluded from Colony Acts deck |
| Pipeline Boost | Excluded from Colony Acts deck (retired exploration speed boost) |

---

## 0.3 Loop (how a play works)

1. **Hand** — place tiles freely (no Materials). Weeks spend on play. Hand strip filters to the sector you are viewing.
2. **Geology** — mines on deposit discs; Aquifer→WaterDeposit; Subglacial→Glacier; Geothermal→Volcano; Lava Tube/Subterranean→LavaTube; Magnetic Shield/Sector Command→FaultLine. Wrong place shows a toast + banner. Non-mines cannot place on **discovered** mineable deposits (**Command Posts exempt** — CP pad wins over a deposit on the same tile). Undiscovered Iron/Regolith do not reserve tiles. Deposits are sparse (~5/sector, snapped with spacing) so climate tiles have room.
3. **Score / climate** — low base score; adjacency/combos pay. Climate produces **on week spend** (config rates = per week) at **20%** unpowered / full when powered, then × adjacency climate combo (pair / same-tag / trio / power neighbor). Combo **card offers** require edge adjacency.
4. **Act clear** → Terra-Coins awarded → upgrade shop → next Act (or win).
5. **Q/E** between sectors; Command Posts expand the map.

**Power note:** Generators restore full production on connected tiles and still grant placement score. Nothing is blocked from placing or crawling unpowered.

**Build source:** cards / tiles only. Selecting a building does **not** open an RTS build/train panel.

---

## 0.4 Tile tags, base score, Habitability

| Tag | Examples | Base Score | Habitability |
|-----|----------|------------|--------------|
| Anchor | Command Post, housing | 6 / 5 | — |
| Power | Solar | 2 | — |
| Labor | Mining Drone (non-building card) | 2 | — |
| Industry | Mines | 3 | — |
| Heat | GHG / geothermal / heat buildings | 3 | +8 |
| Air | Condenser / import | 3 | +8 |
| Water | Aquifer / water buildings | 3 | +8 |
| Life | Oxygen Processor | 2 | +3 |
| Other | default | 2 | — |

Non-building cards grant a small flat score on play (no adjacency). **Most score comes from stacking.**

---

## 0.5 Adjacency (stacking)

**Grid:** card buildings snap to a **building-sized flat-top hex grid** ([`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) — outer radius 2.5 m ≈ **5 m across**). Edge-adjacent hexes count for joins/combos (6 neighbors). [`HexGridManager`](Assets/Scripts/Environment/HexGridManager.cs) uses the same spacing. Selection rings / ghost footprints match one hex.

### Placement feedback (ghost)
* Semi-transparent **tile footprint** under the ghost (locks to the snap cell immediately).
* Ghost mesh stays a stable blue/red valid tint (no green strobe on fresnel).
* **Green footprint + join lines** show adjacency; ghost is removed from `ActiveBuildings` so it cannot occupy its own cell.
* **Combo placement halo** ([`PlacementComboPreview`](Assets/Scripts/Player/PlacementComboPreview.cs) via `ColonyActManager.PreviewPlacement`): blue ring on empty edge hexes, role-colored discs/labels on combo partners (same-tag / climate / power / anchor / life / geology), yellow ghost ring, and a floating summary with **score**, **Temp/Atmos/Water per week** (`+°C/wk`, `+atm/wk`, `+%/wk` after power efficiency × adjacency combo), **on-place pulses**, and remaining sector climate headroom. Join lines tint to the link role.

### Score bonuses (per edge neighbor, soft-capped +36)
| Relationship | Bonus |
|--------------|------:|
| Any neighbor | +3 |
| Same tag | +6 |
| Power next to a consumer (upkeep &gt; 0) | +7 |
| Anchor next to anything | +5 |
| Climate pair Heat↔Air, Air↔Water, Water↔Heat | +8 |
| Life next to Water or Anchor | +6 |

### Climate rate combos (per-sector 1/N share unchanged)
| Neighbor situation | Rate multiplier |
|--------------------|----------------:|
| Same climate tag | 1.35× |
| Climate pair edge | 1.75× |
| Mini-trio (Heat+Air+Water adjacent) | 2.25× |
| + hex-edge Power | +0.25× |
| Cap | 2.5× |

First climate-pair join in a sector also pulses ~7% of remaining sector budget (still clamped by 1/N).

### Combo card offers (edge adjacency only, once per Act per goal)
| Pair | Offers |
|------|--------|
| Heat+Air / Air+Water / Water+Heat | Missing climate channel |
| Power+Industry | Life (Oxygen) |
| Anchor+Power | Industry (mine) if deposit in focus sector, else Heat |
| Life+Water | Housing / population |

---

## 0.6 Habitability & planet look

* Habitability accumulates from climate-tagged tiles (Heat / Air / Water / Life).
* [`ClimateVisualStages`](Assets/Scripts/Environment/ClimateVisualStages.cs): Barren → Thaw → Wet → Living from Habitability — **ground tint**, light **linear fog**, **ambient** (Exp² haze retired — was a dust wall when zoomed out).
* [`VegetationManager`](Assets/Scripts/Environment/VegetationManager.cs): flora density from Habitability (not Oxygen, not Atmos supplies).
* **Not** driven by Temp/Atmos/Water win deltas.

---

## 0.7 Board, power, sectors

* **Board:** whole planet open for tile placement; cards snap to [`ColonyTileGrid`](Assets/Scripts/Player/ColonyTileGrid.cs) (building-sized hexes, ~5 m). **Acts** advance sector-by-sector.
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
* Inflating one sector's climate share above 1/N (claimed-only budgets, etc.) to soft-lock-fix Acts  

---

## 4.1 RTS performance (summary)

**Canonical:** [`.cursor/rules/rts-perf-budget.mdc`](.cursor/rules/rts-perf-budget.mdc) (`alwaysApply`). Four traps: spatial index resets in loops, TMP glyph storms, synchronous survey/trail batches, unbounded time-lapse ticks.

**Verify:** `SKIP_ENTER_PLAY=1 ./tools/click-order-diagnostic.sh` — `maxDispatchSpikeMs` &lt; 2.0. Slice load: **§10.1** + [`ProloguePerformanceBudget`](Assets/Scripts/Prologue/ProloguePerformanceBudget.cs).

---

## 5. Backlog (after feel is right)

1. Placement popcorn (+base / +adj floats)  
2. Neighbor “echo” re-score (optional Combolands cascade lite)  
3. Soft-fail / extra weeks  
4. Guilds / heirlooms / councillors — **out of scope** until Acts + stacking feel good  
5. Retarget `./tools/sector-win-cli.sh` → Colony Act bot — **started**: [`tools/colony-acts-player-bot-cli.sh`](tools/colony-acts-player-bot-cli.sh) + [`ColonyActsPlayerBot`](Assets/Scripts/Player/ColonyActsPlayerBot.cs) (improve heuristics over time)  

---

## 10. Unity CLI

See [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md). **Unity MCP deprecated — CLI only.**

Safe while Editor is open: `unity status`, `unity command …`, `unity menu`, `unity eval`.  
Avoid second-Editor paths (`unity test` / `unity build` / `unity run` / `-batchmode`) on this machine.

### 10.1 Prologue UI playthrough harness (agent handoff — 2026-11-23)

**In flight:** Drive the vertical slice to **prologue complete (steps 0→6)** via real UI (not eval cheats). Loop until exit **0**; abort if Editor RSS exceeds limit; notify the owner when stuck or OOM. **Evidence and logs** live under **`Assets/Playtest/`** (visible folder — do **not** write new playtest artifacts under `.summer/` or other dot paths).

| Script | Role |
|--------|------|
| [`tools/prologue-ui-playthrough.sh`](tools/prologue-ui-playthrough.sh) | One attempt: bootstrap (`BeginNewPrologueRunForPlaytest`), step through [`PrologueUiPlaythrough`](Assets/Scripts/Prologue/PrologueUiPlaythrough.cs), screenshots/metrics under `OUT_DIR` (default `Assets/Playtest/…`). **flock** on `/tmp/prologue-ui-playthrough.lock` — only one run at a time. Exit **6** = RAM over `PROLOGUE_RAM_LIMIT_MB` (default **12288** ≈ 12 GiB). |
| [`tools/prologue-ui-playthrough-until-win.sh`](tools/prologue-ui-playthrough-until-win.sh) | Retries until playthrough exits 0; logs to `/tmp/prologue-until-win.log`; optional notify every N failures. |
| [`tools/unity-editor-reset.sh`](tools/unity-editor-reset.sh) | Stop Play, open main scene, close extra **Game** views (fixes “no cameras rendering” when bot + recompile left 4 views open). |
| [`tools/notify-player-help.sh`](tools/notify-player-help.sh) | Appends `Assets/Playtest/player-help-inbox.txt`; desktop `notify-send`; **`GROK_HELP_CMD`** hook. **`NOTIFY_X_CHAT=1`** is **not fully wired** — still needs X plugin + `send_chat_message` to self-thread after `get_users_me` / Chat PIN. Owner handle: **`@HvnlyGamesBoss`**. If X tools show `needsAuth` in an old chat, start a **new Agent chat** (or Reload Window) and verify with `get_users_me`. |
| [`Assets/Scripts/Editor/ProloguePlaythroughMenu.cs`](Assets/Scripts/Editor/ProloguePlaythroughMenu.cs) | Editor menu entry → same shell script. |

**Recommended loop (Editor already in Play on slice scene, scripts compiled):**

```bash
SKIP_ENTER_PLAY=1 SKIP_RECOMPILE=1 ./tools/prologue-ui-playthrough-until-win.sh
```

Other env (see script headers): `OUT_DIR`, `MAX_ATTEMPTS`, `SKIP_ENTER_PLAY`, `SKIP_RECOMPILE`, `PROLOGUE_RAM_LIMIT_MB`. Stop runaway: `pkill -f prologue-ui-playthrough-until-win` and Unity **Stop Play**.

**Bot / editor hooks (recent fixes — do not regress):**

| Area | Change |
|------|--------|
| [`PlayerInput.EditorPlaytestPlaceAtTile`](Assets/Scripts/Player/PlayerInput.cs) | Sticky placement cell; refresh restrictions before commit; grounding via **`CellToWorldOnGround`** (not bare `CellToWorld`); **Water Ice Aquifer** beside ice pads, not on the deposit cell. |
| [`BuildBuildingCommand.CanHandle`](Assets/Scripts/Commands/BuildBuildingCommand.cs) | Skip Acts **Command Post** pad reservation when Prologue is active. |
| [`PrologueUiPlaythrough`](Assets/Scripts/Prologue/PrologueUiPlaythrough.cs) | End **construction** → **End Week** before replaying cards; mineral gates; briefing / `time_1x` / End Week editor paths; **`fps` / `heapMb`** in step JSON. |
| [`BottomBarActionsUI`](Assets/Scripts/UI/Containers/BottomBarActionsUI.cs) | `EditorPlaytestSelectHandCard`, public `PlayBuildingCard` for reliable Habitat/card plays vs flaky UI raycasts. |

**Bootstrap (required):** Every E2E run must call `PrologueController.BeginNewPrologueRunForPlaytest()` before UI steps — see player verification spec. Playthrough waits extended for prologue instance when Play load is slow (**~180×2s**).

**Known pain (Nov 2026):** Survey + Aquifer + Oxygen progressed after placement fixes; **Habitat** was flaky on pure UI clicks (partially mitigated by direct card play). Until-win had many attempts (contract cancel, faith, RAM). **`no_instance`** if Play did not finish loading — use reset script + single Game view. Until-win is **not** signed off until a clean exit **0** through step 6.

**Open work for next agent:**

1. **X:** `get_users_me` → unlock Chat PIN → wire [`notify-player-help.sh`](tools/notify-player-help.sh) to X `send_chat_message` (self DM via `get_chat_conversations` + `participant_query`).
2. **Until-win:** Finish Habitat → step **6** reliably; tune [`PrologueUiPlaythrough`](Assets/Scripts/Prologue/PrologueUiPlaythrough.cs) only with player-verification policy (real UI path).
3. Optional: move `/tmp/prologue-until-win.log` under `Assets/Playtest/logs/` if owner wants repo-visible logs.

**Longer narrative / session detail:** [`docs/plans/2026-11-23-prologue-playtest-agent-handoff.md`](docs/plans/2026-11-23-prologue-playtest-agent-handoff.md) (optional; **§10.1 is canonical** for agents).

---

## 11. Leave alone until Combolands loop is fun

Colonists/tubes as required systems, deep tech trees, combat, AI opponents, weather particles, lakes/oceans — do not expand unless they block Acts, free place, power gate, or adjacency.

---

*Last rewritten: 2026-09-13 — Act climate need = one sector share (Δ/N); ban is only on inflating per-sector 1/N contribution; building-sized hexes; sparse deposits; combo climate rates.*
