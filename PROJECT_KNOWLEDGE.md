
### Terraforming Tendencies - Project Knowledge & Architecture Notes
This document serves as a persistent memory bank for AI context, detailing the core systems, recent architectural decisions, and current state of the game's economy.

#### [DEPRECATED] Hero Drone & Hybrid Control System
*Note: The Hero Drone system has been completely removed to support the new "Terraforming Mars" roguelite pivot, which requires macro-level map awareness.*
* **Architecture Changes:** `HeroDroneSpawner.cs` and `HeroDroneController.cs` have been deleted. The `useHeroControlMode` flag and camera-tethering logic in `PlayerInput.cs` have been removed.
* **Restored RTS Camera:** The game now uses a traditional Starcraft-style isometric perspective. WASD inputs once again control free-roaming camera panning across the map instead of piloting a specific unit.
* **System Cleanup:** All Hero Drone dependencies have been stripped. The `GreedyAIController` no longer needs exemptions for the drone, the `GameOverManager` no longer checks `heroDroneAlive` for recovery, and all custom UI/mechanics for manual drone vacuuming and cargo have been removed.
* **Design Goal:** This standard point-and-click RTS setup frees the player's camera to focus entirely on the new Terra-Coins engine-building economy and strategically planning the adjacency placement of Cities and Greenery.

#### 1. Automated Economy & AI (GreedyAIController)
* **Logarithmic Spending:** The AI limits its spending mathematically so it doesn't instantly bankrupt the player's treasury.
* **Strict Start Priority:** To prevent the AI from deadlocking itself, it bypasses the spending limit specifically for the *very first* Probe drone. It uses an ignoreReserve flag to forcefully queue the probe. Once the probe is queued, the AI unlocks and interleaves Construction and Mining drones normally.
* **Unit Assignment:** The AI dynamically scans for GatherableSupplies and automatically assigns idle Mining Drones. It explicitly targets supplies that are children of the PlanetGenerator to avoid mining invalid debris.
* **Expansion Priority:** When a new Command Post is established via the EnergyPipelineManager, it uses BuildPriorityUnlockable to queue a starter Probe. Both GreedyAIController and AIController explicitly check BaseBuilding.IsFirstInQueueProbe() to avoid filling the queue before this starter unit is registered.

#### 2. Colony Expansion & Mobile Forge (FoundryCrawler & EnergyPipelineManager)
* **Pipeline Logistics:** The EnergyPipelineManager drives the expansion of the colony. It spawns the FoundryCrawler which slowly crawls along the pipeline path (movementSpeed = 0.05f).
* **Crawler Fuel Hoppers:** The crawler requires Regolith and Iron to move. It has internal hoppers (default reset: 500 Regolith, 200 Iron, with a max of 1000). The maxRegolith and maxIron capacities are forcefully overridden in FoundryCrawler.Awake() to guarantee the Unity Inspector doesn't accidentally load old, smaller prefab capacities.
* **Resource Spawning:** As the crawler moves, the Pipeline Manager exposes Regolith and Iron deposits along the path. These deposits are explicitly parented to the PlanetGenerator so the GreedyAI recognizes them.

#### 3. Drone Routing & Economy (WorkerBrainController & Supplies)
* **Identification:** Drones perfectly identify what they are holding by checking both the SupplySO.name and the physical GameObject.name (fallback).
* **Iron & Regolith:** Routed directly into the active Foundry Crawler's hoppers to fuel the pipeline expansion. Drones will actively avoid the crawler if its hoppers reach maximum capacity (1000).
* **Gas & Minerals:** Routed to the active Foundry Crawler as a centralized drop-off point, but bypass the physical hoppers. Instead, they instantly liquidate into the global **Materials** economy by triggering the GatherEventChannelSO.

#### 4. Game Over Logic (GameOverManager)
* **Depletion Checks:** The game continuously checks if there are valid ways to recover. If Materials is low and all GatherableSupply nodes on the map are destroyed, it triggers Game Over. *(Note: This logic is being repurposed for the new Micro-Round Depletion Trigger).*
* **Pipeline Protection:** To prevent false Game Overs when a drone fully depletes a fuel node, the GameOverManager explicitly checks if there is an active EnergyPipelineManager still expanding. If a pipeline is still building, it assumes more resources will be spawned shortly and aborts the Game Over.
* **Quit & Scene Unload Safety:** Employs static isQuitting tracking via Application.quitting to automatically suppress any loss-checking or GameOver events during scene teardown, editor playmode transition, or application exit. This prevents false Game Over prompts during destruction of scene objects on shutdown.

