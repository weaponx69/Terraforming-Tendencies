### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md)
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Absolute Minimal MVP — “Hamster Planet” (Authoritative)

**One-sentence game:** Draw building cards → place them on pads anywhere on the planet → watch Temp / Atmos / Water climb → when all three are green, you win the run tier.

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) tile-placement dopamine, without requiring combos, guilds, or meta progression for the first shippable slice.

### MVP win loop (the only loop that matters)

1. Open a **5-card hand**.
2. Play a **building card** → browse planet-wide pads with **Q/E** → **click** to place.
3. Powered climate buildings tick **Temperature / Atmosphere / Water** on the **whole board**.
4. When **all three climate lines are met**, the milestone clears (summary / win / next tier).
5. Playtest target: **~8–15 placements, 10–15 minutes**, feel decisive — not RTS micro.

### MVP milestone triggers

**Win condition = Temp + Atmos + Water all met.** No separate primary (Power / Pop / Oxygen / Command Post) in the MVP path.

| Line | MVP target (use current deltas until retuned) |
|---|---|
| **Temperature** | +**15°C** from run/tier baseline |
| **Atmosphere** | +**0.25 atm** from baseline |
| **Water** | +**5%** from baseline |

**What fires the check:** climate supplies crossing those thresholds (buildings ticking over time). Placing a card alone does not win unless its ticks push the bars over.

**Near-complete floats count as done** (≥ ~0.999 / `RoundDeltaProgress` headroom) so values like 0.26 atm don’t softlock.

### MVP tile kit (must always be drawable)

Enough copies that the player can slowly finish without RNG softlock:

| Role | Buildings (use existing) |
|---|---|
| **Power** | Solar Panel |
| **Heat** | GHG Factory (+ Geothermal or Methanogenic as backup) |
| **Air** | Atmospheric Condenser (+ CO₂ Import Laser as backup) |
| **Water** | Water Ice Aquifer (+ Subglacial Extractor as backup) |
| **Bootstrap** | Command Post, Mining Drone (materials / build labor) |

**MVP deck rule:** Heat / Air / Water / Solar stay common (double density OK). Soft climate gates that block Water before Heat (e.g. Aquifer at −20°C) must not softlock the MVP.

### In MVP (keep / finish)

* 5-card FIFO hand, materials costs, play-and-draw  
* Reserved pads, Solar→consumer clusters, drone-required builds  
* Planet-wide pad pick + **Q/E** cycle + click + Esc  
* Sector **lock retired** (pads/builds anywhere)  
* Whole-board climate contribution (**must finish** — see §1)  
* Objectives UI: three climate lines green = done  
* Caps leave headroom past the win line so HUD keeps moving  

### Out of MVP (do not build / do not expand until MVP feels fun)

* Adjacency combos / weather tiles / cloud→rain  
* Guilds, heirlooms, councillors, meta unlocks  
* Exclusive-sector climate mini-game / colonization as win gate  
* Combat, deep tech tree, colonists/tubes as required systems  
* Draft overlays, scouting-as-progression, AI opponents  
* Extra primaries (Oxygen / Power / Population) on the win path  

> If code and this doc conflict on *intent*, follow **§0** for the hamster win loop and **§1A** for the next visual slice. For *what currently runs*, see **§1** / **§2**.

**Next slice after hamster playtest:** **§1A Visual Climate Stages MVP**.

---

## 1. MVP Status Board (Where To Go From Here)

| Piece | Status | Next action |
|---|---|---|
| Card hand + place on pads | **Done** | Polish only if broken |
| Planet-wide pads + Q/E browse | **Done** | Keep; don’t reintroduce sector lock |
| Sector build lock | **Retired** | Do not restore |
| Climate buildings exist (Heat/Air/Water) | **Done** | MVP kit unlocked at start |
| Whole-board climate ticks | **Done** | Every powered climate building counts |
| Win = Temp+Atmos+Water only | **Done** | One round → victory (no primary gate) |
| Baselines / deltas | **Done (Tier 1)** | +15°C / +0.25 atm / +5% |
| Objectives copy | **Done** | Planet terraform / three lines / win |
| 5-card hand hard cap | **Done** | No GlobalCommander extra slots |
| Hamster playtest (place→win) | **Done enough** | Fantasy gap = barren world doesn’t *look* alive |
| **Visual climate stages** | **In progress** | Ground tint done; flora = Atmosphere. Next: fog/sky |
| Adjacency combos | **Later** | After planet looks terraformed |
| Live bot / CLI win check | **Legacy** | Retarget to MVP victory when needed |

**Ordered build list:**

