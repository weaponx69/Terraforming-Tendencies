# Design: Unity Unreal-clone — MainGame first (replace Colony Acts)

**Date:** 2026-11-18  
**Status:** Draft for owner review  
**Engine:** Unity (this repo)  
**Source of truth:** Unreal `TerraformTendencies` (`~/Documents/Unreal Projects/TerraformTendencies`), especially `TT/TTStory.cpp` MainGame, `TT/TTColony.*`, `Docs/design-layers.md`  
**Secondary reference:** Godot `tt-godot-reference` (Unreal already matched it; do not invent new rules)

## 1. Problem

Unity’s current default run model is **Colony Acts** (Combolands-style Materials quotas, adjacency combo scoring, Terra-Coins). The Unreal build is a **Frostpunk 2–style layered colony sim** with cards, End Week, O2/beds/power, guided chapters, and (later) a Base/mission board.

For the Unity Fellowship (playable demo by ~Dec 18, 2026), Unity must **feel and play like Unreal**, not like Colony Acts.

## 2. Decision (approved)

1. **Replace** Colony Acts as the default new-game mode. Colony Acts code may remain in the repo but is **archived / not the run model**.
2. **Reverse-engineer Unreal → Unity**, MainGame first, flat hex (no planetoid).
3. Later layers (Industry, claims, Base/missions, …) follow the Unreal/Godot roadmap after MainGame ships.

## 3. Player fantasy (MainGame)

Land on a dead world. One objective at a time: find ice, drill it, make oxygen, house the shuttle crew, survive the first dust storm. Cards are how you place buildings; **End Week** is how time and pressure move.

## 4. Scope

### In — Fellowship vertical slice (MainGame)

| Piece | Unreal reference | Unity requirement |
| --- | --- | --- |
| Guided steps 0–5 | `FTTColony::MainGameObjective` | Same step order, unlocks, and objective copy (tune wording only if needed for UI width) |
| End Week | `EndWeek` / week report | Explicit End Week control; colony only advances on week spend |
| O2 economy | Oxygen stockpile, drills, processors, runway | AIR meter: stored, made/used per week, weeks left |
| Beds | Habitat Domes, shuttle | Beds vs colonists; shuttle deadline |
| Power | Touching Solar / Wind grids | Processor must be powered; Solar dark in dust storm; Wind keeps grid alive |
| Drone scan | Paint path, reveal tiles / ice | Find ice (and later “new ice”) by scanning from lit ground toward `?` |
| Placement rules | Within 3 of ice; Solar beside Oxygen; step-locked unlocks | Functional nearness only — **no adjacency combo scoring** |
| Construction | Sites / multi-week builds | Multi-week build with progress (full Steel/Concrete/builder fidelity can be simplified if needed, but weeks-to-complete and stall feedback must exist) |
| Dust storm | Solar dark; Wind required; ride out with O2 | Same pressure beat to close MainGame |
| HUD | Objective bar, Coming up, AIR, card bar, ghost + reason | Layout and information density match Unreal feel |

### MainGame step unlocks (from Unreal `StepUnlocks` MainGame row)

| Entering step | Unlocks into hand / build bar |
| --- | ---: |
| 0 | (drone only) |
| 1 | `aquifer` (Ice Aquifer) |
| 2 | `oxygen`, `solar` |
| 3 | `habitat` |
| 4 | (more ice / second aquifer — same card) |
| 5 | `wind_turbine` |

### MainGame objectives (canonical)

0. Find ice: drone path from lit ground to a `?`  
1. Build Ice Aquifer within 3 tiles of the ice  
2. Build Oxygen Processor and power it (Solar touching it)  
3. Build 2 Habitat Domes before shuttle colonists need beds  
4. Find / drill more ice when the first deposit runs dry  
5. Build Wind Turbine on the Oxygen grid; store O2; ride out dust storm (Solar dark in storm)

### Out of MainGame / fellowship slice

- Steel / Concrete industry, Mine → Smelter, Corp Steel contract  
- Base hub / mission board / crew select  
- Department heads, orders, stress, hiring, skills  
- Research trees, full disaster set beyond MainGame dust storm  
- Climate claim missions, fuel chains, roads, trucks  
- Terra-Coins shop, Colony Acts Materials quotas, combo card offers  
- Spherical planetoid / geodesic pathfinding  

