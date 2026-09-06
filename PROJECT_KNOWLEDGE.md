### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md)
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§12** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Design North Star (Authoritative — 2026-09)

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) — a mini roguelike city-builder where buildings are cards, the map is the board, adjacency/cascades drive “number go up,” and runs are paced by **milestones under a clock**.

**Our translation:** a terraforming city-builder where the fun is **reading the whole board, playing decisive cards, and watching climate + adjacency engines stack** — not exclusive per-sector micromanagement.

### Core pillars

| Combolands | Terraforming Tendencies |
|---|---|
| Score / population milestones | **Planet terraforming goals** (Temperature / Atmosphere / Water; Oxygen later if needed) |
| Weeks on the clock | Generation / action budget / idle-turn pressure |
| Place building → combo cascade | Place climate/infra card → **board-wide climate tick + adjacency multipliers** |
| Guild pair (run identity) | Climate specialty / tech path (Heat / Air / Water / Industry) — future |
| Whole island scores | **All completed buildings always contribute** |
| Cascades | GHG + geothermal, condenser + solar, aquifer + ice, etc. |

### Design rules agents must follow

1. **Milestones = terraforming goals on the whole planet.** Hitting Temp / Atmos / Water thresholds (absolute or round deltas from run/tier baselines) is the win path for a generation/tier.
2. **Whole board, not exclusive sectors.** Climate generation must consider **every** powered, completed climate building. Do **not** gate Temp/Atmos/Water on `ActiveSector` / `TerraformingSector` exclusivity going forward.
3. **Sectors are map flavor and expansion space**, not serial mini-games. Use them for biomes, resource pockets, fog frontiers, and combo-enabling terrain — not “only this sector’s buildings count.”
4. **Cards + placement remain the primary verb.** The hand is the decision surface; combos and climate pops are the dopamine.
5. **Avoid the boring micromanage loop.** Prefer decisive card plays and board reading over constant worker babysitting.

### Target loop (vertical slice)

1. Open hand of cards (buildings / support).
2. Place onto pads / terrain → power + climate + **adjacency combos**.
3. Whole-board Temp / Atmos / Water climb toward the next **milestone tier**.
4. Hit all three climate lines (and any primary) → award progress / shop / next harder targets.
5. Optional expansion unlocks better land for combos — **not** required to “restart” climate in a fenced sector.

### Explicitly retiring (legacy design)

* **Per-sector exclusive climate mini-game** (only ActiveSector buildings contribute; each sector needs a fresh +15°C / +0.25 atm / +5% water delta while prior absolute gains do not count).
* **Spoke-and-hub “one exclusive sector at a time” as the main fantasy** — colonization may remain, but it is expansion for better boards, not the only climate progress path.
* Treating Biomass as a sector-completion terraforming goal (already deprecated; Biomass may remain as economy/food).

> **Implementation note:** Much of the codebase still implements the legacy exclusive-sector climate loop. New work should migrate toward §0. When code and this doc conflict on *intent*, follow §0; when documenting *what currently runs*, see §1.

---

## 1. Current Implementation vs Target

| Area | Current code (legacy / transitional) | Target (§0) |
|---|---|---|
| Climate contribution | Focus sector via `TerraformingSector` / `GetClimateFocusSector()`; historically ActiveSector-only | All completed climate buildings always count |
| Round win | Per-sector generation: primary + Temp/Atmos/Water deltas from baselines | Planet milestones / tiers; whole board |
| Sector unlock | **Lock retired** — all sectors open; pads planet-wide on card pick | Optional expansion for land / combos |
| Caps | Floored to round / next-gen cumulative targets | Still need headroom past milestone lines so HUD keeps moving |
| Cards / hand | FIFO 5-card hand, reserved pads, materials costs | Keep; add adjacency combo feedback |
| Adjacency combos | Not yet a first-class system | Core dopamine layer to build |