1. ~~Hamster card/climate win loop~~ **Done**  
2. **Visual climate stages (§1A)** — ground / sky-fog / sparse flora  
3. Colonist life reacting to stages (after visuals)  
4. Placement popcorn / combo juice (after fantasy reads)

---

## 1A. Visual Climate Stages MVP (Authoritative — Next Slice)

**One-sentence goal:** As Temp / Atmos / Water climb toward the hamster win, the **planet visibly shifts** barren → living so the run feels like making another Earth — not just three green meters.

**Does not change:** win gate, card hand, pads, deck. Visuals only.

### Progress driver

Compute one whole-planet **Terraform Look** `0→1` from the same MVP deltas used for win:

* Temp progress = round delta / +15°C  
* Atmos progress = round delta / +0.25 atm  
* Water progress = round delta / +5%  

`lookProgress = min(temp, atmos, water)` (same bottleneck as win). Subscribe to `Supplies.OnTemperatureChanged` / `OnAtmosphereChanged` / `OnWaterChanged` (and refresh on generation start / baselines).

### Stages (exactly 4)

| Stage | `lookProgress` | Player-visible look |
|---|---|---|
| **0 Barren** | &lt; 0.33 | Red-brown ground, cold harsh sky/fog, **no** flora |
| **1 Thaw** | 0.33–0.66 | Ground warms toward dusty soil; sky/fog softer |
| **2 Wet** | 0.66–0.999 | Cooler green-brown tint; **sparse** grass near Water / life-support |
| **3 Living** | ≥ 0.999 (win) | Greener ground; denser grass/plants in range; clearer sky |

Planet-wide stages only — do **not** split per climate channel or per sector in this slice.

### Visual channels (exactly 3)

1. **Ground tint** — lerp planet mesh `_BaseColor` / `_Color` (PlanetGenerator already stamps a fixed martian color; make it dynamic from stage colors).  
2. **Sky / fog** — lerp `RenderSettings.fogColor` + ambient (reuse existing sky material only if already wired and cheap).  
3. **Sparse flora** — `VegetationManager` spawn scales with **Atmosphere** progress (0 = off/trickle, full at Atmos win delta). Prefer existing plant/grass prefabs; no new biomes.

Suggested owner script: `ClimateVisualStages` (or similar) auto-spawned / scene singleton that applies lerps each climate change (smooth over ~1–2s).

### Out of this slice (do not build yet)

* Lakes / ocean meshes  
* Rain / dust / weather particles  
* Per-building adjacency VFX / combo pops  
* Colonist happiness / population fantasy  
* New shaders beyond tint + fog  
* Changing win rules or card kit  

### Success check

One full place→win run: player can **see** barren → thaw → wet → living without reading the HUD.

### Build order for §1A

1. ~~`ClimateVisualStages` + ground tint~~ **Done** (progress/stage enum; terrain gradient + `_BaseColor` lerp)  
2. Fog / ambient sky lerp  
3. ~~Vegetation spawn gated by stage~~ **Done** (`VegetationManager` scales with **Atmosphere** progress toward +0.25 atm — **not** Oxygen)  

**Test:** raise climate (play tiles or cheats) and watch ground shift red-brown → dusty → green-brown → green. Console logs `[ClimateVisualStages] Stage=...`.

### Hooks already in repo

* `Supplies.OnTemperatureChanged` / `OnAtmosphereChanged` / `OnWaterChanged`  
* `GenerationManager` baselines + round deltas  
* `PlanetGenerator` ground `_BaseColor` assignment (~martian brown)  
* `VegetationManager` + `GrowingVegetation` (currently life-support oriented; gate by stage)

---

## 2. Current Code vs MVP (Honest)

| Area | Current code | MVP target |
|---|---|---|
| Climate contribution | **Whole board** (`DoesBuildingCountForActiveClimate` always true) | Same |
| Round win | `min(temp, atmos, water)` ≥ 0.999 → **victory** | Same |
| Deltas | +15°C / +0.25 atm / +5% from baselines | Same |
| Generations | **MaxGenerations = 1** | Same |
| Sector lock | All sectors open; pads planet-wide | Keep |
| Cards / pads / Q/E | Working; MVP climate kit unlocked | Keep |
| Combos | None | Post-MVP |

**Win path:** `GenerationManager.CheckMilestones` → `TriggerMvpVictory` → `GameOverManager.TriggerVictory` (also mirrored in GameOver win check).

---

## 3. Systems To Keep (Support the MVP)