#### 5. UI & Selection Indicators
* **Standardized Outlines:** The FoundryCrawler uses C# Reflection to look inside the constructionDronePrefab and perfectly clone its standard selection indicator ring. This ensures the Crawler matches the stylistic visual outlines of all other units in the game, scaled perfectly to 1.5x to fit the Crawler's chassis without distorting into a dome.

#### 6. Recent Fixes Changelog
* **Probe Build Order (race condition):** Added BaseBuilding.IsFirstInQueueProbe(). GreedyAIController and AIController now skip buildings whose first queued item is the Probe. Added regression test ColonyExpansionTests.ColonyExpansion_BuildsProbeDroneFirst.
* **Curved World Visuals:** Added an "Animal Crossing" style spherical planet illusion without breaking the flat NavMesh. Created CurvedWorld.shader and CurvedWorldUpdater.cs. 
* **Assorted Compiler Fixes:** Removed duplicate method definitions in PlanetGenerator.cs. Fixed an invalid cast warning by treating FoundryCrawler as an AbstractCommandable. Removed unused isProducing field from FoundryCrawler.cs.
* **Application Quit False Triggers:** Solved a critical issue where OnDestroy callbacks on GatherableSupply nodes during scene unloads triggered a cascade of SupplyDepletedEvents, resulting in a false "Resources depleted" screen. 
* **End-of-Generation UI Fixes:** Resolved an issue where the `GenerationSummaryUI` and `TechTreeUI` would fail to appear upon generation completion. Both UIs now aggressively check and re-enable their root GameObjects if they were disabled in the Inspector, ensuring the `panel.SetActive(true)` calls correctly display the UI. Also added detailed reference validation logs to `TechTreeUI` to prevent silent failures if Inspector variables are unassigned.
* **Tech Tree UI Formatting:** Fixed text overlapping and unreadable shrinking in the `TechTreeItemUI` prefab. Text now wraps correctly and is locked to a readable minimum 14pt size. Removed hardcoded C# UI sizing logic so the `TechTreeUI` Grid Layout sizes can be managed strictly inside the Unity Inspector. Added `TestTechTree.cs` editor menu tool to instantly open the UI from Playmode with 5000 test coins.
* **Instant-Depletion Loop Bug:** Fixed a "Time Travel" physics issue where clicking "Next Generation" would instantly end the generation again on the very next frame. Because Unity's `Destroy()` queues objects for cleanup at the end of the frame, the `GenerationManager` was counting dying resource nodes from the previous round before they vanished. Added a 2-second `roundStartTime` grace period to let Unity physics settle before the generation begins counting remaining resources.

#### 7. Errors Encountered & Resolutions
* Fixed CS0111: Type 'PlanetGenerator' already defines a member called 'ScatterResources' by removing the empty stub.
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

#### 10. World-Space Foundry Crawler Metrics
* **FoundryWorldUI:** Added a custom billboarding world-space canvas to the side of the Foundry crawler prefab displaying live stats for Regolith (Yellow), Iron (Gray), and Pipes Buffer (Cyan).
* **Lowered Close Positioning:** Adjusted the local Y position down to 0.55 so the status text sits exactly 0.64 world meters above the top of the forge roof.

#### 11. Recent Visual and Economy Polish
* **Map Wrapping Visual Fix:** The CurvedWorldUpdater.cs has been moved to LateUpdate() to ensure it only queries the camera's final position, fixing a massive 1-frame backwards bend during teleports.
* **Dynamic Proximity Labels:** Added an OnGUI overlay to GatherableSupply.cs that dynamically draws the resource's name above the node when the camera is within 15 units.
* **Visual Shrinking:** GatherableSupply.cs now visually shrinks the node's transform proportional to the remaining amount (clamped to 30%) when resources are depleted.
* **Geography Camouflage for Fuel:** PlanetGenerator.ScatterFuelResources() now randomly selects non-crystal rock models, applies the dynamically generated ground color, and hides fuel resources as natural planetary geography.
* **Selection Indicator Sizing:** Modified BaseBuilding.Start() to intercept the selectionIndicator of the Command Post and shrink it by a factor of 0.6f.
* **Gas/Minerals Efficiency Routing:** Updated WorkerBrainController.cs so that Gas and Minerals route directly back to the Command Center instead of the expanding Crawler, boosting Materials income.
* **Runtime Shader Injection:** Hooked ApplyCurvedWorldShader into AbstractCommandable.Start() to ensure all dynamically spawned units perfectly hug the bent terrain.