**Migration priority:** (1) ~~remove exclusive climate gating~~ / remove sector build lock — **sector lock retired**; (2) whole-board climate ticks; (3) redefine milestones as planet tiers; (4) add adjacency combo multipliers + UI pops.

---

## 2. Core Systems Still in Play

### 2.1 Card hand & play-and-draw
* `CardDeckController` + `BottomBarActionsUI`: **5-card** hand, larger faces (~158×220), docked **above** the Bottom Bar so selection info is not covered.
* Title + materials cost **inside** the card (cost chip top-left). Affordable = gold, unaffordable = red.
* Playing a card draws the next **playable** card from a **FIFO** pile — see **§5**.
* Draft overlay rounds are disabled (`TriggerDraft` / `ShowDraftSelection` are no-ops).
* Cards load from `Resources/Cards` via `BlueprintDraftUI.InitializeDefaultPool()`.

### 2.2 Reserved site pads
Player building cards primarily play onto **pre-placed pads** (`ReservedSiteBuildUtility`), not free terrain placement.

* **Kinds:** `CommandPost`, `Solar`, `PairedBuilding`, `Mine`. Deprecated: `Infrastructure`.
* **Cluster rule:** one solar pad + one consumer pad; consumer requires that cluster’s solar occupied, then auto-wires to it. **Solar never auto-wires to the Command Post.**
* **Builds need an idle drone** (HUD: “A drone is needed.”). Exceptions: waived-cost auto-colonize CP, first Command Post orbital drop.
* **Pad browse (Q/E):** While a building card is selecting a site, **Q** / **E** cycle the camera across planet-wide eligible pads (sorted west→east). Click places on the focused/clicked pad; Esc cancels. Outside site-picking, Q/E still page Command Posts.
* Site-marker preview ghosts must never occupy pads or raise `BuildingSpawnEvent`. See historical pad/ghost rules under **§11** if debugging visuals.

### 2.3 Climate buildings & rates
Themed buildings tick Temp / Atmos / Water from `BuildingConfigSO` (and card propagation fallbacks). Examples: GHG Factory (temp + atmos), Atmospheric Condenser / CO₂ Import Laser (atmos), Geothermal / Methanogenic Spreader (temp), Water Ice Aquifer / Subglacial Extractor (water).

* Climate basics for goals: **Temperature / Atmosphere / Water** only (Biomass not a terraform goal).
* Float near-complete: treat progress ≥ ~0.999 as done (`RoundDeltaProgress` pattern) so targets like 0.26 don’t softlock.
* Caps must leave **headroom past the current milestone line** so values don’t freeze on the win threshold.

### 2.4 Power grid
* Undirected graph: `PowerGridManager` + `PowerNode`. Power is **net capacity**, not a stockpile loop.
* Command Posts stay operational even when unpowered (life-support recovery); temporary CP backup cells exist for bootstrap.
* Cluster consumers allocate shared solar before CP drain.

### 2.5 Economy & failure
* Materials costs: always use `GetMaterialsCost()` / `GetMaterialsPlayCost()` — null `Cost` must not mean free.
* Scouting costs: Orbital Scan 50, Survey Drone 75, Pipeline Boost 50; Emergency Caches free.
* `GameOverManager`: suppress false losses on quit/unload; don’t fail while pipelines are still expanding; integrity drain gated by **§7**.

### 2.6 Semi-turn flow
* No End Turn button. Idle ~2s after player action → turn resolves (`GameFlowManager` / phase controller).
* Phases (conceptually): Upkeep → Recovery → Income → Threats → Draw (`FillHand`) → Events (stub) → Milestones → Win/Lose.
* **Key intent:** batch decisive card plays, then watch the board resolve — Combolands pacing, not full RTS micro.

### 2.7 Fog, hexes, camera
* Hex shroud / reveal for exploration; colonization / vision clear fog.
* WASD hex camera may focus any hex; movement does **not** auto-`Reveal()`.
* Starting area reveal (~15) bootstraps Sector 0 pads + minerals.

---

## 3. Sectors — Map Regions (Lock Retired)

