# Design: Research cards, connected power, and energy spends

**Date:** 2026-11-22  
**Status:** Approved — awaiting implementation plan  
**Engine:** Unity Prologue  
**Builds on:** Prologue cards / End Week in [`PROJECT_KNOWLEDGE.md`](../../PROJECT_KNOWLEDGE.md); Acts power stack (`PowerNode`, `PressurizedTube`, `ConnectPowerCommand`, `AutoConnectAdjacentPowerNodes`); RTS `TechTreeSO` / `TechTreeUI`.

## 1. Player fantasy

You aim the colony with cards. Power is real from day one: pressurized tubes link buildings, camp power runs the grid until generators take over, and spends like Orbital Scan come off that energy. Research is a time bet — play a research card, wait weeks, and unlock better cards and caps (more links, better scans, faster builds). You supply materials and skills; colonists maintain the tubes.

## 2. Decisions (locked)

1. **Connected power is Tier‑1** — reuse the existing tube / `PowerNode` stack from Prologue start. No research gate on basic connectivity.
2. **Camp power is power** — it fills the same energy pool until the colony generates its own (e.g. Geothermal on the tube grid).
3. **Orbital Scan costs energy**, not minerals. No research paywall. Unchanged seating rule (available while unrevealed ice remains); refuse or disable when energy is too low.
4. **Research costs weeks only** — no mineral / faith / tech-point currency for research.
5. **Research cards unlock seating** — playing research completes a next-tier target; that unlocks which advanced cards / abilities can seat. Combo of pause tech-tree map + card delivery.
6. **Tier‑1 of every branch starts unlocked** in Prologue (basic Survey, builds, Orbital, connected tubes).

## 3. Power and tubes

### 3.1 Existing stack (reuse, do not rewrite)

| Piece | Role |
| --- | --- |
| `PowerNode.ConnectTo` | Links nodes; draws pressurized tube (`LineRenderer` + `PressurizedTube`) |
| `BaseBuilding.AutoConnectAdjacentPowerNodes` | Auto-link neighbors when construction finishes |
| `ConnectPowerCommand` | Click-connect path (available if needed; prefer auto-adjacent for Prologue) |
| `PressurizedTube` | Damageable link; engineers / colonists repair and transit |
| `EnergyPipelineManager` / worker `BuildPipeline` | Worker-built pipeline path when a link needs a trip |

### 3.2 Prologue behavior

- When a building that needs or supplies power finishes next to another powered node, tubes auto-link within the Tier‑1 connection rules.
- Colonists build / maintain tubes. Player supplies materials when a link costs minerals (existing pipeline material rules where they already apply).
- Replace abstract “camp blanket until storm, then Geothermal must touch Oxygen” with **grid power**: Oxygen (and later spends) are powered when connected to a live source. Camp is the starter source on that same pool.
- HUD shows energy / power status from the real pool (extend current `power camp` / `power geothermal` / `power NONE` toward pool + source).

### 3.3 Later research (not Tier‑1)

- Higher connection count / reach  
- Solid tubes (`BlueprintDraftManager.TubesAreSolid`)  
- Broader energy metering for more spends  

Manual wire drawing stays out of scope.

## 4. Energy and Orbital Scan

- One colony **energy** pool fed by camp power, then by generators on the tube grid.
- **Orbital Scan:** deduct energy on play; ping nearest unrevealed ice (current ping UX). No mineral cost. No tech unlock required.
- If energy is below cost: card does not resolve (clear feedback). Card may still seat when ice remains unrevealed.

## 5. Research loop

1. A **Research** card seats randomly in the hand (same seating family as other Prologue cards).
2. Player plays it → picks a available **next-tier** target (pause tech tree and/or short pick list).
3. Research runs over **End Weeks** until the week cost is paid.
4. On complete: unlock that tier’s **cards / abilities** so they can seat (delivery is seating, not only invisible buffs).
5. Pause tech tree remains the map of “what’s next”; Tier‑1 nodes start lit.

Research does **not** gate Orbital Scan or basic connected power.

## 6. Unlock ladder (first pass)

| Tier | Examples |
| --- | --- |
| **1 (start)** | Connected power tubes, camp→grid energy, Survey, Orbital Scan (energy cost), basic builds |
| **2** | More connection reach / building count; faster build; additional scan types |
| **3+** | Scan-for-specific (water / minerals); solid tubes; further energy spends |

Exact week costs and card list are balance follow-ups in the implementation plan.

## 7. Unchanged

- Survey Team seating and paint rules  
- End Week O2 / beds / ice pressure  
- Scaffolding on every build  
- Mine placement on deposit ghosts  
- Orbital ping UX (direction + pulsing `?`); only the spend currency changes (energy)

## 8. Non-goals (this pass)

- Gating Orbital or basic tubes behind research  
- Manual wire drawing UI as the primary Prologue path  
- Full Acts power-placement gates in Prologue  
- Solid-tube upgrade as a required ship item (research later)  
- Rewriting `PowerNode` / `PressurizedTube` from scratch  

## 9. Success criteria

- Prologue buildings link with visible pressurized tubes using the existing stack.  
- Camp power supplies energy until colony generators are online.  
- Orbital Scan spends energy and never minerals; never requires research.  
- Research cards spend weeks and unlock higher-tier seating only.  
- Pause tree shows Tier‑1 complete and Tier‑2+ as research targets.  