## 5. Architecture (Unity)

### Run model ownership

| Role | Unity direction |
| --- | --- |
| Colony sim (week tick, goods, O2, beds, power, prologue state) | New `MainGameColony` / `ColonySim` style owner (name TBD) — **not** `ColonyActManager` |
| Board / hex / features | Reuse / adapt `PlanetGenerator`, `HexGridManager`, `ColonyTileGrid` |
| Cards / hand | Adapt `CardDeckController` + card SOs; gate by MainGame unlocks |
| Placement / ghost | Adapt `PlayerInput` placement; reasons from Unreal preview order |
| HUD | New MainGame HUD (objective, Coming up, AIR, End Week); retire Acts-specific panels from default scene |
| Docs | Rewrite `PROJECT_KNOWLEDGE.md` §0 to Unreal/MainGame rules; mark Colony Acts **archived** |

### Colony Acts

- Do **not** delete immediately if risky for compile; disable entry points (boot, scene defaults, UI).  
- Dev/cheat paths that assume Acts quotas must not be the default play path.  
- Long-term: remove or quarantine under `Archived/` once MainGame is stable.

### Rules fidelity

- Prefer porting numbers and checks from Unreal (`TTData`, `TTColony`, `TTStory`) over inventing Unity-only balance.  
- Where Unity must simplify (e.g. builders/stockpile), document the delta in the implementation plan and keep MainGame **feel** (O2 runway, beds, storm) intact.

## 6. UX / look

- **Top:** chapter name + one objective; optional “do it” affordance when a build is the next step.  
- **Coming up:** shuttle week and/or dust storm countdown.  
- **AIR:** stored · Δ/week · weeks left (click → short fix hint: ice → drill → powered processor).  
- **Bottom:** card bar — only unlocked MainGame buildings.  
- **Ghost:** blue valid / red invalid + reason under cursor (Unreal copy).  
- **End Week:** primary time control + short week report (completed builds, O2 change, events).

Visual fidelity: stand-in meshes OK for fellowship if silhouette/tag color and HUD rhythm match Unreal.

## 7. Success criteria

### Playable demo

- Cold start → complete MainGame (survive dust storm) in one sitting without cheats.  
- Player never sees Materials Acts quota / adjacency combo UI.  
- Trailer can show: drone find ice → place drill → oxygen+power → habitats → storm.

### Fellowship framing

- Game is clearly **Unity**, ~1 year from launch narrative fits “MainGame done, Acts/missions next.”  
- Pitch: Frostpunk 2 pacing + cards + terraforming.

## 8. Non-goals (explicit)

- Porting all 11 Unreal layers before Dec 18.  
- Planetoid tiles / spherical NavMesh.  
- Pixel-identical Slate widgets.  
- Keeping Colony Acts as a supported parallel product mode.

## 9. Follow-on (after MainGame)

Order matches Unreal `Docs/roadmap.md` / `Docs/design-layers.md`:

1. Industry (Mine → Smelter → Concrete → first Corp contract)  
2. Home claim / climate burners  
3. Fuel / roads / further missions  
4. Base + mission board  
5. Heads, research, full disasters, shop  

Each layer gets its own design + plan; do not expand MainGame scope mid-demo crunch.

## 10. Open implementation choices (plan may pick)

These do not change the player-facing MainGame design:

1. **Construction fidelity:** full Steel/Concrete + builders vs simplified “N weeks then complete” with fake costs. Prefer Unreal fidelity if schedule allows; otherwise simplified with honest doc note.  
2. **Card data:** new MainGame `BlueprintCardSO` / building IDs aligned to Unreal names (`aquifer`, `oxygen`, `solar`, `habitat`, `wind_turbine`) vs remap existing Unity assets.  
3. **Scene strategy:** new MainGame bootstrap scene vs convert main scene and strip Acts UI.

---

## Spec self-review

- [x] No TBD placeholders for player-facing MainGame steps  
- [x] Colony Acts vs MainGame conflict resolved (replace / archive)  
- [x] Scope capped for fellowship; full clone deferred to follow-on  
- [x] Source of truth named (Unreal; Godot secondary)  
- [x] Planetoid explicitly out  