### 3.1 Card hand
* `CardDeckController` + `BottomBarActionsUI`: **5** cards, faces ~158×220, docked **lower-left corner**.
* Selection info (`Building Selected Container`) shifted to the **right** of the bottom band so cards do not cover it.
* While Temp/Atmos/Water unmet, force-seat one tool of each color (`EnsureMvpClimateGoalsInHand`) so FIFO cannot bury Water.
* Cost chip inside card; affordable gold / unaffordable red.
* FIFO play-and-draw (**§5**). Draft overlays disabled.

### 3.2 Reserved pads
* Kinds: `CommandPost`, `Solar`, `PairedBuilding`, `Mine`.
* Cluster: solar then consumer; solar never auto-wires to Command Post.
* Idle drone required (except first CP / waived colonize CP).
* **Q/E** cycles eligible pads west→east while selecting; click places; Esc cancels. Outside picking, Q/E pages Command Posts.
* Card eligibility uses planet-wide sites (`visibleToPlayerOnly: false`).

### 3.3 Climate buildings
* Config rates on `BuildingConfigSO` (GHG, Condenser, CO₂ Laser, Geothermal, Aquifer, Extractor, etc.).
* Only Temp / Atmos / Water are terraform goals (Biomass is not).

### 3.4 Power
* `PowerGridManager` / `PowerNode` net capacity (not stockpile).
* CP stays operable unpowered; cluster solar prefers consumers over CP drain.

### 3.5 Light economy
* Always charge via `GetMaterialsCost()` / `GetMaterialsPlayCost()` (null Cost ≠ free).
* Integrity gated by **§6** — don’t expand decay design for MVP.

### 3.6 Sectors (demoted)
* Lock retired; regions still hold pad lists / features / borders.
* Hex fog remains for world vibe; does **not** gate card pads.
* Do **not** reintroduce `IsLocked` on pads, `CanApply`, or movement.
* Colonization / scouting = optional flavor, **not** MVP progress.

---

## 4. Goal Colors & Objectives

* Temp = amber, Atmos = fuchsia, Water = blue (`TerraformingGoalColors`).
* MVP objectives: those three only; all green = milestone clear.
* Primary colors (Oxygen / Power / Pop / CP) stay in code for later — **not** MVP win gates.

---

## 5. Card Deck FIFO (Authoritative)

* No shuffle on rebuild; recycle preserves order.
* Climate / milestone tools duplicated once (~2× density).
* Hand seats only `IsGateMet()` + `CanApply()` cards.
* Opening: Command Post + Solar + Mining Drone; keep Solar / drone reseated when needed.
* **MVP:** prefer seating Heat/Air/Water tools; don’t let support spam starve the engine.

---

## 6. Colony Integrity Gate

* `ColonyIntegrityActive` false until first real `(Clone)` building completes.
* Until then integrity 100% and decay skipped.
* UCC invulnerable / excluded from integrity math.

---

## 7. Prefabs & Ghosts

* Prefer `BaseBuilding` variants.
* Never overwrite assigned `GhostPrefab` with a shared Solar fallback.
* Site ghosts must not complete construction, steal pads, or shelter colonists.

---

## 8. Condensed Fix Memory (Do Not Reintroduce)

* Climate stuck: focus sector stolen; caps == win line; float atmos progress; CO₂ laser as mine; null BuildingSO; milestones only on idle turns.
* Softlocks: Solar missing from hand; pad starvation; null Cost = free; CP unpowered blocking solar.
* Site ghosts looking finished / occupying pads.
* Scene reload singleton/static bugs.
* Unity MCP deprecated — **CLI only** (**§10**).

---

## 9. Backlog (After Visual Climate Stages)

1. ~~Visual climate stages (§1A)~~ — **in progress / next**  
2. Colonists react to stages (survive → thrive)  
3. Placement popcorn (+Temp / +Atmos float text)  
4. Adjacency combo multipliers  
5. Weather / terrain amplifier tiles  
6. Harder milestone tiers / action clock  
7. Guild-like run identity  
8. Retarget `./tools/sector-win-cli.sh` → planet milestone bot  

---

## 10. Unity CLI & Live Editor Automation (Authoritative)

* **Unity MCP deprecated.** CLI only against the **already-open** Editor.
* Never `unity test` / `build` / `run` / `-batchmode` while this Editor is open (OOM).
* Full procedure: [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)

```bash
unity status --format json
unity command eval "return UnityEditor.EditorApplication.isPlaying;" --json
```

---

## 11. Related Systems (Ignore Until §1A Is Done)

Curved world, colonists/tubes as required systems, deep unit tech trees, combat, AI expansion — leave alone unless they block visuals or the hamster loop. `VegetationManager` may be lightly gated by climate stage; do not rebuild flora economy for this slice.

---

*Last rewritten: 2026-09-06 — Visual climate stages (§1A) is the next authoritative slice after the playable hamster win loop.*