**Sector lockdown is retired.** At planet init every sector starts `IsLocked = false` and `IsExplored = true`. Building cards offer **eligible pads across the entire planet** (`GetEligibleSites(..., visibleToPlayerOnly: false)`). Movement and free-placement no longer reject “locked” sectors.

**Still true:**
* Sectors remain map regions (centers, features, pad lists, borders).
* Hex fog still hides idle world content; card pad-picking ignores fog so you can jump to distant pads.
* Resource node discovery stays bootstrap-limited to Sector 0 (`DiscoverStartingSectorResources`) — exploring hexes reveals deposits elsewhere.
* Optional colonization (Command Post claim) can still expand occupied footprint; it is not required to unlock build rights.

**Do not reintroduce** `IsLocked` gates on pad eligibility, card `CanApply`, or unit movement.

---

## 4. Goal Colors & Objectives UI

Color coding ties **terraforming goal** cards to Active Objectives (`TerraformingGoalColors`).

* Colored: TEMPERATURE (amber), ATMOSPHERE (fuchsia), WATER (blue), plus primary types when used (Oxygen cyan, Power gold, Population indigo, Command Post white).
* Neutral: materials, exploration, mining, emergency caches, buffs, etc.
* Objectives show remaining need / DONE; all three climate lines must be green to clear a climate milestone set.
* Mapping: `UnlockBuildingCardSO.ClassifyBuildingGoal` (e.g. GHG → TEMPERATURE).

Rename mentally from “sector-completion goals” → **“milestone / terraforming goals”** as UI copy migrates.

---

## 5. Card Deck FIFO Draw (Authoritative)

`CardDeckController` rules (keep unless redesigning the deck engine):

* Stable order — **no shuffle** on rebuild.
* Milestone/climate tools duplicated once in the draw pile (~2× frequency) via runtime clones.
* Draw from front; played/skipped → back of discard; recycle preserves order.
* Hand only seats cards that pass `IsGateMet()` + `CanApply()`.
* Opening hand seeds Command Post + Solar + Mining Drone; `EnsureSolarPrereqInHand` / `EnsureMiningDroneInHand` prevent softlocks.
* Extra Solar infra copies in the pile when solar pads exist.

---

## 6. Blueprint / Themed Card Pool (Summary)

Default unlocks / support: Solar, Oxygen Processor, Habitat, materials/biomass shipments, Mining Drone, gather/power buffs.

**Themed climate / utility** (procedural `BuildingSO` + cards; costs Materials; many climate-gated): strip-mine, deep-core laser, aquifers/extractors, GHG / geothermal / condensers / CO₂ laser, microbe/algae spreaders, biosphere, etc. Full historical list lived in older §22 notes — assets/runtime pool in `BlueprintDraftUI` remain source of truth for exact names/stats.

**Scouting:** Orbital Scan, Pipeline Boost, Survey Drone, Emergency Caches (+300 Mat).

---

## 7. Colony Integrity Start Gate (Authoritative)

* `Supplies.ColonyIntegrityActive` starts false each scene.
* Until true: integrity reads 100%; `GlobalDecayManager` skips drain.
* Becomes true on first real gameplay building `(Clone)` completing construction (not UCC / DecayStarter / hub).
* UCC / GlobalCommander is invulnerable and excluded from integrity math.

---

## 8. Prefabs & Ghosts (Critical Rules)

* Prefer building prefabs as variants of `BaseBuilding.prefab`.
* **Never** overwrite a command’s assigned `GhostPrefab` with a shared “first available” template or solid prefab fallback that makes all ghosts look like Solar.
* Missing references should fail loudly in editor, not silently degrade.
* Site ghosts: inactive holder → disable simulation → translucent URP materials after procedural meshes exist.

---

## 9. UI Philosophy

* Universal Bottom Bar mirrors selection ActionsUI; card hand **is** the action surface.
* `CommandSelectedEvent` → `PlayerInput` executes; keep event-driven refresh (`UpgradeResearchedEvent`) in sync.
* Tech tree / generation summary: deactivate full-screen raycast blockers when closed.