#### 12. Hybrid Construction & Auto-Supply Crawler
* **Hybrid Building Construction:** Drones will withdraw from the global bank if the Command Center is physically closer, otherwise they will manually mine Map Resources.
* **Auto-Supply Crawler Mechanics:** Drones can enter an autonomous SupplyMissionLoop to fetch missing Iron/Regolith for the Crawler. Changed the assignment command to the standard Build command to prevent Unity behavior cancellation.
* **Parallax Sinking Fix:** Prevented the Crawler from snapping its Y-pivot to the flat NavMesh, stopping it from visually "sinking" underground.
* **UI & UX Changes:** ProbeLogic no longer automatically calls TriggerExpansion; players place Command Centers directly again via their drone's Build UI. Added a persistent Volume Control Slider to the PauseMenuUI.

#### 13. Architecture Pivot: Sector Lockdown & Milestone Progression
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
<summary><b>Foundry Crawler (Levels 1-20)</b></summary>

1. **Treads Mk I** (+MovementSpeed)
2. **Iron Hopper Mk I** (+MaxIron)
3. **Regolith Hopper Mk I** (+MaxRegolith)
4. **Treads Mk II** (+MovementSpeed)
5. **Iron Hopper Mk II** (+MaxIron)
6. **Regolith Hopper Mk II** (+MaxRegolith)
7. **Treads Mk III** (+MovementSpeed)
8. **Iron Hopper Mk III** (+MaxIron)
9. **Regolith Hopper Mk III** (+MaxRegolith)
10. **Treads Mk IV** (+MovementSpeed)
11. **Iron Hopper Mk IV** (+MaxIron)
12. **Regolith Hopper Mk IV** (+MaxRegolith)
13. **Treads Mk V** (+MovementSpeed)
14. **Iron Hopper Mk V** (+MaxIron)
15. **Regolith Hopper Mk V** (+MaxRegolith)
16. **Advanced Treads** (+MovementSpeed)
17. **Advanced Iron Hopper** (+MaxIron)
18. **Elite Regolith Hopper** (+MaxRegolith)
19. **Elite Treads** (+MovementSpeed)
20. **Omega Iron Hopper** (+MaxIron)
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

#### 19. Blueprint Card Deck Drafting & Milestone Progression Loop
* **1-Sector-Per-Round Paradigm:** Shifted the gameplay loop so that one sector corresponds directly to exactly one generation (round).
* **Card Deck Drafting System:**
  * Created `CardDeckController.cs` (monitored by `SectorManager.OnSectorUnlocked`) which manages a player's `masterDeck` of blueprint cards (`BlueprintCardSO`), drawing, shuffling, and discarding hands (default size 3) on sector expansion.
  * Added `DraftingUI.cs` and `CardSlotUI.cs` to handle full-screen overlay rendering, unscaled fade transitions, and hover pop-scale animations during selection.
* **Milestone Progression Engine:**
  * Updated `GenerationManager.cs` to check milestone completion conditions inside `Update()` rather than resource depletion.
  * Supported milestone requirements: `Biomass` levels, atmospheric `Oxygen` percentages, grid `Power` capacity, `Population` limits, and completed `CommandPosts` built inside the active sector.
  * Starting a new generation/round calls `SectorManager.Instance.UnlockNextSector()` to unlock the next adjacent sector, which automatically initiates a fresh card draft phase.

#### 20. Themed Card Decks, Climate Gates, and Active Structure Abilities
* **Procedural Card/Building Generation:** Since the 20 themed buildings did not exist as pre-defined asset files, they are instantiated as `BuildingSO` and `TerraformingCardSO` ScriptableObjects at runtime by cloning a default building asset (`SolarPanel` or `Habitat`) and overriding its cost, stats, and configurations.
* **Climate Gates Filtering:** `TerraformingCardSO` defines environmental constraints (Temperature, Oxygen, Atmosphere) and geological requirements (`LavaTube`, `FaultLine`, `WaterDeposit`). `BlueprintDraftUI.GetRandomCards()` filters the selection pool using `IsGateMet()`.
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
7. **Automated Repair Crawler**
   * *Type:* Spawn Unit
   * *Description:* Deploy a specialized Repair Drone to automatically rebuild pipelines and repair bases.
8. **High-Power Induction Drills**
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

