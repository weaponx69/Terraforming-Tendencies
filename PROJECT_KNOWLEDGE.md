
### Terraforming Tendencies - Project Knowledge & Architecture Notes


**📚 Central Hub Documentation**
* **Game Design Document (Lore, Mechanics, Stats):** Read [GDD.md](file:///home/brian/UnityProjects/Terraforming%20Tendencies/GDD.md)
* **Visual Scripting & C# Refactoring:** Read [.zoo/rules/UnityVisualScripting-conversion.md](file:///home/brian/UnityProjects/Terraforming%20Tendencies/.zoo/rules/UnityVisualScripting-conversion.md)
* **AI Unity CLI Automation:** See **§40 Unity CLI & Live Editor Automation** and [.zoo/rules/UnityCLI-Automation.md](.zoo/rules/UnityCLI-Automation.md)
* **Agent Rules:** See [`AGENTS.md`](AGENTS.md) at the repo root (also mirrored in `.clinerules` and `.zoomodes` for tool-specific configs).

---

> **Design Note — Spoke & Hub Text Adventure Philosophy:**
> The Universal Command Center (UCC) is the central hub. Sectors are the spokes radiating outward. The game loop: start at UCC → explore outward along sector spokes → discover nodes → return resources to UCC → upgrade → push further out. This maps naturally to a text adventure / roguelike structure. The sector nodes, discovery UI flavor text, and chain exploration already support this. Future design should lean into this: make the game equally playable as a text-driven experience where the player reads node descriptions, makes strategic choices from the UCC hub, and watches the colony grow. The 3D RTS layer is the visual reward — the strategic depth comes from the spoke-and-hub expansion decisions.
This document serves as a persistent memory bank for AI context, detailing the core systems, recent architectural decisions, and current state of the game's economy.


#### 1. Automated Economy & AI (GreedyAIController)
* **Logarithmic Spending:** The AI limits its spending mathematically so it doesn't instantly bankrupt the player's treasury.
* **Strict Start Priority:** To prevent the AI from deadlocking itself, it bypasses the spending limit specifically for the *very first* Probe drone. It uses an ignoreReserve flag to forcefully queue the probe. Once the probe is queued, the AI unlocks and interleaves Construction and Mining drones normally.
* **Unit Assignment:** The AI dynamically scans for GatherableSupplies and automatically assigns idle Mining Drones. It explicitly targets supplies that are children of the PlanetGenerator to avoid mining invalid debris.
* **Expansion Priority:** When a new Command Post is established via the EnergyPipelineManager, it uses BuildPriorityUnlockable to queue a starter Probe. Both GreedyAIController and AIController explicitly check BaseBuilding.IsFirstInQueueProbe() to avoid filling the queue before this starter unit is registered.

#### 2. Colony Expansion & Mobile Forge (FoundryCrawler & EnergyPipelineManager)
* **Pipeline Logistics:** The EnergyPipelineManager drives the expansion of the colony. It spawns the FoundryCrawler which slowly crawls along the pipeline path (movementSpeed = 0.05f).

#### 3. Drone Routing & Economy (WorkerBrainController & Supplies)
* **Identification:** Drones perfectly identify what they are holding by checking both the SupplySO.name and the physical GameObject.name (fallback).

#### 4. Game Over Logic (GameOverManager)
* **Depletion Checks:** The game continuously checks if there are valid ways to recover. If Materials is low and all GatherableSupply nodes on the map are destroyed, it triggers Game Over. *(Note: This logic is being repurposed for the new Micro-Round Depletion Trigger).*
* **Pipeline Protection:** To prevent false Game Overs when a drone fully depletes a fuel node, the GameOverManager explicitly checks if there is an active EnergyPipelineManager still expanding. If a pipeline is still building, it assumes more resources will be spawned shortly and aborts the Game Over.
* **Quit & Scene Unload Safety:** Employs static isQuitting tracking via Application.quitting to automatically suppress any loss-checking or GameOver events during scene teardown, editor playmode transition, or application exit. This prevents false Game Over prompts during destruction of scene objects on shutdown.

#### 5. UI & Selection Indicators

#### 6. Recent Fixes Changelog
* **Auto-colonize on sector complete (2026-09-01):** Finishing a terraforming round and advancing generation (`StartNextGeneration`) now **auto-unlocks and claims the geographically closest sector** that still needs a Command Post (`SectorColonization.TryColonizeClosestSectorNeedingCommandPost`). Orbital Scan / Survey Drone still work as optional early unlocks; `TryWinAndColonizeNextSector` no longer requires a scan step.
* **Sector colonization softlock (2026-08-31):** Only 3 solar clusters/sector + fog-gated pads meant climate buildings (temp) could softlock after pads filled; Survey/Orbital unlock did not claim with a Command Post and mid-run CP was locked until expansion. Now: **6 clusters/sector**, unlock reveals pad fog + **auto-places Command Post**, CP allowed whenever an unlocked sector is unclaimed, and `TryWinAndColonizeNextSector` is the strict live bot (CLI default).
* **Solar prereq hand softlock (2026-08-31):** Unmet climate unlocks (e.g. blue WATER buildings) stayed in hand before their cluster had solar, filling all slots so Solar Panel never cycled back from discard. `EnsureSolarPrereqInHand` now reseats Solar whenever open solar pads exist (makes room by dropping a pad-blocked unlock if needed). Draw pile also gets +2 Solar infra copies.
* **Solar never auto-wires to Command Post (2026-08-31):** `ConnectPowerGeneratorToCommandPost` skips all solar panels (not only cluster-flagged ones) and severs any CP links. Reserved-site builds occupy the pad **before** `CompleteConstruction` so cluster solar is recognized the same frame. Cluster consumers still auto-wire to their pad solar.
* **Mining drone proximity repair (2026-08-31):** Mining drones passively heal damaged friendly buildings within ~14m while gathering/idling (`WorkerBrainController.TickProximityRepair`), without abandoning their gather loop.
* **Unity CLI agent rules documented (2026-08-31):** See **§40**. Agents use the experimental Unity CLI + Pipeline package against the **already-open** Editor (`unity status` / `unity command` / `eval`). Do **not** spawn a second Editor via `unity test` / `build` / `run` / `-batchmode` while this heavy project is open. Sector win automation: `SectorWinAutomation` + `tools/sector-win-cli.sh` (live `eval`), or `unity command run_tests --mode playmode --filter SectorWinAutomationTests` on the connected Editor.
* **Unity MCP deprecated (2026-09-05):** Agents must not use Unity MCP / Cursor `user-Unity` / `unity mcp`. Live Editor automation is **Unity CLI only**.
* **Climate softlock fix (2026-08-31):** `Supplies` climate/biomass/oxygen caps used **occupied** sector count, so with no Command Post occupation atmos/water maxed near 0 and gen 1 targets (0.25 atm, 5% water) were unreachable (~42% progress). Caps now use **unlocked** sectors and never sit below the **current generation’s win targets**.
* **FIFO hand draw + sector-goal colors (2026-08-31):** See **§37 Card Deck FIFO**, **§38 Sector Goal Colors**, and **§39 Colony Integrity Start Gate**. Random shuffle / priority-promote draw was replaced with a stable FIFO queue so sector-finish cards eventually cycle into the hand. Active Objectives and hand buttons share colors **only** for sector-completion terraforming goals. Sector-win cards are **duplicated once** in the draw pile (~2× frequency) without changing shared assets.
* **Biomass terraforming deprecated (2026-09-03):** Climate basics are **Temperature / Atmosphere / Water** only. Biomass is no longer a sector-completion goal or gen-1 primary milestone (replaced by Temperature). Biomass resource may still exist for economy/food.
* **New-sector handoff (2026-09-04):** After colonization UI acknowledgement, camera focuses the new CP, markers refresh, hand seats Solar + unmet TEMP/ATMOS/WATER (+ primary) tools, and the new CP is selected. Expansion entry also refreshes hand / fires `OnGenerationStarted`. Climate IncrementalProgress treats absolute-target-met as complete (avoids CompleteExpansion softlock).
* **Per-sector climate mini-game (2026-09-04):** Each sector requires a **fresh** climate delta from round-start baselines (+15°C / +0.25 atm / +5% water) — prior-sector absolute gains do not finish the next sector. Only **ActiveSector** buildings contribute Temp/Atmos/Water generation. ClimateManager card tick-ups clear on `RecordBaselines`. Caps floor to round targets so high baselines are not soft-locked.
* **Atmosphere not progressing (2026-09-05):** Silent failures: prefab `Progress=Destroyed` could latch `hasCompletedConstruction` before the drone finished (powered look, no climate tick); climate rates could be 0 on cloned configs; edge pads could fail nearest-sector checks. Fixed by resetting completion on ghost init, binding card BuildingSO, name-based climate fallbacks, and pad-based active-sector climate checks.
* **Atmosphere still stalled (2026-09-05):** `Carbon Dioxide Import Laser` was classified as a **mine** (`name.Contains("laser")`), so it never auto-wired to cluster solar. Prefabs also left `BuildingSO` null (only `UnitSO`), which zeroed power-grid generation/upkeep lookups. Fixed mine detection, `ResolvedBuildingSO` for climate+power, broader cluster solar connect, and prefab BuildingSO wiring. Diagnose live with `ClimateGenerationTicker.ReportStatus()`. Regression: EditMode `ClimateGenerationTests` + Play Mode / CLI `ClimateGenerationAutomation.TryVerifyAtmosphereRises()` (needs `editor_play`).
* **Sector targets never completing (2026-09-05):** `CheckMilestones` only ran on idle-turn `OnTurnMilestones`, so real-time climate gains often never finished the sector. Now also checked every frame after the round grace period. Active Objectives shows **remaining** climate need (and DONE) instead of a fixed full-delta that never shrank.
* **Pink atmosphere buildings went silent (2026-09-05):** Unlocking/colonizing another sector mid-round overwrote `ActiveSector`, so Temp/Atmos/Water only counted in the new empty sector. Added stable `TerraformingSector` focus (set on gen start / post-round handoff only). Climate checks use `GetClimateFocusSector()`.
* **Atmos stuck at 0.26 (2026-09-05):** Real-time watch showed Atmos rising then freezing at the sector win line (`Cap == roundTarget == 0.26`) while condensers still ran — `UpdateAtmosphere` clamped every tick. Caps now leave headroom through the **next** generation’s cumulative target so the top bar keeps moving; sector win still uses baseline deltas. Diagnose/watch: `ClimateGenerationAutomation.DiagnoseAtmosphere()` / `StartAtmosphereWatch()`.
* **Sector never greened / advanced at 0.26 (2026-09-05):** Hitting Atmos 0.26 did not mark the goal met or finish the sector because `(0.26f - 0.01f) / 0.25f` is slightly under `1` (float error). `RoundDeltaProgress` now treats near-complete deltas as done. Objectives UI clarifies that **Temp + Atmos + Water** must all be green before the sector advances.
* **Free hex camera pan (2026-09-05):** WASD/arrow hex stepping may focus **any** map hex (including shrouded). Camera movement does **not** call `Reveal()` — fog clears only via vision / exploration / colonization. Mouse hover outlines still use revealed hexes only.
* **Card hand bottom-left (2026-09-05):** Playing-card hand docks bottom-left; empty slots ignore layout so the strip does not spill past the screen edge.
* **5-card hand readability (2026-09-05):** Hand capped at **5** cards; larger face (≈158×220). Title + materials cost sit **inside** the card (cost was previously drawn below the button bounds). Affordable costs gold, unaffordable red.
* **Hand above Bottom Bar (2026-09-05):** Card hand docks **above** the classic Bottom Bar (~300px) so the middle selection-info panel (Building Selected Container) is no longer covered.
* **Free cards priced (2026-09-05):** Themed BuildingSOs had `Cost=null` (UI showed Free). Assigned SupplyCost assets (100–400 Materials) and code fallbacks. Non-building cards use `GetMaterialsPlayCost()` (Orbital Scan 50, Survey Drone 75, Pipeline Boost 50; Emergency Caches stays free).
* **Buildings still placed free (2026-09-06):** `HasEnoughMaterials` / `ConsumeMaterials` treated `building.Cost == null` as success and skipped the charge. Now always use `GetMaterialsCost()` (with floors). Card cost chip moved top-left for visibility.
* **Oxygen Processor reserved-site opacity (2026-08-30):** Instant pad builds call `CompleteConstruction` before `Start`. `SmokestackVisuals` now seeds `FinalMaterial` (not ghost) and updates `BaseBuilding.SetPrimaryMaterial`; `Start` skips re-applying a captured ghost `primaryMaterial` after completion.
* **Resource discovery vs sector unlock (2026-08-30):** Unlocking a sector no longer mass-`ForceDiscover`s known deposit types. Deposits stay hidden until node exploration (`RevealGatherableAtNode`) or a discovery card reveals a type.
* **Reserved-site card builds (2026-08-30):** Building cards (Command Post → Solar Panel → Oxygen Processor / other paired buildings) play onto pre-placed pads, not free placement. See **§36 Reserved Site Pads**. Site-marker preview ghosts stay paused/translucent and must never occupy a pad. Sector 0 guarantees a CP pad + solar/oxygen cluster + minerals inside the starting fog reveal.
* **Drone-required construction (2026-09-05):** Pad builds (and free-placement builds) assign an idle worker drone instead of completing instantly. No idle drone → HUD warning **"A drone is needed."** Exceptions: auto-colonize CP (`waiveCost`) and the player's very first Command Post orbital drop. Under-construction ghosts reserve the pad until finished or cancelled.
* **Site ghosts looking like finished buildings:** Instantiating the solid prefab ran `BaseBuilding.Start` → `CompleteConstruction` → `BuildingSpawnEvent`, which locked the Command Post pad (`elig=0`) and made ghosts look solid. `BuildingSiteMarker` now instantiates under an inactive holder, disables simulation, forces `InitializeAsGhost` / Paused, strips colliders, and applies translucent placement materials **after** procedural meshes exist (`SmokestackVisuals` Awake).
* **Oxygen Processor ghosts stayed 100% opaque:** OP visuals are generated in `SmokestackVisuals.Awake` (URP Lit). Old Standard `_Mode` fade flags were ignored. Ghost materials are reapplied after activate; URP `_Surface=1` transparency is set; idle site tint is ~16–18% alpha. Highlighted pads use a higher alpha so card site-picking is readable.
* **OP not connecting to solar:** `ConnectToClusterSolar` now registers `PowerNode`s (Start may not have run in the same Instantiate frame) and auto-wires the cluster solar. `ConnectPowerCommand` falls back to the nearest completed building when the Solar Panel prefab has no collider to click.
* **Colonists entering ghost pads (selection dead):** Completed-looking ghosts were treated as shelter; `EnterBuilding` disabled the colonist collider. `BuildingSiteSlot.IsValidOccupant` is required for shelter/wander, and invalid shelter restores the collider.
* **Second mining drone unselectable:** Training from the Command Post is correct (including two at once). The invisible Universal Command Center had an 8×4×8 solid box at map center that stole raycasts from air drones. That collider is disabled; left-click selection prefers `AbstractUnit` over `GlobalCommander` / large buildings. Empty-ground click still selects the UCC.
* **Probe Build Order (race condition):** Added BaseBuilding.IsFirstInQueueProbe(). GreedyAIController and AIController now skip buildings whose first queued item is the Probe. Added regression test ColonyExpansionTests.ColonyExpansion_BuildsProbeDroneFirst.
* **Curved World Visuals:** Added an "Animal Crossing" style spherical planet illusion without breaking the flat NavMesh. Created CurvedWorld.shader and CurvedWorldUpdater.cs. 
* **Assorted Compiler Fixes:** Removed duplicate method definitions in PlanetGenerator.cs. Fixed an invalid cast warning by treating FoundryCrawler as an AbstractCommandable. Removed unused isProducing field from FoundryCrawler.cs.
* **Application Quit False Triggers:** Solved a critical issue where OnDestroy callbacks on GatherableSupply nodes during scene unloads triggered a cascade of SupplyDepletedEvents, resulting in a false "Resources depleted" screen. 
* **End-of-Generation UI Fixes:** Resolved an issue where the `GenerationSummaryUI` and `TechTreeUI` would fail to appear upon generation completion. Both UIs now aggressively check and re-enable their root GameObjects if they were disabled in the Inspector, ensuring the `panel.SetActive(true)` calls correctly display the UI. Also added detailed reference validation logs to `TechTreeUI` to prevent silent failures if Inspector variables are unassigned.
* **Tech Tree UI Formatting:** Fixed text overlapping and unreadable shrinking in the `TechTreeItemUI` prefab. Text now wraps correctly and is locked to a readable minimum 14pt size. Removed hardcoded C# UI sizing logic so the `TechTreeUI` Grid Layout sizes can be managed strictly inside the Unity Inspector. Added `TestTechTree.cs` editor menu tool to instantly open the UI from Playmode with 5000 test coins.
* **Instant-Depletion Loop Bug:** Fixed a "Time Travel" physics issue where clicking "Next Generation" would instantly end the generation again on the very next frame. Because Unity's `Destroy()` queues objects for cleanup at the end of the frame, the `GenerationManager` was counting dying resource nodes from the previous round before they vanished. Added a 2-second `roundStartTime` grace period to let Unity physics settle before the generation begins counting remaining resources.
* **Sector Capping & Stuck Progress Bar:** Resolved a milestone progression issue on smaller planets (e.g., Planet 1 - Easy) by dynamically capping `MaxGenerations` to match the actual sector count (4) during initialization in `GenerationManager.InitializeGenerations()`. Also resolved a visual bug by explicitly resetting the generation progress bar UI back to 0% when starting any generation or entering the expansion phase.
* **Command Post / Center Construction Unlock & Active Round Lockout:** Fixed a progression lock during the expansion phase where the player was unable to build a new Command Post. The issue stemmed from `BlueprintDraftManager.knownBuildings` being cleared on reset and never registering default/starting buildings like the `"Command Post"`. Resolved this by automatically loading and registering all `BuildingSO` assets from `Resources` on reset, adding case/space-insensitive fuzzy matching and lazy-loading fallbacks, and explicitly unlocking the blueprint when transitioning to the expansion phase. Also addressed a bug where the Command Post incorrectly remained buildable during standard rounds (Generations 1-4) by modifying `BuildBuildingCommand.IsLocked()` to directly lock out Command Posts unless `GenerationManager.Instance.IsExpansionPhase` is active.
* **Oxygen Milestone Target Scaling:** Solved a critical pacing bug where the Oxygen milestone in later sectors would instantly auto-complete. Because global oxygen carries over between sectors, starting the Oxygen round in Sector 1 instantly passed the hardcoded 25% target using Sector 0's metrics. Fixed this by updating `GenerationManager.InitializeDefaultMilestones()` to dynamically scale the target value based on the number of unlocked sectors (e.g., 25% in Sector 0, 50% in Sector 1, 75% in Sector 2, 100% in Sector 3).
* **Card Draft Auto-Completion Fix:** Solved a critical issue where drafting a resource-granting card (e.g., +250 Biomass) at the start of a generation caused the round to instantly auto-complete on the first frame. This occurred because `GenerationManager.StartNextGeneration()` was recording the baseline values *before* the card effects were applied, meaning the newly granted starting resources were incorrectly counted as progress earned during that generation. Resolved this by subscribing `GenerationManager` to `BlueprintDraftManager.OnDraftCompleted` and recalculating/overwriting `RecordBaselines()` and resetting the grace timer immediately after the chosen draft card's effects are applied.
* **Static Power Capacity & Auto-Completion Fix:** Resolved a major bug where power milestones (e.g., Generate 20 Grid Power) would automatically complete on their own over time. The issue was that `BaseBuilding.cs` `UpkeepRoutine` was continuously adding and subtracting power every second inside a coroutine loop, causing the global `Supplies.Power` value to accumulate like a stockpile resource. Converted Power into a static net capacity metric by: (1) removing the loop-based updates from `BaseBuilding.cs` and (2) updating `PowerGridManager.RecalculateGrids()` to calculate the exact net power surplus across all grids dynamically and report it to `Supplies.UpdatePower()`. This ensures the power level only changes when generation or upkeep capacity changes, allowing `pow - baselinePower` to evaluate correctly.
* **Command Post Placement & Invisibility Fix:** Resolved a critical bug where the Command Post would fail to render in the scene upon placement. The issue was two-fold: (1) the `Command Post` prefab had its `UnitSO` and `BuildingSO` fields unassigned (set to `null`), preventing it from identifying itself as a Command Post and scaling its `VisionTransform` to clear the Fog of War, and (2) the prefab's default material for the `Building_02` child mesh was overwritten and saved as the transparent `Building Ghost Placement` material instead of the solid `SciFi Toon` material. This caused the building to render as a transparent ghost permanently even after construction completed. Fixed by programmatically restoring the solid `SciFi Toon` material as the default material on the prefab mesh and correctly assigning the `Command Post` `BuildingSO` asset references.
* **Command Post Health & Spawn Healing Fix:** Resolved an issue where instant-placed/spawned Command Posts were destroyed by decay almost immediately after placement. Boosted the Command Post's default max health from `200` to `1000` on both its `BuildingSO` asset and its prefab configuration. Also updated `CompleteConstruction()` in `BaseBuilding.cs` to set `CurrentHealth = MaxHealth` unconditionally upon construction completion, ensuring buildings do not spawn near-dead if initialized with non-zero fractional values.
* **Batch Building Prefab Materials Fix:** Fixed a project-wide rendering bug where almost all buildings (23 prefabs total, including Barracks, Spaceport, Supply Hut, and various themed buildings) were permanently transparent/invisible after placement. Discovered that their main visual child meshes (`Building_02`) had their default materials overwritten and saved as the transparent `Building Ghost Placement` material directly inside the asset files. Executed a batch script to resolve this by mapping each prefab to its correct solid material: assigning the custom themed material (e.g., `Atmospheric CondenserMaterial` for the Atmospheric Condenser) to the themed buildings, and the standard `SciFi Toon` material to the basic buildings (Barracks, Supply Hut, etc.). This restores their unique textures and ensures they render correctly.
* **Temporary Command Post Startup Power & Grace Period Fix:** Solved a critical deadlock pacing issue at startup. Added a temporary backup power cell system in `PowerNode.cs` that grants the Command Post 90 seconds of startup power upon completion, allowing the player to train their first worker drones and place Solar Panels before the grid drops to negative net power. Also modified `GameOverManager.cs` to ignore editor-placed dummy command centers (e.g. `Universal Command Center` placed at `(0,0,0)`) and suspend **loss checks** until the player's first gameplay-instantiated Command Post (containing `(Clone)` in its name) is placed. **Colony integrity drain / recalculation** is separately gated by `Supplies.ColonyIntegrityActive` — see **§39** (starts on first real building, not only Command Post).
* **Colony Integrity Decay Override Removal:** Resolved a bug where colony integrity would automatically collapse and destroy the colony in 10 seconds regardless of player actions. Discovered that a helper component `DecayStarter` was dealing 5 damage to itself every 0.1 seconds and directly overwriting the global integrity metric with its own health ratio. Removed the self-damaging update loop from `DecayStarter.cs`, allowing `GlobalDecayManager` to authoritatively calculate and update colony integrity based on the actual health ratios of all active buildings. **`DecayStarter` and `GlobalCommander` (UCC) are excluded from `Supplies.CalculateIntegrity()`** — the hidden stand-in and invulnerable hub must not inflate or mask the colony bar. Until `ColonyIntegrityActive` is true, `CalculateIntegrity()` returns **100%** and decay does not run.
* **Consumable Cards & Hand Battler Mechanics:** Implemented a major gameplay shift making both building blueprints and unit production options act as consumable cards that are discarded immediately when played:
  * **Consumable Blueprints:** Modified `Handle()` in `BuildBuildingCommand.cs` to call `BlueprintDraftManager.LockBuilding()` on the building's name immediately when the player places a building ghost (or triggers an orbital drop). This locks the blueprint and removes it from the active actions bar right at the moment of placement, rather than waiting for construction completion.
  * **Consumable Unit Production:** Modified `Handle()` in `BuildUnitCommand.cs` to call `RemoveBuildUnitCommand()` immediately when a unit training order is queued (clicked). This discards the training button from the building's active commands array and refreshes the bar instantly, making unit training buttons one-time consumable cards.
  * **Play-and-Draw Loop:** Playing / consuming a hand card calls `FillHand()`, which draws the next **playable** card from the **front** of the FIFO draw pile (see **§37**). Unplayable cards are sent to the back of discard and skipped until recycled. There is **no** random reshuffle and **no** priority promotion of sector cards.
  * **Event-Driven UI Refresh & Duplicate Bar Prevention:** Restored the use of the `UpgradeResearchedEvent` broadcast in both `CardDeckController.cs` and `BaseBuilding.cs`. Since this event is handled globally by both the persistent bottom bar (`BottomBarActionsUI`) and the original selection-driven bar (`ActionsUI` via `RuntimeUI`), it keeps both bars perfectly synchronized and updated upon cards playing. Also added a `FindAnyObjectByType()` fallback in `RuntimeUI.cs` `Awake()` before creating a dynamic bottom bar, preventing duplicate bottom bars from spawning when one is already active in the scene. Finally, added critical null guards to `HandleUpgradeResearched()` in `AbstractCommandable.cs` to prevent `NullReferenceExceptions` when upgrades are researched or when commands are updated, safeguarding the game from stability crashes.
  * **Editor-Placed Building Preservation:** Removed the forceful destruction check of editor-placed buildings in `BaseBuilding.cs` `Awake()`. The asynchronous cleanup of destroyed starting Command Posts was incorrectly locking the Command Post card at startup (since the old object was still registered in `ActiveBuildings` during the first frame). This restoration allows level designers and testers to lay out starting structures directly in the Unity Editor normally. Builds require an **idle** worker drone; if none is available, the HUD shows **"A drone is needed."** (busy drones are not auto-reassigned).
  * **First Command Post Orbital Drop:** Updated `BuildBuildingCommand.cs` `Handle()` to forcefully bypass worker drone searches and trigger the instant orbital drop (completing construction immediately) for the player's very first Command Post. This ensures that when the player starts the game with 0 worker drones and 0 buildings, they can always place and instantly construct their starting base. Subsequent Command Center expansions placed during the expansion phase will naturally require builder drones.
  * **Command Center Operating Exception (Power Softlock Fix):** Modified `IsOperating` in `BaseBuilding.cs` to return `true` authoritatively for Command Posts/Centers. Previously, when the Command Post's 90-second temporary startup backup power expired, it counted as unpowered and ceased operating. This disabled the range-checking life support zone surrounding it, preventing the player from placing any Solar Panels to restore power, resulting in a permanent startup softlock. Command Centers now remain authoritatively operational even when unpowered, allowing recovery by placing power structures. Also exempted Command Centers from unit training power upkeep stalls inside `DoBuildUnits()`, ensuring that unpowered bases can still successfully train worker drones.
  * **Universal Command Center Invulnerability:** Modified `TakeDamage()` in `AbstractCommandable.cs` and `Start()` in `GlobalCommander.cs` to make the starting `Universal Command Center` (GlobalCommander) completely invulnerable to all damage, decay, and health changes, with a maximized health pool of `99999`. This ensures it never takes damage or requires upkeep to run.
  * **Command Post Auto-Placement & Clone Filtering:** Re-enabled the hardcoded auto-placement block in `PlayerInput.cs` `HandleActionSelected()` which automatically builds Command Centers at the nearest unoccupied sector center when the UI card is clicked. In addition, updated `Handle()`, `IsLocked()`, and `AllRestrictionsPass()` in `BuildBuildingCommand.cs` to check if existing Command Post game object names contain `"Clone"` (case-insensitive) to filter out editor-placed debug structures (like the `Universal Command Center`). This prevents starting expansions from being locked out or blocked physically by preplaced scene items. Also added an explicit `InvalidOperationException` inside `ActivateAction()` of `PlayerInput.cs` to crash loudly if the `GlobalCommander` is missing when executing a global action; this facilitates prompt debugging if another system destroys the invulnerable starting base.
  * **Static State Restarts Memory Leak & Persistence Fixes:** Subscribed `sceneLoaded` handlers to the Unity SceneManager in `Supplies.cs`, `BaseBuilding.cs`, and `BlueprintDraftManager.cs`. Previously, when restarting the level or playing the game a second time within the same Editor session, static fields (like available resources, list of active buildings, unlocked blueprints, and buffs) were not reset, causing the resources from the end of the first game (e.g. 0 materials) and active building lists to carry over and lock the player out of placing their starting base. Static fields are now cleanly reset on every scene load.
  * **Singleton Self-Destruction Reload Fixes:** Corrected the `Awake()` singleton initialization logic in `SectorManager.cs`, `PlanetGenerator.cs`, `CardDeckController.cs`, `CardDeckManager.cs`, `BuildingUpkeepManager.cs`, `ExplorationManager.cs`, `GreedyAIController.cs`, and `Supplies.cs`. Previously, these managers had duplicate-destroy checks (`if (Instance != null && Instance != this) Destroy(gameObject)`). On scene reload, Unity instantiates the new scene's objects *before* destroying the old scene's unloading objects. Consequently, the new singletons saw the old ones still in memory, mistakenly identified them as duplicates, and destroyed themselves. When the old scene then unloaded, both instances were gone, leaving the game without singletons (such as `SectorManager`), which broke camera tracking and building placement. Overwriting `Instance = this;` unconditionally on reload resolved the issue.
  * **Global Drone Card Routing (Worker Selection Lockout Fix):** Updated `CanHandle()` and `Handle()` in `BuildUnitCommand.cs` to automatically route drone training requests to the player's Command Post even if a non-production unit (like a worker drone) is currently selected. Previously, clicking the "Train Mining Drone" card on the persistent bottom bar while having a worker drone selected evaluated `CanHandle` on the worker, which failed and did nothing. Drones can now be trained globally at any time.
* **GlobalDecayManager Console Spam Removal:** Removed all constant `Debug.Log` statements inside the main decay loop and damage triggers in `GlobalDecayManager.cs` to keep the console clean and free of redundant, repetitive output during gameplay.
* **GHG Factory & Themed Building Passive Climate Generation Fix:** Resolved a critical bug where the GHG Factory (and several other themed buildings) stopped generating temperature/atmosphere/water passively. The root cause was that the `BuildingConfigSO` asset files for themed buildings (e.g., `GHG FactoryConfig.asset`) were created before the `temperatureGeneration`, `atmosphereGeneration`, and `waterGeneration` fields existed on `BuildingConfigSO`, so they defaulted to `0` at runtime. The [`UpkeepRoutine`](Assets/Scripts/Units/BaseBuilding.cs:726) checks `if (config.TemperatureGeneration > 0f)`, which evaluated to `false` for all pre-existing configs. Fixed by setting the correct climate generation values on the config assets: GHG Factory (`temperatureGeneration: 1`, `atmosphereGeneration: 0.02`), Atmospheric Condenser (`atmosphereGeneration: 0.02`), Carbon Dioxide Import Laser (`atmosphereGeneration: 0.03`), Geothermal Generator (`temperatureGeneration: 0.2`), Water Ice Aquifer (`waterGeneration: 0.5`), Subglacial Water Extractor (`waterGeneration: 0.3`), and Methanogenic Microbe Spreader (`temperatureGeneration: 0.3`). Also added a defensive code path in [`AddThemedBuildingCard()`](Assets/Scripts/UI/Containers/BlueprintDraftUI.cs:538) to propagate card-level climate generation values to pre-existing `BuildingSO` configs at runtime, preventing future regressions.
* **Exploration Cards & Command Post Sector Lock Re-architecture:** Reworked the sector expansion flow so that sector unlocks are driven by player-chosen exploration cards rather than automation. **Four changes:**
  1. **Removed auto-unlock from `StartNextGeneration()`** ([`GenerationManager.cs:391`](Assets/Scripts/Player/GenerationManager.cs:391)): The expansion phase no longer automatically calls `UnlockNextSector()`. Sector unlocking is now entirely player-driven through exploration cards.
  2. **Added exploration cards to the deck** ([`BlueprintDraftUI.cs:424`](Assets/Scripts/UI/Containers/BlueprintDraftUI.cs:424)): Created four `ScoutingCardSO` instances — **Orbital Scan** (instantly explores + unlocks the next sector), **Pipeline Boost** (2x scouting speed for 60s), **Survey Drone** (deploys probe to scout and unlock), and **Emergency Caches** (+300 Materials safety net). Cards gate themselves via `IsGateMet()` so they only appear when locked sectors remain.
  3. **Lock Command Post when no unoccupied sectors** ([`BuildBuildingCommand.IsLocked()`](Assets/Scripts/Commands/BuildBuildingCommand.cs:377)): During the expansion phase, if a Command Post is selected but there are no unlocked-and-unoccupied sectors available, the card is locked and cannot be played. The player must play an exploration card first to unlock a new sector.
  4. **Added auto-spawn to ExplorationManager** ([`ExplorationManager.cs:23`](Assets/Scripts/Environment/ExplorationManager.cs:23)): The `ExplorationManager` singleton was never placed in any scene and had no auto-spawn logic, so Orbital Scan/Survey Drone cards silently failed with `"No ExplorationManager found in scene!"`. Added a `[RuntimeInitializeOnLoadMethod]` auto-spawn that creates the manager before scene load, following the same pattern as `AudioManager`.

#### 7. Errors Encountered & Resolutions
* Fixed CS0111: Type 'PlanetGenerator' already defines a member called 'ScatterResources' by removing the empty stub.
* Fixed CS0104: 'Object' is an ambiguous reference between 'UnityEngine.Object' and 'object' in `CardDeckController.cs` and `BaseBuilding.cs` by explicitly qualifying the calls as `UnityEngine.Object.FindAnyObjectByType`.
* Fixed Shader error in 'Custom/URP_CurvedWorld' for the ShadowCaster pass by declaring _LightDirection and _LightPosition globals.

#### 8. HUD Standardization & Colony Expansion Integration
* **Category Re-Alignment:** Changed all top-bar HUD containers to use a vertically-stacked layout where the Header Label is centered at the top and the Metric is directly beneath it.
* **Fixed Stretched Icons:** Locked all HUD icons to a standard 24x24 pixel size via LayoutElement components.
* **Auto-Wiring:** Implemented self-wiring in RuntimeUI.Awake() to automatically link and subscribe to UI containers.
* **Expansion Progress Integration:** Restored the colony expansion progress metric back to its original integrated display inside the "Sectors" label. Increased the width of the Sectors HUD container to 220 to prevent clipping.
* **AI Auto-Spend Toggle Removal:** Removed the AI Spending Toggle element from the HUD and cleanly deleted AISpendingToggleUI.cs from the project.

#### 9. Persistent Audio System (AudioManager)
* **Soundtrack Integration:** Generated a 2-minute-56-second high-quality Stereo WAV space ambient soundtrack.
* **Persistent BGM:** Created AudioManager.cs as a persistent DontDestroyOnLoad Singleton that automatically spawns and plays the BGM using a RuntimeInitializeOnLoadMethod tag.
* **Smooth Volume Fading:** Features a built-in volume fader that smoothly transitions the audio from 0 to 0.5 volume over 3.0 seconds.

#### 11. Recent Visual and Economy Polish
* **Map Wrapping Visual Fix:** The CurvedWorldUpdater.cs has been moved to LateUpdate() to ensure it only queries the camera's final position, fixing a massive 1-frame backwards bend during teleports.
* **Dynamic Proximity Labels:** Added an OnGUI overlay to GatherableSupply.cs that dynamically draws the resource's name above the node when the camera is within 15 units.
* **Visual Shrinking:** GatherableSupply.cs now visually shrinks the node's transform proportional to the remaining amount (clamped to 30%) when resources are depleted.
* **Geography Camouflage for Fuel:** PlanetGenerator.ScatterFuelResources() now randomly selects non-crystal rock models, applies the dynamically generated ground color, and hides fuel resources as natural planetary geography.
* **Selection Indicator Sizing:** Modified BaseBuilding.Start() to intercept the selectionIndicator of the Command Post and shrink it by a factor of 0.6f.
* **Runtime Shader Injection:** Hooked ApplyCurvedWorldShader into AbstractCommandable.Start() to ensure all dynamically spawned units perfectly hug the bent terrain.

* **UI & UX Changes:** ProbeLogic no longer automatically calls TriggerExpansion; players place Command Centers directly again via their drone's Build UI. Added a persistent Volume Control Slider to the PauseMenuUI.

To prevent unregulated expansion across the map, the game's core progression loop now restricts exploration and building to explicitly unlocked **Sectors**.

##### 1. Sector Lockdown
* **Restricted Action:** The map is divided into Sectors. At the start of a game, only Sector 0 (the starting location) is unlocked. All other sectors are strictly locked.
* **Building Blocked:** The player cannot place building ghosts inside a locked sector. The auto-placement logic for Command Posts also ignores locked sectors.
* **Exploration Blocked:** Drones and units cannot be commanded to move into locked sectors. The input system physically drops movement commands into uncharted territory.

##### 2. Milestone Unlocks
* **Progression:** The player plays out the game as a standard RTS within their active, unlocked sectors.
* **Sector Expansion:** To expand, the player must meet specific "Terraforming Conditions" or milestones (determined by external drafting/roguelite mechanics).
* **Unlocking:** Once a milestone is met, a call to `SectorManager.Instance.UnlockNextSector()` dynamically peels back the lockdown on the next adjacent sector, allowing the player's colony to organically expand into the newly unlocked territory.

##### 3. AI Constraints
* **Fair Play:** The enemy AI (`GreedyAIController`) is fully bound to this new system. It will not attempt to analyze, scout, or build inside locked sectors, ensuring it does not cheat the progression rules.

#### 14. Current Tech Tree & Upgrade Paths
To prevent unregulated expansion across the map, the game's core progression loop now restricts exploration and building to explicitly unlocked **Sectors**.

##### 1. Sector Lockdown
* **Restricted Action:** The map is divided into Sectors. At the start of a game, only Sector 0 (the starting location) is unlocked. All other sectors are strictly locked.
* **Building Blocked:** The player cannot place building ghosts inside a locked sector. The auto-placement logic for Command Posts also ignores locked sectors.
* **Exploration Blocked:** Drones and units cannot be commanded to move into locked sectors. The input system physically drops movement commands into uncharted territory.

##### 2. Milestone Unlocks
* **Progression:** The player plays out the game as a standard RTS within their active, unlocked sectors. 
* **Sector Expansion:** To expand, the player must meet specific "Terraforming Conditions" or milestones (determined by external drafting/roguelite mechanics).
* **Unlocking:** Once a milestone is met, a call to `SectorManager.Instance.UnlockNextSector()` dynamically peels back the lockdown on the next adjacent sector, allowing the player's colony to organically expand into the newly unlocked territory.

##### 3. AI Constraints
* **Fair Play:** The enemy AI (`GreedyAIController`) is fully bound to this new system. It will not attempt to analyze, scout, or build inside locked sectors, ensuring it does not cheat the progression rules.

#### 14. Current Tech Tree & Upgrade Paths
The following represents the current state of the upgrade system defined in the game's scriptable objects (`Assets/Units/Upgrades/`):

##### Infantry Weapons Upgrade Path (Damage)
* **Level 1 (Infantry Weapons 1):** Increases unit attack damage by +1. *(No prerequisites)*
* **Level 2 (Infantry Weapons 2):** Increases unit attack damage by +1. *(Requires Level 1)*
* **Level 3 (Infantry Weapons 3):** Increases unit attack damage by +1. *(Requires Level 2)*

##### Attack Speed Upgrades
* **Rapid Fire (Rifleman Attack Speed):** Decreases unit attack delay by 0.25 seconds, significantly increasing the rate of fire. *(No prerequisites)*

#### 15. The 20-Level Engine Deck (Per-Unit Progression)
To support a massive roguelite Tech Tree, the game features a deep 20-level tech progression for every single unit. Upgrades alternate through 3 core stats per unit, scaling from Mk I all the way to Omega-tier power.

<details>
<summary><b>Mining Drone (Levels 1-20)</b></summary>

1. **Thrusters Mk I** (+Speed)
2. **Cargo Bins Mk I** (+Capacity)
3. **Drill Bits Mk I** (+GatherRate)
4. **Thrusters Mk II** (+Speed)
5. **Cargo Bins Mk II** (+Capacity)
6. **Drill Bits Mk II** (+GatherRate)
7. **Thrusters Mk III** (+Speed)
8. **Cargo Bins Mk III** (+Capacity)
9. **Drill Bits Mk III** (+GatherRate)
10. **Thrusters Mk IV** (+Speed)
11. **Cargo Bins Mk IV** (+Capacity)
12. **Drill Bits Mk IV** (+GatherRate)
13. **Thrusters Mk V** (+Speed)
14. **Cargo Bins Mk V** (+Capacity)
15. **Drill Bits Mk V** (+GatherRate)
16. **Advanced Thrusters** (+Speed)
17. **Advanced Cargo Bins** (+Capacity)
18. **Elite Drill Bits** (+GatherRate)
19. **Elite Thrusters** (+Speed)
20. **Omega Cargo Bins** (+Capacity)
</details>

<details>
<summary><b>Construction Drone (Levels 1-20)</b></summary>

1. **Thrusters Mk I** (+Speed)
2. **Alloy Plating Mk I** (+Health)
3. **Welding Torch Mk I** (+BuildSpeed)
4. **Thrusters Mk II** (+Speed)
5. **Alloy Plating Mk II** (+Health)
6. **Welding Torch Mk II** (+BuildSpeed)
7. **Thrusters Mk III** (+Speed)
8. **Alloy Plating Mk III** (+Health)
9. **Welding Torch Mk III** (+BuildSpeed)
10. **Thrusters Mk IV** (+Speed)
11. **Alloy Plating Mk IV** (+Health)
12. **Welding Torch Mk IV** (+BuildSpeed)
13. **Thrusters Mk V** (+Speed)
14. **Alloy Plating Mk V** (+Health)
15. **Welding Torch Mk V** (+BuildSpeed)
16. **Advanced Thrusters** (+Speed)
17. **Advanced Alloy Plating** (+Health)
18. **Elite Welding Torch** (+BuildSpeed)
19. **Elite Thrusters** (+Speed)
20. **Omega Alloy Plating** (+Health)
</details>

<details>
<summary><b>Probe (Levels 1-20)</b></summary>

1. **Thrusters Mk I** (+Speed)
2. **Optics Mk I** (+ScanRadius)
3. **Processor Mk I** (-AnalysisTime)
4. **Thrusters Mk II** (+Speed)
5. **Optics Mk II** (+ScanRadius)
6. **Processor Mk II** (-AnalysisTime)
7. **Thrusters Mk III** (+Speed)
8. **Optics Mk III** (+ScanRadius)
9. **Processor Mk III** (-AnalysisTime)
10. **Thrusters Mk IV** (+Speed)
11. **Optics Mk IV** (+ScanRadius)
12. **Processor Mk IV** (-AnalysisTime)
13. **Thrusters Mk V** (+Speed)
14. **Optics Mk V** (+ScanRadius)
15. **Processor Mk V** (-AnalysisTime)
16. **Advanced Thrusters** (+Speed)
17. **Advanced Optics** (+ScanRadius)
18. **Elite Processor** (-AnalysisTime)
19. **Elite Thrusters** (+Speed)
20. **Omega Optics** (+ScanRadius)
</details>

<details>
<summary><b>Command Post (Levels 1-20)</b></summary>

1. **Scaffolding Mk I** (-BuildTime)
2. **Bio-Dome Mk I** (+LifeSupportRadius)
3. **AI Schedulers Mk I** (+QueueSize)
4. **Scaffolding Mk II** (-BuildTime)
5. **Bio-Dome Mk II** (+LifeSupportRadius)
6. **AI Schedulers Mk II** (+QueueSize)
7. **Scaffolding Mk III** (-BuildTime)
8. **Bio-Dome Mk III** (+LifeSupportRadius)
9. **AI Schedulers Mk III** (+QueueSize)
10. **Scaffolding Mk IV** (-BuildTime)
11. **Bio-Dome Mk IV** (+LifeSupportRadius)
12. **AI Schedulers Mk IV** (+QueueSize)
13. **Scaffolding Mk V** (-BuildTime)
14. **Bio-Dome Mk V** (+LifeSupportRadius)
15. **AI Schedulers Mk V** (+QueueSize)
16. **Advanced Scaffolding** (-BuildTime)
17. **Advanced Bio-Dome** (+LifeSupportRadius)
18. **Elite AI Schedulers** (+QueueSize)
19. **Elite Scaffolding** (-BuildTime)
20. **Omega Bio-Dome** (+LifeSupportRadius)
</details>

<details>
<summary><b>Rifleman (Levels 1-20)</b></summary>

1. **Iron Sights Mk I** (+Damage)
2. **Auto-Loader Mk I** (-AttackDelay)
3. **Kevlar Mk I** (+Health)
4. **Iron Sights Mk II** (+Damage)
5. **Auto-Loader Mk II** (-AttackDelay)
6. **Kevlar Mk II** (+Health)
7. **Iron Sights Mk III** (+Damage)
8. **Auto-Loader Mk III** (-AttackDelay)
9. **Kevlar Mk III** (+Health)
10. **Iron Sights Mk IV** (+Damage)
11. **Auto-Loader Mk IV** (-AttackDelay)
12. **Kevlar Mk IV** (+Health)
13. **Iron Sights Mk V** (+Damage)
14. **Auto-Loader Mk V** (-AttackDelay)
15. **Kevlar Mk V** (+Health)
16. **Advanced Iron Sights** (+Damage)
17. **Advanced Auto-Loader** (-AttackDelay)
18. **Elite Kevlar** (+Health)
19. **Elite Iron Sights** (+Damage)
20. **Omega Auto-Loader** (-AttackDelay)
</details>

<details>
<summary><b>Grenadier (Levels 1-20)</b></summary>

1. **Powder Mk I** (+AoE Radius)
2. **Shrapnel Mk I** (+MaxHits)
3. **Armor Mk I** (+Health)
4. **Powder Mk II** (+AoE Radius)
5. **Shrapnel Mk II** (+MaxHits)
6. **Armor Mk II** (+Health)
7. **Powder Mk III** (+AoE Radius)
8. **Shrapnel Mk III** (+MaxHits)
9. **Armor Mk III** (+Health)
10. **Powder Mk IV** (+AoE Radius)
11. **Shrapnel Mk IV** (+MaxHits)
12. **Armor Mk IV** (+Health)
13. **Powder Mk V** (+AoE Radius)
14. **Shrapnel Mk V** (+MaxHits)
15. **Armor Mk V** (+Health)
16. **Advanced Powder** (+AoE Radius)
17. **Advanced Shrapnel** (+MaxHits)
18. **Elite Armor** (+Health)
19. **Elite Powder** (+AoE Radius)
20. **Omega Shrapnel** (+MaxHits)
</details>

<details>
<summary><b>Warrior (Levels 1-20)</b></summary>

1. **Sharpened Blade Mk I** (+Damage)
2. **Adrenaline Mk I** (+Speed)
3. **Shielding Mk I** (+Health)
4. **Sharpened Blade Mk II** (+Damage)
5. **Adrenaline Mk II** (+Speed)
6. **Shielding Mk II** (+Health)
7. **Sharpened Blade Mk III** (+Damage)
8. **Adrenaline Mk III** (+Speed)
9. **Shielding Mk III** (+Health)
10. **Sharpened Blade Mk IV** (+Damage)
11. **Adrenaline Mk IV** (+Speed)
12. **Shielding Mk IV** (+Health)
13. **Sharpened Blade Mk V** (+Damage)
14. **Adrenaline Mk V** (+Speed)
15. **Shielding Mk V** (+Health)
16. **Advanced Sharpened Blade** (+Damage)
17. **Advanced Adrenaline** (+Speed)
18. **Elite Shielding** (+Health)
19. **Elite Sharpened Blade** (+Damage)
20. **Omega Adrenaline** (+Speed)
</details>

#### 16. End-of-Generation Summary UI Hierarchy Restructure (UI Controller Pattern)
* **The Core Bug:** The `GenerationSummaryUI` component was attached directly to the visual panel (`'Generation Summary Panel'`). In `Start()`, the visual panel deactivated itself, thereby disabling the component and preventing its `OnEnable()` from ever subscribing to `GenerationManager.OnGenerationEnded`. 
* **The Architecture Fix (UI Controller Pattern):** 
  * Separated the **logic (event listener)** from the **visual panel (rendering)**.
  * Created an always-active parent GameObject named **`Generation Summary UI Controller`** to host the `GenerationSummaryUI` component.
  * Placed the actual visual panel (`Generation Summary Panel`) as a child of the controller and **disabled it in Edit Mode** (cleanly hiding it from view).
  * Wired the disabled visual panel into the script's `panel` slot. Now, when the event fires, the active controller cleanly sets the visual panel child to active.
* **Diagnostics Tool:** Created a custom editor diagnostic utility accessible via **`Tools > Diagnostics > Check Generation Summary UI`** to automatically identify, report, and correct any disabled parent structures or unassigned inspector fields.

#### 17. Building Upgrades Architecture & Baseline Configurations
* **Decay Coverage & Build Time Scaling:** 
  * Upgraded **`BuildingSO.cs`** to cleanly clone its specialized `BuildingConfig` upon spawning to guarantee runtime upgrades remain isolated from persistent editor assets.
  * Enhanced **`BaseBuilding.cs`** to calculate unit production/research queue build times based on the building's localized `BuildingConfig.BuildTimeMultiplier`.
  * Updated **`AbstractCommandable.cs`**'s `HandleUpgradeResearched()` method to support retroactive structural scaling: researching a max health upgrade instantly adds the HP difference and heals the structure, and researching a life support radius upgrade instantly scales the radius of the active `LifeSupportNode` component.
* **Default Configurations & Baselines:**
  * Discovered that all `BuildingSO` assets (`Command Post`, `Supply Hut`, `Spaceport`, `Infantry School`, `Barracks`, and `Oxygen Processor`) lacked physical config files, which would trigger a null reference crash upon any upgrade applications.
  * Created **`Default Building Config.asset`** (defaulting to Queue Size 5, Life Support Radius 25, and Build Time Multiplier 1.0) and auto-populated these to all buildings.
* **Custom Modifiers & Tech Tree Integration:**
  * Created 4 brand new physical modifier upgrades in `Assets/Units/Upgrades/BuildingUpgrades/`:
    * **`Spaceport Efficiency.asset`**: Reduces unit training times by 35% (`BuildingConfig/BuildTimeMultiplier = -0.35`).
    * **`Supply Node Range.asset`**: Expands decay-protection LifeSupportRadius of Supply Huts by 15 meters (`LifeSupportRadius = +15`).
    * **`Command Post Reinforcement.asset`**: Increases max Command Post health by 300 HP (`Health = +300`).
    * **`Tactical Processing.asset`**: Speeds up Infantry School research queues by 40% (`BuildingConfig/BuildTimeMultiplier = -0.40`).
  * Registered these modifiers to their respective building assets and successfully appended them to the global **`Human Tech Tree.asset`** to populate automatically in the player's Tech Tree GUI between generations.

#### 18. Power Grid Mechanics & Building Config Standardization
* **Power Grid Networks:** Verified that power functions as an interconnected undirected graph grid (`PowerGridManager` and `PowerNode`). A single Solar Panel can connect to multiple buildings, and buildings can daisy-chain connections. The grid remains powered as long as total generation across all connected nodes meets or exceeds the total upkeep.
* **UI Action Menu Slot Collisions:** Fixed an issue where "Build Habitat" and "Build Oxygen Processor" were mysteriously hidden in the `ActionsUI` sub-menu. Discovered that the UI panel utilizes fixed data slots, and multiple commands were competing for Slot 1 and Slot 4, causing overwrites. Re-assigned overlapping commands to empty slots (e.g., Habitat to Slot 6, Oxygen Processor to Slot 7).
* **Building Config Overhaul:** Discovered that almost all buildings were relying on the generic 0-upkeep `Default Building Config`. Generated and assigned dedicated `BuildingConfigSO` assets for the `Command Post`, `Spaceport`, `Supply Hut`, `Oxygen Processor`, `Infantry School`, and `Barracks` to properly assign baseline `powerUpkeep` values across the economy. Adjusted Solar Panel baseline generation to 5.
* **Double-Construction Bug:** Fixed a logic bug where `BaseBuilding.CompleteConstruction()` was invoked twice per building (once by the `WorkerBrainController`, once by `BaseBuilding.Start()`), which caused `UpkeepRoutine` to launch multiple overlapping coroutines. Implemented a `hasCompletedConstruction` boolean guard.

#### 19. Blueprint Card Deck & Sector Milestone Progression
* **1-Sector-Per-Round Paradigm:** One sector corresponds to one generation (round). Completing the round requires the **primary milestone** (Biomass / Oxygen / Power / Population / Command Posts) **and** temperature, atmosphere, and water targets for that generation (`GenerationManager.CheckMilestones` uses the bottleneck of all four).
* **Card deck (current):** `CardDeckController` owns a 10-card hand shown in `BottomBarActionsUI`. Cards load from `Resources/Cards` via `BlueprintDraftUI.InitializeDefaultPool()`. **Draft overlay rounds are disabled** (`TriggerDraft` / `ShowDraftSelection` are no-ops). **Sector unlock on round advance** is automatic (closest sector needing a Command Post); scouting cards (Orbital Scan / Survey Drone) remain optional for early unlocks.
* **Milestone Progression Engine:** `GenerationManager` tracks `CurrentMilestoneType` / targets; `UnlockPrerequisitesForMilestone()` unlocks relevant blueprints when a round starts. Climate gates on `TerraformingCardSO` still filter whether a card is playable (`IsGateMet` / `CanApply`).

#### 20. Themed Card Decks, Climate Gates, and Active Structure Abilities
* **Procedural Card/Building Generation:** Since the 20 themed buildings did not exist as pre-defined asset files, they are instantiated as `BuildingSO` and `TerraformingCardSO` ScriptableObjects at runtime by cloning a default building asset (`SolarPanel` or `Habitat`) and overriding its cost, stats, and configurations.
* **Climate Gates Filtering:** `TerraformingCardSO` defines environmental constraints (Temperature, Oxygen, Atmosphere) and geological requirements (`LavaTube`, `FaultLine`, `WaterDeposit`). Hand fill only admits cards that pass `IsGateMet()` **and** `CanApply()` (e.g. reserved pad available).
* **Worker Dynamic Commands Injection:** `Worker.cs` overrides `AvailableCommands` to inject dynamic `BuildBuildingCommand` instances based on drafted building blueprints registered in `BlueprintDraftManager`.
* **Active Abilities Integration:** `ActiveAbilityCommand.cs` handles active structure actions with cooldowns. `BaseBuilding.CompleteConstruction()` automatically attaches these commands to constructed active structures (*Deep-Core Mining Laser*, *Carbon Dioxide Import Laser*, *Methanogenic Microbe Spreader*, *Genetically Modified Algae Spreader*).

#### 21. Dynamic Drafting UI Details & Guaranteed Sector Resource Spawning
* **Guaranteed Sector Resource Spawning:** Refactoring `PlanetGenerator.ScatterResources()` guarantees that every single sector on the generated map contains at least one Mineral deposit and one Gas deposit inside its boundaries. It places guaranteed deposits by iterating mathematically through all sector grid cells, while maintaining safety clearances from the central exclusion zone (radius 15f) and minimum spacing (5f) from other nodes. Residual resources are randomly scattered across the rest of the map to meet the targeted map count.

#### 23. UI Raycast Blocking & Auto-Resolution Fallbacks
* **UI Raycast Blocking Bug:** Discovered that closing the `TechTreeUI` would hide its inner panel but leave its root GameObject active. Since the root GameObject spans the entire screen canvas with a Graphic Raycaster, it blocked all mouse raycasts from hitting the underlying `GenerationSummaryUI` buttons. Now, closing `TechTreeUI` deactivates its root GameObject to free up raycasts. We keep `GenerationSummaryUI`'s controller root GameObject active during navigation to prevent disabling any nested child components (such as `TechTreeUI` itself if it is located inside the same hierarchy).
* **Auto-Resolution Fallbacks:** Added `Awake()` fallback resolution methods to `TechTreeUI` and `GenerationSummaryUI` to automatically locate and bind missing button and panel references at runtime, safeguarding against unassigned variables in Unity Inspector setups.
* **Active Hierarchy Validations:** Added explicit `Debug.LogError` calls inside `TechTreeUI.Open` and `GenerationSummaryUI.OnViewTechTreeClicked` that check if the activated GameObjects or panels are `activeInHierarchy`. If any parent objects are disabled (which would silently hide the UI), Unity will now throw a prominent red error to the console indicating a hierarchy setup issue.

#### 24. Vegetation Biomass Generation Rule
* **Spawning Costs Removed:** Spawning grass or plants no longer checks for nor consumes the player's Biomass. This allows vegetation to grow dynamically without any initial resource blocks.
* **Biomass Generation Added:** Every active plant and grass blade in the scene now acts as a source of Biomass. In `VegetationManager.ProcessBalanceTick()`, active vegetation objects generate Biomass over time based on the `biomassCostPerPlant` and `biomassCostPerGrass` values, contributing directly to the player's global Biomass pool.

#### 22. Blueprint Drafting Deck Cards (Complete Pool)
The following is the exhaustive database of all **29 cards** in the game's blueprint deck:

##### Default Blueprint & Resource Cards
1. **Solar Array Project**
   * *Type:* Unlock Building
   * *Description:* Unlocks the ability to construct Solar Panels to generate massive clean grid Power.
   * *Target:* `Buildings/SolarPanel/SolarPanel`
2. **Atmosphere Processor**
   * *Type:* Unlock Building
   * *Description:* Unlocks the Oxygen Processor to extract carbon dioxide and enrich colony atmosphere.
   * *Target:* `Buildings/Oxygen Processor/Oxygen Processor`
3. **Modular Habitat Dome**
   * *Type:* Unlock Building
   * *Description:* Unlocks the Colonist Habitat building, increasing your maximum colony housing capacity.
   * *Target:* `Buildings/Habitat/Habitat`
4. **Heavy Alloys Shipment**
   * *Type:* Resource Shipment
   * *Description:* Receive an immediate cargo supply shipment of +400 Materials for base construction.
   * *Stats:* +400 Materials instantly.
5. **Bio-Dome Culture Serum**
   * *Type:* Resource Shipment
   * *Description:* Deploy advanced fertilizer cultures to instantly receive +150 Biomass.
   * *Stats:* +150 Biomass instantly.
6. **Mining Drone**
   * *Type:* Spawn Unit
   * *Description:* Fabricate and deploy an additional fully functioning Mining Drone immediately at your command center.
7. 8. **High-Power Induction Drills**
   * *Type:* Passive Buff
   * *Description:* Upgrade mining tools. All mining droids gather minerals and deposits +30% faster permanently.
   * *Stats:* 1.3x Gather Speed multiplier.
9. **Photovoltaic Tuning Upgrades**
   * *Type:* Passive Buff
   * *Description:* Install resonance tuners onto solar collectors. All Solar Panels generate +20% grid Power permanently.
   * *Stats:* 1.2x Power Gen multiplier.

##### Themed Building Unlock Cards (Procedural)
*All themed cards cost Minerals (Materials) to construct. Some require specific climate thresholds or geological features to be built.*

* **Utility & Mining Deck**
  10. **Basalt Strip-Mine**
      * *Cost:* 120 Materials | *Upkeep:* 2 Power
      * *Description:* Unlocks the Basalt Strip-Mine building, providing solid planetary foundations. Includes active "Strip-Mine Basalt" ability (+150 Materials).
  11. **Deep-Core Mining Laser**
      * *Cost:* 200 Materials | *Upkeep:* 5 Power
      * *Gate:* Temperature $\ge -40^{\circ}\text{C}$
      * *Description:* Unlocks active fire mining laser. Includes active "Fire Mining Laser" ability (+200 Materials, +2.0°C Temp).
  12. **Water Ice Aquifer**
      * *Cost:* 150 Materials | *Upkeep:* 3 Power | *Biomass Gen:* +5
      * *Gate:* Temperature $\ge -20^{\circ}\text{C}$
      * *Description:* Extracts subterranean ice reservoirs.
  13. **Geothermal Generator**
      * *Cost:* 250 Materials | *Power Gen:* +15 Power
      * *Gate:* Temperature $\ge -10^{\circ}\text{C}$
      * *Description:* Converts thermal vents into clean energy.
  14. **Lava Tube Outpost**
      * *Cost:* 180 Materials | *Upkeep:* 2 Power | *Housing:* +12
      * *Gate:* Lava Tube sector feature
      * *Description:* Establishes a shelter inside a protective lava tube feature.

* **Urban & Residential Deck**
  15. **Inflatable Bio-Dome**
      * *Cost:* 100 Materials | *Upkeep:* 1 Power | *Housing:* +10
      * *Gate:* Atmosphere $\ge 0.05\text{ atm}$
      * *Description:* Creates modular colonist housing.
  16. **Urban Green Commons**
      * *Cost:* 150 Materials | *Upkeep:* 2 Power | *Housing:* +15
      * *Gate:* Atmosphere $\ge 0.15\text{ atm}$
      * *Description:* Fosters colonist happiness and health.
  17. **Solar Greenhouse**
      * *Cost:* 140 Materials | *Upkeep:* 2 Power | *Biomass Gen:* +2
      * *Gate:* Atmosphere $\ge 0.20\text{ atm}$
      * *Description:* Integrates vegetation modules into habitats.
  18. **Subterranean Apartment Block**
      * *Cost:* 300 Materials | *Upkeep:* 4 Power | *Housing:* +30
      * *Gate:* Lava Tube sector feature
      * *Description:* Deep housing inside a lava tube, shielded from cosmic radiation.
  19. **Sector Command Center**
      * *Cost:* 400 Materials | *Upkeep:* 5 Power | *Housing:* +20
      * *Gate:* Fault Line sector feature
      * *Description:* Coordinates regional supply lines from a fault line feature.

* **Science & Terraforming Deck**
  20. **GHG Factory**
      * *Cost:* 150 Materials | *Upkeep:* 4 Power
      * *Description:* Vaporizes chemicals to heat the planet. Includes active "Release GHG" ability (+1.0°C Temp, +0.02 atm Atmos).
  21. **Atmospheric Condenser**
      * *Cost:* 180 Materials | *Upkeep:* 3 Power
      * *Gate:* Atmosphere $\ge 0.05\text{ atm}$
      * *Description:* Extracts gases from thin air. Includes active "Condense Atmosphere" ability (+0.5% Oxygen).
  22. **Carbon Dioxide Import Laser**
      * *Cost:* 250 Materials | *Upkeep:* 6 Power
      * *Gate:* Atmosphere $\ge 0.10\text{ atm}$
      * *Description:* Attracts cometary ice to enrich atmosphere. Includes active "Import CO2" ability (+0.05 atm Atmos).
  23. **Subglacial Water Extractor**
      * *Cost:* 220 Materials | *Upkeep:* 4 Power | *Biomass Gen:* +4
      * *Gate:* Water Deposit sector feature
      * *Description:* Drills deep into subglacial water deposits to pump biomass media.
  24. **Magnetic Shield Generator**
      * *Cost:* 350 Materials | *Power Gen:* +25 Power
      * *Gate:* Fault Line sector feature
      * *Description:* Protects regional grids from solar wind from an elevated fault line.

* **Ecological & Biosphere Deck**
  25. **Methanogenic Microbe Spreader**
      * *Cost:* 130 Materials | *Upkeep:* 2 Power
      * *Gate:* Temperature $\ge -30^{\circ}\text{C}$ & Atmosphere $\ge 0.05\text{ atm}$
      * *Description:* Spreads methane-producing microbes. Includes active "Spread Microbes" ability (+1.5°C Temp, +30 Biomass).
  26. **Lichen Nursery**
      * *Cost:* 140 Materials | *Upkeep:* 2 Power | *Biomass Gen:* +3
      * *Gate:* Temperature $\ge -25^{\circ}\text{C}$ & Atmosphere $\ge 0.10\text{ atm}$
      * *Description:* Cultivates rock-decomposing lichens.
  27. **Genetically Modified Algae Spreader**
      * *Cost:* 210 Materials | *Upkeep:* 3 Power
      * *Gate:* Temperature $\ge -15^{\circ}\text{C}$ & Atmosphere $\ge 0.15\text{ atm}$ & Oxygen $\ge 1.0\%$
      * *Description:* Sows oxygen-producing algae pools. Includes active "Spread Algae" ability (+2.0% Oxygen, +60 Biomass).
  28. **Greenery Dome**
      * *Cost:* 280 Materials | *Upkeep:* 4 Power | *Biomass Gen:* +6
      * *Gate:* Temperature $\ge -10^{\circ}\text{C}$ & Atmosphere $\ge 0.20\text{ atm}$ & Oxygen $\ge 2.0\%$
      * *Description:* Advanced glass canopy housing local flora.
   29. **Biosphere Center**
       *Cost:* 500 Materials | *Upkeep:* 6 Power | *Biomass Gen:* +10
       *Gate:* Water Deposit sector feature
       *Description:* Coordinates global ecological cycles from a protected water deposit.

##### Exploration & Scouting Cards (ScoutingCardSO)
*These cards are created at runtime by [`BlueprintDraftUI.InitializeDefaultPool()`](Assets/Scripts/UI/Containers/BlueprintDraftUI.cs:424) as `ScoutingCardSO` instances. Their purpose is to gate sector expansion behind player-chosen cards rather than automation. The [`ScoutingCardSO`](Assets/Scripts/Player/BlueprintCard.cs:387) class has its own `IsGateMet()` that hides Orbital Scan/Pipeline Boost/Survey Drone when no locked sectors remain.*

30. **Orbital Scan**
   * *Type:* Exploration
   * *Description:* Deploy satellites to instantly explore and unlock the next sector for colonization. Calls [`ExplorationManager.InstantExplore()`](Assets/Scripts/Environment/ExplorationManager.cs:73) which marks the next locked sector as explored and then unlocks it.
31. **Pipeline Boost**
   * *Type:* Exploration
   * *Description:* Boost exploration pipeline pressure, doubling sector scouting speed for 60 seconds. Calls [`ExplorationManager.BoostExplorationSpeed(2f, 60f)`](Assets/Scripts/Environment/ExplorationManager.cs:116).
32. **Survey Drone**
   * *Type:* Exploration
   * *Description:* Deploy an automated survey drone to scout ahead and unlock the next sector. Calls [`ExplorationManager.DeploySurveyDrone()`](Assets/Scripts/Environment/ExplorationManager.cs:128) which delegates to `InstantExplore()`.
33. **Emergency Caches**
   * *Type:* Materials (always available safety net)
   * *Description:* Scavenge emergency supply caches for +300 Materials. Always available regardless of sector state.

*All four cards were added on 2026-07-04 to fix a progression deadlock where no exploration cards existed in the deck. Previously, [`UnlockNextSector()`](Assets/Scripts/Environment/SectorManager.cs:213) required sectors to be explored first but there was no way to explore them, and [`BuildBuildingCommand.IsLocked()`](Assets/Scripts/Commands/BuildBuildingCommand.cs:377) did not check for available sectors, allowing the Command Post to be built in an already-occupied sector.*

#### 25. Building Operational State & Power Upkeep Fixes
* **Battery Removal & Solar Array Spawning on Command Post:** Removed the dynamic `BatteryNode` component addition on the Command Post in `BaseBuilding.InitializeIfNeeded()`. To prevent new Command Posts from immediately shutting down due to zero starting grid power, player-owned Command Posts now automatically spawn and connect a 4-Solar-Panel array (generating 20 total power) upon completing construction. This ensures sustainable, permanent power from completion.
* **Operational-Aware Decay Protection:** Updated `GlobalDecayManager.DecayLoop()` to only grant decay protection to buildings that are both completed and operational (`IsOperating` is true). Unpowered or shut-down life support buildings (like the Command Post or Oxygen Processors) will not prevent decay for themselves or nearby structures.
* **Operational-Aware Game Over Checks:** Updated `GameOverManager.AnyLifeSupportNodesRemain()` to verify `b.IsOperating` before counting a completed life support building. An unpowered Command Post or Oxygen Processor will not count as a functioning life support node, ensuring the player faces loss if their colony collapses and cannot be rebuilt.
* **Operational-Aware Vegetation Growth:** Updated `VegetationManager`'s growth loop and balance tick to check if the building associated with a `LifeSupportNode` is operational. If a node's building is not operational, vegetation spawning is blocked around it, and existing vegetation in that zone decays at an accelerated, orphaned rate.
* **Dynamic Selection Indicator Generation:** Added fallbacks in `BaseBuilding.InitializeIfNeeded()` to dynamically construct and configure a `Selection Indicator` quad if it is missing from a building's prefab (such as the Solar Panel). This ensures any selected building receives a properly sized, styled selection indicator ring even when spawned procedurally.

#### 26. Prefab Variant Architecture & Automated Conversion
* **Building Architecture Rule:** All building prefabs in the project should ideally be created as **Prefab Variants** of `Assets/Units/Buildings/BaseBuilding.prefab`. This ensures they automatically inherit common sub-hierarchies, such as the `Selection Indicator` child GameObject, the `Vision` child GameObject, the `BaseBuilding` behavior component, and their proper default configurations.
* **Automated Standalone-to-Variant Conversion Script:** Added an editor utility script `Assets/Scripts/Editor/ConvertPrefabsToVariants.cs` that automatically converts standalone building prefabs (like `Command Post` and `Solar Panel`) to `BaseBuilding` Prefab Variants on Unity startup/recompile. 
* **Manual Prefab Variant Creation Guide:**
  1. In the Project tab, right-click `BaseBuilding.prefab` and select **Create -> Prefab Variant**.
  2. Rename the variant to the new building name (e.g., `Solar Panel`).
  3. Drag the visual mesh/model FBX or GLB of the new building into the variant's hierarchy.
  4. Select the inherited `Selection Indicator` child GameObject and scale it (typically `10` for normal buildings, `15` for Command Posts) to match the visual footprint of the building.
  5. Drag the `Selection Indicator` child into the `Selection Indicator` field on the `BaseBuilding` script component on the root of the prefab.
  6. Fill in the values for `UnitSO`, custom commands, health, etc. in the inspector.

#### 27. Ghost Placement Prefab Rule - CRITICAL: Do Not Overwrite GhostPrefab with Fallbacks
* **Architectural Principle:** Each `BuildBuildingCommand` ScriptableObject asset (stored in `Assets/Units/Commands/` and its subfolders) has **its own correctly-assigned `GhostPrefab`** pointing to the building's dedicated ghost variant prefab (e.g., `Command Post Ghost Variant.prefab`, `SolarPanel Ghost Variant.prefab`). These were set up correctly from the start and must be **trusted as the single source of truth** for ghost previews during placement.
* **Anti-Pattern - DO NOT DO:** Never write code that:
  * Searches for a "first available" `GhostPrefab` from a different command asset and copies it to others (e.g., `FindFirstTemplateCommand()` pattern). This is what caused the "all ghosts show solar panel" bug.
  * Overwrites `GhostPrefab` with `buildingSO.Prefab` (the solid building model) as a "defensive fallback" — this causes the solid model to appear instead of the ghost variant.
  * Copies `GhostPrefab` alongside restrictions or other metadata from a shared template, since each building has a unique ghost variant.
* **Correct Pattern:** Assign `GhostPrefab` on each `BuildBuildingCommand` individually from its own `BuildingSO` asset. If any fallback behavior is needed, use `buildingSO.Prefab` (solid model) **only** when `GhostPrefab` is null, and couple it with the ghost placement material swap so the result still looks ghosted.
* **Technical Debt Warning:** Defensive coding patterns that mutate correct ScriptableObject assignments at runtime introduce subtle, hard-to-debug visual bugs. The `GhostPrefab` field on each command asset is the authoritative source and must not be overwritten by shared template logic.
* **Core Philosophy — Trust the Variants, Fail Loudly:** The project is architected around prefab variants (e.g., `BaseBuilding.prefab` → `Command Post.prefab` → `Command Post Ghost Variant.prefab`). These variants and their ScriptableObject assets are the **single source of truth**. Code must NOT write defensive fallbacks that search for "any available" data and silently copy it to everything — this is what caused the "all ghosts show solar panel" bug. **Instead, code should throw exceptions when expected references are missing.** A missing ghost prefab on a command asset is a setup error that should crash in the editor, not a runtime condition that silently degrades visuals. Every defensive fallback that papers over missing data creates technical debt and masks real configuration problems.

#### 28. UI Design Philosophy — Universal Bottom Bar as ActionsUI Mirror
* **Architectural Origin:** The selection-based `ActionsUI` (from the original `RuntimeUI`/`ActionsUI` container pattern) worked well and was the trusted UI pattern. Actions were shown based on the currently selected unit or building's `AvailableCommands`.
* **Bottom Bar Evolution:** The `BottomBarActionsUI` was created to be a near **1:1 duplicate** of `ActionsUI` but made **persistent and universal** — so the entire game could be played from the bottom bar without needing to select units first. Actions should appear in the bottom bar automatically as long as they are **viable** (unlocked, affordable, and not restricted).
* **Key Constraint — One Source of Truth:** The bottom bar and the selection-based ActionsUI should ultimately reflect the **same set of viable commands**. The bottom bar is not a separate system — it is the ActionsUI made globally available. If a command works from the selection-based UI, it should also work identically from the bottom bar, and vice versa.
* **Avoiding Duplicate Command Sources:** Both UIs should ideally draw from the **same command instances** rather than creating independent dynamic copies. When they create separate instances (as happens with `BuildBuildingCommand` for buildings), all properties including `GhostPrefab` must be populated identically on both sides. Inconsistencies between the two sources (e.g., GhostPrefab set in one but not the other) are bugs.

#### 29. Card Hand = Bottom Action Bar
* **The bottom action bar IS the player's hand.** There is no separate hand panel. If a card is not visible in the bottom bar, it is not in the player's hand.
* **Hand size:** 10 cards maximum. The bottom bar has 12 wired slots, so up to 10 cards are displayed.
* **Guaranteed opening seeds:** Command Post and Solar Panel are always seeded into the opening hand (Mining Drone is added if already playable). Exact name matches: `"Command Post"`, `"Solar Panel"`, `cardName == "Mining Drone"`.
* **Play-and-draw (FIFO):** Playing a card removes it from the hand, appends it to the **back** of discard, and fills empty slots from the **front** of the draw pile. See **§37**.
* **Deck population:** `BlueprintDraftUI.InitializeDefaultPool()` loads `Resources/Cards` (+ runtime Deploy Engineer) into `CardDeckController.masterDeck`, then calls `RebuildDeck()`.
* **Initialization order:**
  1. `CardDeckController.AutoSpawn()` — empty deck.
  2. `BlueprintDraftUI.Awake()` → `InitializeDefaultPool()` → `RebuildDeck()` (stable FIFO order, seed starters, `FillHand`).
  3. `CardDeckController.Start()` is empty on purpose.
* **Sector-goal colors:** Hand buttons and Active Objectives share colors only for sector-completion terraforming — see **§38**.

#### 30. RTS Memory Cleaner & Monitor Tool
* **Purpose:** A custom Editor Window utility (`MemoryCleanerWindow.cs` inside `Assets/Scripts/Editor/`) designed to help developers monitor memory usage, warn about leaks, and clean dangling assets directly inside the Unity Editor during active development.
* **Key Features:**
  * **Managed Memory Tracking:** Displays real-time C# managed memory usage (in MB).
  * **Memory Warning Banner:** Automatically scans and alerts the developer if it detects runtime-instantiated materials (`(Instance)`) or temporary clones/ghost objects (`(Clone)` or `Ghost`) that survived a Play Mode session into Edit Mode.
  * **Force Garbage Collection:** Invokes `GC.Collect()` and `GC.WaitForPendingFinalizers()` to immediately flush unused managed objects.
  * **Asset Unloading:** Invokes `Resources.UnloadUnusedAssets()` and `EditorUtility.UnloadUnusedAssetsImmediate()` to reclaim GPU and RAM from unreferenced texture, model, and material assets.
  * **Playmode Leak Pruning:** Automatically identifies and cleans up survived playmode clones or dangling runtime materials with a single click.
  * **Menu Path:** Accessible via **Tools ➔ RTS Memory Cleaner** in the Unity Editor menu bar.

#### 31. PlayerInput & ActionsUI Event-Driven Architecture
**Core Principle:** `PlayerInput` does NOT select what action buttons appear in the UI action container. There is a strict separation of concerns between UI display and input execution.

##### 1. ActionsUI Determines What Buttons to Show
[`ActionsUI.RefreshButtons()`](Scripts/UI/Containers/ActionsUI.cs:60) directly queries the **selected unit's `AvailableCommands`** property (from [`AbstractCommandable.AvailableCommands`](Scripts/Units/AbstractCommandable.cs:20)):
- Takes the first selected unit's `AvailableCommands` as the base set.
- Filters them through `command.IsAvailable()` to check costs/restrictions/gates.
- Intersects with `AvailableCommands` from all other selected units (only shows commands all units share).
- Assigns each surviving command to a slot-based UI button using `command.Slot`.

##### 2. ActionsUI Raises `CommandSelectedEvent` on Click
When a UI button is clicked, [`ActionsUI.HandleClick()`](Scripts/UI/Containers/ActionsUI.cs:98) raises a [`CommandSelectedEvent`](Scripts/Events/CommandSelectedEvent.cs) via the event bus:
```csharp
Bus<CommandSelectedEvent>.Raise(Owner.Player1, new CommandSelectedEvent(action));
```

##### 3. PlayerInput Listens for and Executes the Command
[`PlayerInput.Awake()`](Scripts/Player/PlayerInput.cs:65) subscribes to `CommandSelectedEvent`. [`PlayerInput.HandleActionSelected()`](Scripts/Player/PlayerInput.cs:93) sets `activeCommand` and either:
- **Executes immediately** if `command.RequiresClickToActivate == false` (e.g., training a unit).
- **Shows a placement ghost** if `command.GhostPrefab != null` (e.g., building placement).

##### 4. Left-Click & Right-Click Execution
- **Left-click** ([`HandleLeftClick()`](Scripts/Player/PlayerInput.cs:283)): If `activeCommand` is set, calls [`ActivateAction()`](Scripts/Player/PlayerInput.cs:303) which iterates selected units and calls `activeCommand.Handle(context)`.
- **Right-click** ([`HandleRightClick()`](Scripts/Player/PlayerInput.cs:223)): Iterates selected units via [`GetAvailableCommands()`](Scripts/Player/PlayerInput.cs:261), finds the first `CanHandle()` == true, and executes `command.Handle(context)`.

##### 5. RuntimeUI Orchestrates UI Refresh
[`RuntimeUI`](Scripts/UI/RuntimeUI.cs) subscribes to selection/death/spawn/upgrade events and calls `actionsUI.EnableFor(selectedUnits)` to refresh buttons whenever selection or game state changes.

##### 6. The Complete Flow
```
User clicks UI button
  -> ActionsUI raises CommandSelectedEvent
    -> PlayerInput.HandleActionSelected() receives it
      -> Sets activeCommand, shows ghost, or executes instantly

User left-clicks on map (with active command)
  -> PlayerInput.HandleLeftClick()
    -> ActivateAction() calls activeCommand.Handle(context)

User right-clicks on map (with units selected)
  -> PlayerInput.HandleRightClick()
    -> Iterates GetAvailableCommands() for each selected unit
    -> Finds first CanHandle() == true
    -> Calls command.Handle(context)
```

##### 7. Architectural Rule for AI Agents
- **DO NOT** make `PlayerInput` control which UI buttons appear. It only reacts to `CommandSelectedEvent`.
- **DO NOT** modify `ActionsUI` to ask `PlayerInput` what to show. Query the selected unit's `AvailableCommands` instead.
- **To add a new command/action:** Create a `BaseCommand` ScriptableObject, assign it to the unit's `AvailableCommands` array, and wire its `Slot` number to an available UI slot.
- **To add a new UI action button slot:** Increase the `actionButtons` array size on the `ActionsUI` component.

#### 32. Unit Command UI Restoration & Serialization Fix
* **The Problem:** In a previous refactor, the auto-property `AvailableCommands` on `AbstractCommandable` was changed to a standard property backed by a private field named `_availableCommands`. This broke loading unit commands from all unit prefab assets (e.g., `Mining Drone.prefab`, `Construction Drone.prefab`, `Probe.prefab`) because Unity's serialized data for existing prefabs saved the list under the compiler's auto-generated backing field name `<AvailableCommands>k__BackingField`. Consequently, all units loaded with 0 available commands, leaving the `ActionsUI` panel blank.
* **The Solution:** Added `[UnityEngine.Serialization.FormerlySerializedAs("<AvailableCommands>k__BackingField")]` to the `_availableCommands` field inside [`AbstractCommandable.cs`](Assets/Scripts/Units/AbstractCommandable.cs). This instructs Unity's deserializer to map the legacy auto-property backing field name directly into `_availableCommands`.
* **UI Activation:** Updated [`RuntimeUI.cs`](Assets/Scripts/UI/RuntimeUI.cs) to call `actionsUI.gameObject.SetActive(true)` unconditionally when any unit selection is active (`selectedUnits.Count > 0`). This restores selection-driven command button visibility in the Actions container (rather than restricting them exclusively to the `GlobalCommander`).

#### 33. Command Post Placement, Fog of War, and Resource-Crushing Rules
* **The Problem:** 
  1. Snapping player-placed Command Post buildings to the exact sector center forced them to spawn directly on top of resource nodes and debris, crushing them instantly.
  2. Because Sector 0's occupancy logic didn't account for the pre-placed starting base (`GlobalCommander`), Sector 0 was marked as unoccupied at startup. This made clicking "Build Command Post" automatically snap to Sector 0's center, instantly dropping a new base over starting resources.
  3. `BuildBuildingCommand.Handle()` instantly completed the first Command Post from orbit, clearing the fog of war and removing resources on placement click.
* **The Solution:**
  1. Disabled snapping player-placed command buildings to sector centers in [`BuildBuildingCommand.cs`](Assets/Scripts/Commands/BuildBuildingCommand.cs). They can now be placed freely in unoccupied areas.
  2. Modified [`SectorManager.cs`](Assets/Scripts/Environment/SectorManager.cs) to detect the `GlobalCommander` as an occupying structure, so Sector 0 is properly marked occupied on start, directing future Command Post placement camera snaps to Sector 1.
  3. Updated [`BuildBuildingCommand.cs`](Assets/Scripts/Commands/BuildBuildingCommand.cs) to only treat a Command Post as `isFirstCommandPost` (orbital drop) if NO `GlobalCommander` exists in the scene. 
  4. Updated [`BaseBuilding.cs`](Assets/Scripts/Units/BaseBuilding.cs) to disable `VisionTransform` (fog of war reveal) during construction/ghost states, enabling it only upon calling `CompleteConstruction()`.
  5. Moved the resource-crushing `OverlapBox` logic from `BuildBuildingCommand.Handle()` to `BaseBuilding.CompleteConstruction()`, ensuring underlying resource nodes are only cleared when construction is finished.
  6. Restructured `IsLocked()` in [`BuildBuildingCommand.cs`](Assets/Scripts/Commands/BuildBuildingCommand.cs) to allow the player to place their first Command Post unconditionally (bypassing unoccupied sector checks), and preventing standard buildings (Solar Panels, etc.) from being incorrectly locked by Command Post sector restrictions.
  7. Updated `AllRestrictionsPass()` in [`BuildBuildingCommand.cs`](Assets/Scripts/Commands/BuildBuildingCommand.cs) to ignore the collider of the pre-placed `GlobalCommander` (Universal Command Center) starting base, so placing a player Command Post nearby in Sector 0 is not blocked.
  8. Appended `"Ghost_"` prefix to the name of instantiated ghosts in [`PlayerInput.cs`](Assets/Scripts/Player/PlayerInput.cs) and filtered out buildings containing `"Ghost"` from count and sector occupancy evaluations in [`BuildBuildingCommand.cs`](Assets/Scripts/Commands/BuildBuildingCommand.cs), preventing placement ghosts from locking card selections.
  9. Implemented runtime glowing cyan sci-fi borders around all active/unlocked sectors using LineRenderers in [`SectorManager.cs`](Assets/Scripts/Environment/SectorManager.cs). The borders automatically conform to the height of the terrain.
  10. Updated `CenterCameraOnMap()` in [`PlayerInput.cs`](Assets/Scripts/Player/PlayerInput.cs) to center the camera on the starting sector (Sector 0) center instead of the mathematical center of the entire map.
  11. Fixed a bug in [`NaturalEventManager.cs`](Assets/Scripts/Environment/NaturalEventManager.cs) where pre-placed managers in the scene were never starting their wave routines. `OnFirstBuildingSpawned` now properly calls `BeginAssault()` on the existing manager rather than silently returning.
  12. Fixed a bug in [`BuildBuildingAction.cs`](Assets/Scripts/Behavior/BuildBuildingAction.cs) where humanoid workers/builders controlled by the Behavior Tree would finish building construction but never call `CompleteConstruction()`, leaving the building permanently under construction in the "Building" state. OnEnd now calls `CompleteConstruction()` upon successful completion.
  13. Exposed fallback meteor variables (Damage Radius, Damage Amount, Max Health, Fall Height, and Fall Speed) to the inspector of [`NaturalEventManager.cs`](Assets/Scripts/Environment/NaturalEventManager.cs) so developers can easily tune meteor strike values and curate game flow without code modifications.
  14. Implemented a dynamic card-driven hazard registry system:
      * Added `hazardEventPrefab` field and property to `BlueprintCardSO` (in [`BlueprintCardSO.cs`](Assets/Scripts/Player/BlueprintCardSO.cs)), enabling all blueprint cards to store negative hazards/blowouts in the inspector.
      * Updated [`CardDeckController.cs`](Assets/Scripts/Player/CardDeckController.cs) to automatically register a played card's hazard prefab to [`NaturalEventManager`](Assets/Scripts/Environment/NaturalEventManager.cs) when played.
      * Modified [`NaturalEventManager.cs`](Assets/Scripts/Environment/NaturalEventManager.cs) to accumulate registered card hazards at runtime and merge them into the wave spawning pool, dynamically scaling threats based on what cards the player plays.
  15. Split the grouped classes in `BlueprintCard.cs` into individual C# script files (e.g. [`BlueprintCardSO.cs`](Assets/Scripts/Player/BlueprintCardSO.cs), [`UnlockBuildingCardSO.cs`](Assets/Scripts/Player/UnlockBuildingCardSO.cs), etc.) inside `Assets/Scripts/Player/`. This allows Unity to correctly serialize and link subclasses as persistent `.asset` files in the editor.
  16. Migrated all 32 code-generated cards into persistent `.asset` files inside the `Assets/Resources/Cards/` directory, resolving the code-generation limits.
  17. Refactored [`BlueprintDraftUI.cs`](Assets/Scripts/UI/Containers/BlueprintDraftUI.cs) to load all card blueprints dynamically from persistent assets using `Resources.LoadAll<BlueprintCardSO>("Cards")`, simplifying and cleaning up the codebase.

#### 34. Martian Colonist Survival & Pressurized Tubes System
* **Colony Commander Vitals & Snapping**: Added the `MartianColonist` VIP unit. It floats a world-space billboard Canvas badge showing live oxygen and starvation vitals. When garrisoned inside a building, the commander's C# `transform.position` snaps to the center of the building, and the vitals badge offset shifts upward (`Y = 6.0` world units) to float over the building's roof mesh.
* **Pressurized Tubes as RTS Structures**: visual power/resource connections are styled as thick pressurized tubes (translucent blue for Inflatable, solid metallic gray for Solid). The connection GameObjects now host the [`PressurizedTube.cs`](Assets/Scripts/Environment/PressurizedTube.cs) component which inherits from `AbstractCommandable` (meaning it has health, can take damage, and is repaired by drones). It spawns and aligns a `CapsuleCollider` on the `Interactable` layer, allowing it to be damaged by meteor explosions and targeted for repairs.
* **Automated Tube Transit**: In [`MartianColonist.cs`](Assets/Scripts/Units/MartianColonist.cs), if the commander is commanded to move to another building and a pressurized tube connection chain exists, the unit enters "Tube Transit" mode. The `NavMeshAgent` and colliders are temporarily disabled, and they are smoothly translated along the 10 curved path points of the tube. This guarantees zero oxygen depletion during transit. If no connection exists, they must walk outside on the NavMesh terrain exposed to the vacuum.
* **Food & Starvation Loop**: Added `Food` tracking to [`Supplies.cs`](Assets/Scripts/Player/Supplies.cs) and displayed it next to Biomass on the HUD. The commander consumes `1.0` food every 15 seconds. If food is empty, they enter a Starving state, and take `10` damage every 5 seconds. Biomass-generating buildings (like the Solar Greenhouse) passively produce food at 50% of the biomass rate. Garrisoning the commander inside the Greenhouse grants a **50% boost** to its food generation speed.
* **Colony Engineers (Inspecting & Repairing)**: Created [`ColonyEngineer.cs`](Assets/Scripts/Units/ColonyEngineer.cs) representing micro-scale engineers who roam the base automatically. They random-wander between completed buildings (riding inside pressurized tubes when connections exist), entering and "inspecting" them for random intervals. If any building or tube takes damage, they rush to the site and repair it back to full health. They consume food just like the Colony Commander. Added the dynamic card [`TrainEngineerCardSO.cs`](Assets/Scripts/Player/TrainEngineerCardSO.cs) ("Deploy Engineer") to the draft deck to spawn them at the Command Post.
* **Disable Background Behavior Graphs**: Unity 6 models instantiated from drone prefabs inherit a background `BehaviorGraphAgent` component executing idle/patrol behaviors. To prevent this from overriding and cancelling custom C# movement paths, both [`MartianColonist.cs`](Assets/Scripts/Units/MartianColonist.cs) and [`ColonyEngineer.cs`](Assets/Scripts/Units/ColonyEngineer.cs) explicitly call `GetComponent<BehaviorGraphAgent>().enabled = false;` on startup.
* **Path-Jitter & Travel Locks**: Resolved unit freezing and destination spamming by modifying wander routines to check `agent.pathPending || agent.hasPath` before updating paths, preventing the NavMesh agent from continually recalculating and locking in place. Resets shelter wait states (`isWaitingInBuilding = false`) properly on building exit.
* **NavMesh Proximity & Spawning Security**: Added `NavMesh.SamplePosition()` queries to all spawn and teleportation routes in [`TrainEngineerCardSO.cs`](Assets/Scripts/Player/TrainEngineerCardSO.cs) and exit procedures. Spawns fall back to the pre-placed `GlobalCommander` (Universal Command Center) starting base if no player-built `BaseBuilding` Command Posts are active, ensuring units never spawn at `Vector3.zero` or off the walkable NavMesh.
* **Building Entry Bounds**: Increased the completed building entry detection radius from `2.2` meters to `6.0` meters. This allows pathing colonists and engineers to successfully trigger the `EnterBuilding()` state when reaching the outer edge of large obstacle colliders (such as the Solar Panels), instead of walking into their perimeter forever.
* **Tube Proximity Distance-Guards**: Added a distance threshold of `4.5` meters from both building centers to the tube proximity check. This prevents units standing outside next to structures from registering as "inside a tube" and displaying false-positive tube transit badges.
* **Physical Wave Colonists**: Modified [`ColonistManager.cs`](Assets/Scripts/Player/ColonistManager.cs) to instantiate physical human game units in the scene when a new wave of colonists arrives. They land at the completed Spaceport (or fallback bases) and automatically run the `MartianColonist` logic, showing `👩‍🚀 COL` badges to indicate standard colonists.

#### 35. Core Game Flow (The One Rule: Actions Drive Time)
* **The One Rule**: There is no "End Turn" button. The turn advances automatically after the player stops acting.
* **Player Actions**: (Any of these resets a short idle timer)
  * **Deploy**: Play a card from hand onto a valid map tile.
  * **Explore**: Reveal an adjacent undiscovered tile (costs 1 energy).
  * **Repair**: Fix a degraded structure (costs 2 materials).
* **Flow**:
  * PLAYER ACTS → idle timer resets (≈2 seconds)
  * PLAYER ACTS AGAIN → timer resets again
  * PLAYER STOPS → timer expires → TURN RESOLVES
* **Turn Resolution (sequential)**:
  1. **Upkeep**: Each deployed structure drains energy. Shortfall → random structures degrade.
  2. **Recovery**: Disabled structures tick down toward reactivation.
  3. **Income**: Gain base resources (energy, materials, research) + bonuses from upgrades/deposits.
  4. **Threats**: Random chance (scales with turn number × planet danger). Damages resources or structures.
  5. **Draw**: `FillHand()` — FIFO draw of playable cards (unplayable cards go to the back of discard). Hand is **not** discarded wholesale every turn in the current `GameFlowPhaseController` path (it only calls `FillHand`).
  6. **Events**: Legacy design mentioned Discovery Draft / choice events; draft overlays are currently disabled.
  7. **Milestones**: If terraform progress crosses a threshold, end generation / open summary shop flow.
  8. **Win/Lose Check**: All sectors occupied + oxygen complete = victory (see `GameOverManager`).
* **Card Rules**:
  * **Play-and-draw (FIFO):** Playing a card draws the next playable card from the front of the queue — see **§37**.
  * **Climate / site gates:** Some cards locked until terraform stats / reserved pads allow `CanApply()`.
  * **Sector-goal colors:** Only sector-completion terraforming goals are color-coded — see **§38**.
* **Exploration**:
  * Map starts with fog of war, only a few tiles revealed.
  * Adjacent-to-revealed tiles are “discoverable” (marked visually).
  * Revealing a tile also makes its neighbors discoverable (frontier expands outward).
  * Some tiles have resource deposits that boost income when structures are placed on them.
* **Milestones & Upgrades**:
  * Per-sector generations: primary milestone + Temp / Atmos / Water must all reach target.
  * Hitting a generation awards Terra-Coins and opens the summary / tech shop flow.
* **Key Design Intent**:
  * **RTS pacing without real-time combat**: The idle timer creates urgency. Players batch actions quickly, then watch resolution.
  * **“Never stuck, always starving”**: Players always have something to do (emergency cards, exploration), but never have enough resources to do everything — FIFO draw ensures sector cards still cycle through.
  * **Escalating pressure**: Threats scale up, upkeep accumulates, structures degrade. The planet fights back harder as you terraform more.

#### 20. GameFlowManager & Turn Resolution Loop
* **Turn-Based System in Real-Time:** The game operates on a semi-turn-based loop managed by `GameFlowManager.cs`. The turn resolves automatically if the player is idle for a set duration (`idleTimerDuration = 2.0f`).
* **Player Action Interrupts:** Any action the player takes (Deploy, Explore, Repair, etc.) calls `PlayerActed()`, which resets the idle timer, giving the player more time to think.
* **8-Phase Resolution:** When the timer expires, the turn resolves sequentially in 8 phases using C# events:
  1. **Upkeep:** Structures drain energy.
  2. **Recovery:** Disabled structures tick down toward reactivation.
  3. **Income:** Gain base resources and bonuses.
  4. **Threats:** Random chance of damage/events.
  5. **Draw:** `FillHand()` (FIFO playable cards).
  6. **Events:** Narrative window stub / draft disabled.
  7. **Milestones:** Generation progress check.
  8. **Win/Lose Check:** Victory or defeat conditions evaluated.
* **System Integration:** This manager replaced the periodic `UpkeepRoutine` coroutines in buildings. Now, buildings and systems subscribe directly to `GameFlowManager` events (like `OnTurnUpkeep` and `OnTurnIncome`) for synchronized, deterministic economy ticks.

#### 36. Reserved Site Pads (Card → Pad → Instant Build)
Player building cards do **not** free-place on the terrain. Planet generation drops reserved pads; playing a card picks an eligible pad and `ReservedSiteBuildUtility` instantiates + `CompleteConstruction` immediately (materials cost still applies).

* **Bootstrap (Sector 0):** Inside `HexGridManager.StartingAreaRevealRadius` (~15), planet gen guarantees a Command Post pad, a solar + paired-building cluster (Oxygen Processor preview), and mineral nodes. Markers refresh on planet gen, sector unlock, and starting-area reveal.
* **Cluster rule:** Each `BuildingSiteCluster` is one solar pad + one consumer pad. `CanPlaceBuilding` requires that cluster's solar to already be occupied. The player picks **which** solar cluster to hook a building to. After build, the consumer is auto-wired to that cluster's solar via `PowerNode.ConnectTo`. **Solar never auto-connects to the Command Post** (CP keeps its temporary backup cells; shared solar watts stay for the cluster consumer). Each sector places **6** solar/paired clusters (was 3) so climate goals are not pad-starved.
* **Sector unlock colonization:** Completing a terraforming round auto-colonizes the **closest** sector still needing a Command Post (`GenerationManager.StartNextGeneration` → `SectorColonization.TryColonizeClosestSectorNeedingCommandPost`). Orbital Scan / Survey Drone can still unlock sectors early; either path reveals fog over reserved pads and **auto-places a Command Post** on its CP pad. Command Post cards stay playable whenever an unlocked sector is still unclaimed (not only during expansion).
* **Kinds:** `CommandPost`, `Solar`, `PairedBuilding` (Oxygen Processor and other non-mine/non-CP buildings), `Mine` (resource-typed). Deprecated: `Infrastructure`.
* **Mining drones:** Trained from the Command Post. While within ~14m of a damaged friendly building they passively repair it (does not cancel gathering). Explicit Repair command still uses the full repair loop.
* **Eligibility vs fog:** `GetEligibleSites` does **not** require hex fog reveal (that blocked all bootstrap builds). Marker *display* requires fog reveal **and** (for idle ghosts) Sector 0 pads inside `StartingAreaRevealRadius` only — do not show site ghosts across fog-cleared map that has not been explored as a frontier.
* **Occupancy — `BuildingSiteSlot.IsValidOccupant`:** A pad is occupied by any active real `BaseBuilding` that is **not** a site-marker preview (`BuildingSiteMarker` parent or `GhostPreview_*` name). That includes under-construction (Paused) ghosts assigned to a drone, so pads stay reserved during build. Marker preview ghosts must never lock a pad. `RegisterOccupancy` / `GetEligibleSites` / `RefreshAllMarkers` clear invalid occupants.
* **Site ghosts (`BuildingSiteMarker`):**
  * Idle preview: only Command Post / Solar / PairedBuilding pads inside Sector 0 starting reveal radius. Mine pads and out-of-radius pads show ghosts only while site-picking (`isSelectable`).
  * Instantiate under an inactive holder so `Awake`/`Start` do not complete construction.
  * Disable all `MonoBehaviour`s, force `InitializeAsGhost` (Paused), `DestroyImmediate` colliders/NavMeshObstacles.
  * Prefer `BuildingSiteGhostUtility.GetGhostPrefab`, then `building.Prefab`. Always swap to `PlacementMaterial` (`Building Ghost Placement`) **after** activation — procedural buildings (`SmokestackVisuals`) create meshes in Awake.
  * Idle opacity ~0.16–0.18; selected-for-placement slightly brighter. Never set `_Tint` to `Color.white` (that made pads look finished).
  * Click colliders are triggers, enabled only while `BuildingSiteSelectionController` is picking a pad.
* **Selection (`PlayerInput.ShouldIgnoreSelectionHit`):** Ignore site markers, `GhostPreview_*`, invalid occupants, disabled `BaseBuilding`, and `GlobalCommander` colliders. Prefer field `AbstractUnit` hits over UCC / bulky building volumes. Empty ground click still selects the UCC.
* **Do not** let site ghosts raise `BuildingSpawnEvent` or join `BaseBuilding.ActiveBuildings` as real structures. Colonists/engineers must not `EnterBuilding` on site ghosts (that disables their collider and breaks click-select).
* **Training drones:** Mining drones are trained from the **Command Post** (including multiple in queue). That is intended. The UCC is only the invisible hub for empty-click / global commands — it must not block drone raycasts.
* **Reserved-site build path:** Pad builds skip free-placement `IsLocked` / `AllRestrictionsPass` (those falsely blocked card builds: card not unlocked yet, nearby solar overlap). Normal builds spawn a construction ghost and call `Worker.ResumeBuilding`; only waived-cost colonization and the first Command Post complete instantly. Failures (including no idle drone) surface via `ExplorationManager.NotifyExplorationFailed` (on-screen). Selecting a building card frames the camera on eligible pads; a single candidate auto-builds when a drone is available.
* **Power grid allocation:** Command Post `powerUpkeep` (20) used to starve cluster consumers when solar auto-wired to CP. Allocation now powers normal consumers before CPs, and self-powered nodes (CP backup cells / battery) do not drain shared generation. `PowerGridManager` auto-spawns if missing.

#### 37. Card Deck FIFO Draw (Authoritative)
Players were soft-locked finishing sectors because always-playable "spam" cards (Emergency Caches, shipments, etc.) could dominate a **randomly shuffled** draw, while climate-gated sector cards sat in discard and never returned predictably.

**Current rules (`CardDeckController`):**
* **Stable order:** `RebuildDeck` / `InitializeDrawPile` copies `masterDeck` into `drawPile` **without shuffling**.
* **Sector-win density:** Cards whose `GetCardGoal()` is a sector-completion goal (TEMPERATURE / ATMOSPHERE / WATER / OXYGEN / POWER / POPULATION / COMMAND POST) are **added a second time** as runtime `Instantiate` clones. Support cards stay single-copy. This roughly doubles how often finishing tools appear without priority-cheat draw.
* **FIFO:** Draw from index `0` of `drawPile`. Played or skipped cards append to the **back** of `discardPile`.
* **Recycle:** When `drawPile` is empty, discard is moved onto draw **in the same order** (`RecycleDiscardIntoDraw`) — **no re-shuffle**.
* **Playable only in hand:** `FillHand` / `RefreshHand` only seat cards that pass `IsGateMet()` and `CanApply()`. Unplayable cards are discarded to the back of the queue (they will reappear later when gates are met).
* **No priority promotion:** Sector cards are **not** pulled out of order or forced into the hand. Fair cycling is the fix.
* **Opening exception:** Command Post + Solar + Mining Drone are seeded into the opening hand so the player can bootstrap (drones are required for all builds after the first CP). Everything after that is FIFO, with Solar / Mining Drone re-seated when missing (`EnsureSolarPrereqInHand` / `EnsureMiningDroneInHand`).
* **Solar infrastructure:** Whenever open solar pads exist, `EnsureSolarPrereqInHand` keeps a Solar Panel card in hand (climate/paired unlocks can sit in hand before their pad is powered; without this, they clog the hand and Solar never returns). Draw pile also includes +2 Solar infra copies beyond sector-win doubling.
* **Refresh triggers:** Builds, deaths, materials/energy/climate/oxygen/biomass/power/population changes, sector unlock, planet gen, and generation start call `RefreshHand()` so newly-playable cards can enter when they reach the front of a recycle or when unplayable cards leave the hand.

#### 38. Sector Goal Color Coding (Authoritative)
Color coding exists to match **sector-completion terraforming** cards to Active Objectives — not every card type.

**What gets colored** (`TerraformingGoalColors.IsSectorCompletionGoal`):
* Climate basics (always required each sector): **TEMPERATURE**, **ATMOSPHERE**, **WATER**
* Primary milestones: **OXYGEN**, **POWER**, **POPULATION**, **COMMAND POST** (and **TEMPERATURE** when it is the generation’s primary)

**Deprecated:** **BIOMASS** is no longer a terraforming / sector-completion goal. The resource may still exist for food/upkeep economy, but cards that only grant biomass are treated as support (`RESOURCES` / `CONSTRUCTION`) and stay neutral. Gen-1 primary milestone is **Temperature**, not Biomass.

**What stays neutral:** MATERIALS, BIOMASS, EXPLORATION / scouting, MINING, MAINTENANCE, unit spawns, Emergency Caches, discovery/salvage, passive buffs, and other support cards.

**Palette (dark HUD):** Temp=amber orange, Atmos=hot fuchsia, Water=bright blue, Oxygen=cyan, Power=gold, Population=indigo violet, Command Post=white. Top-bar climate/milestone **headers** use the same colors; values stay bright white. Objectives **values** still use green/red for met/unmet — that red is not a card color. Active Objectives shows a short color key matching hand cards.

**UI wiring:**
* `ActiveObjectivesUI` — colors the primary Goal label and Temp / Atmos / Water labels (values still green/red for met/unmet).
* `BottomBarActionsUI` + `UIActionButton` — outline tint, label tint, and a short goal badge **only** when `GetSectorGoalForCard` returns a sector goal.
* `UnlockBuildingCardSO.ClassifyBuildingGoal` / `GetCardGoal` — maps buildings (GHG Factory → TEMPERATURE, aquifer → WATER, Oxygen Processor → OXYGEN, etc.).
* Key file: `Assets/Scripts/UI/TerraformingGoalColors.cs`.

#### 39. Colony Integrity Start Gate (Authoritative)
* **`Supplies.ColonyIntegrityActive`** starts `false` each scene / `Supplies.Awake`.
* Until it is true: `CalculateIntegrity()` returns **100%**, and `GlobalDecayManager` **skips** damage ticks and integrity recalculation.
* Becomes true on the first gameplay-placed building that counts toward integrity: `BaseBuilding.CompleteConstruction` → `Supplies.BeginColonyIntegrityIfNeeded` (must be `(Clone)`, not UCC / DecayStarter / 90k+ HP hub).
* Loss checks in `GameOverManager` still wait for a real Command Post `(Clone)` before evaluating many fail states — that gate is separate from integrity drain start.

#### 40. Unity CLI & Live Editor Automation (Authoritative)
The experimental **Unity CLI** (`unity`) installs/manages Editors and, with the **Unity Pipeline** package (`com.unity.pipeline`), forwards commands to a **connected** Editor. Authoritative flag lists: `unity --help` / `unity <command> --help`. Full agent procedure: [`.zoo/rules/UnityCLI-Automation.md`](.zoo/rules/UnityCLI-Automation.md). Root [`AGENTS.md`](AGENTS.md) points agents here.

**Hard rules for this repo:**
* **Unity MCP is deprecated.** Do not use Cursor/`user-Unity` MCP, `mcp_auth` for Unity, or `unity mcp` in agent workflows. Live Editor automation is **CLI only**.
* Prefer the **already-running** Editor. Check: `unity status --format json` (expect this project `ready`).
* **Never** spawn a second Editor while the user’s Editor is open. Forbidden for agent automation here: `unity test`, `unity build`, `unity run`, and legacy `-batchmode` / headless Editor launches. This map is extremely heavy (large hex grid); a second instance will OOM-kill both Editors on Linux.
* Use live tools: `unity command` / `unity list` / `unity command eval "..."`.

**Safe connected-Editor workflow:**
```bash
unity status --format json
unity command --format json          # list Pipeline tools on the connected Editor
unity command eval "return Application.version;" --json
unity command eval "return UnityEditor.EditorApplication.isPlaying;" --json
```
Bug iteration: confirm `status` → enter Play Mode via `eval` → inspect with `eval --json` → edit C# → recompile → restart Play Mode via `eval` → verify.

**Safe CLI uses that do not open a second Editor:** `status`, `command` / `list`, `pipeline`/`pipe`, `doctor`, `logs`, `editors -i`, `auth status`, `upgrade`.

**Unsafe / deprecated while Editor is open:** Unity MCP; `test`, `build`, `run`, and usually `open` (may launch another instance). If headless tests are truly required, ask the user first with no Editor open.

**Automation output:** Prefer `--format json` / `--json`. Piped default is TSV; errors go to stderr. Project path auto-detects; override with `--project-path` or `UNITY_PROJECT_PATH` if needed.

**Scene note:** The GameObject is often named `PlanetManager`; the generating script is `PlanetGenerator`. “Planet generation” means that script rebuilding the map (Play auto-runs it; optional Editor context menu **Generate Planet (Editor)** for preview).

**How to run the local sector-win bot**
```bash
# 1) Open this project in Unity (wait until Pipeline connects)
# 2) From the repo root:
./tools/sector-win-cli.sh
```
Expect `RESULT: PASS`. Exit code 0 = pass, 2 = fail.

**CI/CD**
* Workflow: `.github/workflows/unity-editmode.yml`
  * **EditMode** job (cloud): lightweight card/goal contract tests via GameCI — needs `UNITY_LICENSE` (and usually email/password) secrets.
  * **Live bot** job: only on a self-hosted runner labeled `unity-pipeline-live` with this project open in Unity; runs `./tools/sector-win-cli.sh`.
* Do **not** run `unity test` / batchmode on a machine that already has this Editor open (OOM). Cloud CI is fine because it starts a single dedicated Editor.