---

## 10. Automated Economy / AI (Secondary)

* `GreedyAIController`: logarithmic spend, force first Probe, assign mining drones to valid `GatherableSupply` under planet gen, respect sector locks while those still exist.
* Foundry crawler / energy pipeline: expansion logistics (may be demoted as sector fantasy changes).
* Mining drones: proximity repair (~14m) while gathering.

Treat AI as secondary until the player combo loop feels good.

---

## 11. Condensed Fix Memory (Do Not Reintroduce)

Agents should not re-litigate these; details are in git history if needed.

* Climate silent / stuck: focus sector stolen mid-round; caps == win line; float `(0.26-0.01)/0.25 < 1`; CO₂ laser classified as mine; null BuildingSO / Progress=Destroyed ghosts; milestones only on idle turns.
* Softlocks: solar missing from hand; 3 clusters/sector pad starvation; colonization without CP; integrity DecayStarter self-damage; CP unpowered blocking solar placement.
* Free builds: null Cost treated as free — always charge via `GetMaterialsCost()`.
* Site ghosts looking finished / stealing pads / sheltering colonists.
* UCC collider stealing drone selection.
* Scene reload singleton / static persistence bugs — overwrite `Instance = this` on load; reset statics on `sceneLoaded`.
* Instant next-gen completion: grace period after destroy; record baselines **after** draft effects.
* Power as stockpile from upkeep coroutine — use static net capacity.
* Unity MCP deprecated — **CLI only** (§12).

Live climate diagnose helpers (legacy sector bot era): `ClimateGenerationTicker.ReportStatus()`, `ClimateGenerationAutomation.DiagnoseAtmosphere()` / `StartAtmosphereWatch()`, `./tools/sector-win-cli.sh` (will need retargeting when milestones go planet-wide).

---

## 12. Unity CLI & Live Editor Automation (Authoritative)

Experimental **Unity CLI** + **Unity Pipeline** against the **already-open** Editor. Full procedure: [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md).

**Hard rules:**
* **Unity MCP deprecated.** Do not use Cursor `user-Unity` / `mcp_auth` for Unity / `unity mcp`.
* Prefer connected Editor: `unity status --format json`.
* **Never** spawn a second Editor while the user’s is open (`unity test` / `build` / `run` / `-batchmode` → OOM on this heavy map).
* Use: `unity command` / `list` / `eval`.

```bash
unity status --format json
unity command eval "return UnityEditor.EditorApplication.isPlaying;" --json
```

**CI:** `.github/workflows/unity-editmode.yml` — EditMode via GameCI; live bot only on self-hosted `unity-pipeline-live` with Editor open.

Scene note: GameObject often named `PlanetManager`; script is `PlanetGenerator`.

---

## 13. What To Build Next (Design Backlog)

1. **Whole-board climate** — delete exclusive sector contribution; keep optional sector fog/colonization.
2. **Planet milestone table** — Tier 1/2/3 Temp/Atmos/Water targets + clock/action budget.
3. **Adjacency combo rules** — 2–3 multipliers among existing pink/orange/blue buildings + floating score/climate pop UX.
4. **Objectives / bot retarget** — Active Objectives and CLI automation speak “planet milestones,” not “finish this sector’s exclusive climate.”
5. **Guild-like run identity** (later) — pick climate specialties that shape the offer pool.

---

## 14. Related Systems (Keep, Don’t Expand Blindly)

* Curved world shader / updater (visual only).
* Martian colonists / pressurized tubes / habitats.
* Tech tree 20-level unit decks & infantry upgrades (roguelite shop layer).
* AudioManager persistent BGM.
* Vegetation biomass generation (economy, not terraform milestone).

---

*Last rewritten: 2026-09-06 — Combolands-style whole-board terraforming milestones as design authority; sector lockdown retired (pads planet-wide).*
