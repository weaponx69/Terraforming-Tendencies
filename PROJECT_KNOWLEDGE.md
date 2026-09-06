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

> If code and this doc conflict on *intent*, follow **§0**. For *what currently runs*, see **§1**.

---

## 1. MVP Status Board (Where To Go From Here)

| Piece | Status | Next action |
|---|---|---|
| Card hand + place on pads | **Done** | Polish only if broken |
| Planet-wide pads + Q/E browse | **Done** | Keep; don’t reintroduce sector lock |
| Sector build lock | **Retired** | Do not restore |
| Climate buildings exist (Heat/Air/Water) | **Done** | Ensure deck density + no softlock gates |
| Whole-board climate ticks | **Not done** | Remove `TerraformingSector` / focus-sector exclusivity so every powered climate building counts |
| Win = Temp+Atmos+Water only | **Partial** | Code still requires **primary + three climates**; drop primary from MVP win gate |
| Baselines / deltas | **Legacy sector-round** | Keep +15 / +0.25 / +5 for now; treat as **planet tier** not “new sector mini-game” |
| Objectives copy | **Partial** | Say “terraforming milestones,” not “finish this sector” |
| Adjacency combos | **Post-MVP** | Only after 10–15 min place→win feels good |
| Live bot / CLI win check | **Legacy** | Retarget to planet Temp/Atmos/Water, not unlock-next-sector |

**Ordered build list (do in order):**

1. **Whole-board climate** — all completed powered climate buildings always contribute.  
2. **MVP win gate** — `IsCurrentSectorRoundComplete` / progress = min(Temp, Atmos, Water) only (no primary).  
3. **Deck reliability** — Solar + GHG + Condenser + Aquifer (and backups) always cycle; remove Softlocks from climate gates for basics.  
4. **Objectives / HUD** — three lines, remaining-to-green, milestone language.  
5. **Playtest** — can a player finish without cheats in ~15 min?  
6. **Only then:** combos, tiers 2/3, guilds, colonization-as-flavor.

---

## 2. Current Code vs MVP (Honest)

| Area | Current code | MVP target |
|---|---|---|
| Climate contribution | Still focus-sector oriented (`GetClimateFocusSector` / historical ActiveSector) | All powered completed climate buildings |
| Round win | `min(primary, temp, atmos, water)` ≥ 0.999 → generation end | `min(temp, atmos, water)` only |
| Deltas | +15°C / +0.25 atm / +5% from round baselines | Same numbers OK as Tier 1 |
| Sector lock | All sectors open; pads planet-wide | Keep |
| Cards / pads / Q/E | Working | Keep |
| Combos | None | Post-MVP |

**Legacy milestone check (today):** `GenerationManager.Update` every frame (after 2s grace) + idle `OnTurnMilestones` → `CheckMilestones` → `TriggerGenerationEnd` when progress ≥ 0.999.

---

## 3. Systems To Keep (Support the MVP)

### 3.1 Card hand
* `CardDeckController` + `BottomBarActionsUI`: **5** cards, faces ~158×220, above Bottom Bar.
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

## 9. Post-MVP Backlog (After Hamster Loop Works)

1. Adjacency combo multipliers + pop UX  
2. Weather / terrain tiles (cloud, ice, vent) as amplifiers  
3. Harder milestone tiers / action clock  
4. Guild-like run identity  
5. Colonization as optional better land, not a softlock  
6. Retarget `./tools/sector-win-cli.sh` → planet milestone bot  

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

## 11. Related Systems (Ignore for MVP)

Curved world, colonists/tubes, deep unit tech trees, combat, AI expansion, vegetation biomass-as-economy — leave alone unless they block the hamster loop.

---

*Last rewritten: 2026-09-06 — Absolute minimal “hamster planet” MVP is design authority; ordered next steps in §1.*
