### Terraforming Tendencies — Project Knowledge & Architecture Notes

**📚 Central Hub Documentation**
* **Game Design Document (lore / mechanics intent):** [GDD.md](GDD.md)
* **Visual Scripting & C# Refactoring:** [.zoo/rules/UnityVisualScripting-conversion.md](.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** **§10** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** [`AGENTS.md`](AGENTS.md) (mirrored in `.clinerules` / `.zoomodes`)

If this file and `plans/project_knowledge.md` disagree, follow **this file**.

---

## 0. Authoritative Run Model — Combolands-Style Colony Acts

**One-sentence game:** Draw building tiles → place them on pads anywhere on the planet → each play spends a **week** and finished tiles raise **Colony Score** → clear rising Act quotas before weeks run out → win the run.

**Inspiration:** [Combolands](https://store.steampowered.com/app/4075620/Combolands/) — timed score milestones via placement, not RTS climate sim gates.

### What is NOT the win meter
* Temp / Atmos / Water **supply tickers** (old hamster MVP)
* Sector unlock / colonization ladder
* Oxygen Processor / primary Power / Pop gates

### Acts (milestones)

| Act | Name | Target Score | Weeks |
|-----|------|--------------|-------|
| 1 | Survive | 40 | 8 |
| 2 | Settle | 120 | 8 |
| 3 | Habitable | 280 | 10 |
| 4 | Thrive | 500 | 10 → **victory** |

* Act count is **fixed** for the run mode — **independent of sector count** (sectors are variable map geography only).
* **1 card play = 1 week.** Exhausting weeks without the score target = **Act fail / run loss**.
* ~25% of score (+ excess) **carries** into the next Act.
* Owner script: [`ColonyActManager`](Assets/Scripts/Player/ColonyActManager.cs).

### What it takes to meet an Act
1. 5-card hand → play a tile (materials may still apply).
2. Week decrements on commit (`PlayCard` / `ConsumeCardAfterBuild`).
3. When the building **completes**, grant **Base Score** (+ Habitability for Heat/Air/Water tags).
4. Hit Score ≥ Target before weeks hit 0.

### Tile score table (v1 — no adjacency yet)

| Tag | Examples | Base Score | Habitability |
|-----|----------|------------|--------------|
| Anchor | Command Post, housing | 12 / 10 | — |
| Power | Solar | 4 | — |
| Labor | Mining Drone (card) | 3 | — |
| Industry | Mines | 8 | — |
| Heat / Air / Water | GHG, Condenser, Aquifer | 10 | +8 each |
| Life | Oxygen Processor | 6 | +3 |
| Other | default | 5 | — |

### Habitability & look
* Cumulative Habitability from climate-tagged tiles drives [`ClimateVisualStages`](Assets/Scripts/Environment/ClimateVisualStages.cs) (Barren→Living) and flora spawn density.
* Not driven by Supplies climate deltas for win/look.

### Board / sectors
* **Whole planet** pads + Q/E. Sector **lock stays retired**.
* Sectors = fog / borders / pad lists only. Do **not** unlock on Act clear.

---

## 1. Status Board

| Piece | Status | Next action |
|---|---|---|
| Card hand + place on pads | **Done** | Show +Score on cards (badge/tooltip) |
| Colony Acts + week clock | **Done** | Tune targets / weeks in playtest |
| Score on building complete | **Done** | Adjacency multipliers later |
| Objectives HUD (Act/Score/Weeks) | **Done** | Polish |
| Habitability → ground tint / flora | **Done** | Fog/sky lerp still open |
| Adjacency combos | **Later** | After Acts feel good |
| Sector unlock progression | **Retired** | Do not restore for win |

**Ordered build list:**
1. ~~Colony Acts runtime + docs~~ **Done**
2. Fog / ambient sky from Habitability
3. Simple adjacency score bonuses
4. Placement popcorn (+Score float text)

---

## 2. Current Code vs Intent

| Area | Current code | Intent |
|---|---|---|
| Win | `ColonyActManager` final Act → `NotifyColonyActVictory` | Same |
| Climate contribution | Whole board still ticks Supplies | Flavor only; not Act gate |
| Generations | `MaxGenerations = 1` legacy shell | Acts replace multi-gen |
| Sector lock | All open | Keep |
| Cards | Unlock buildings + BaseScore table | Add adjacency later |

---

## 3. Systems To Keep

### 3.1 Card hand
* 5 cards, lower-left; Building Selected far right.
* Week spent on successful play/consume.
* Climate force-seat helpers may remain for tile variety — retune if they starve Anchors.

### 3.2 Reserved pads / drones / power
* Unchanged support for placing tiles.

### 3.3 Sectors (geography only)
* Variable count from map gen.
* No `IsLocked` gate on pads; no UnlockNextSector on Act clear.

### 3.4 Colony integrity / UCC
* Keep §6 integrity gate behavior.

---

## 4–8. (Unchanged support notes)

Goal colors still tint Heat/Air/Water tiles. FIFO deck rules still apply. Prefab/ghost rules unchanged. Condensed fix memory: do not reintroduce climate-trio victory or sector lockdown for progression.

---

## 9. Backlog

1. Fog / sky from Habitability  
2. Adjacency score  
3. Placement +Score VFX  
4. Soft-fail / extra weeks (Combolands-like)  
5. Retarget CLI bots to Colony Score Acts  

---

## 10. Unity CLI

See [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md). Unity MCP deprecated.

---

## 11. Related Systems

Colonists/tubes, guilds, combat — leave until Acts feel like Combolands.

*Last rewritten: 2026-09-06 — Combolands Colony Acts replace climate MVP win.*
