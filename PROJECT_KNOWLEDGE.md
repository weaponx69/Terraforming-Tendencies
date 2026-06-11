# Terraforming Tendencies - Project Knowledge & Architecture Notes

This document serves as a persistent memory bank for AI context, detailing the core systems, recent architectural decisions, and current state of the game's economy.

## 1. Automated Economy & AI (GreedyAIController)
- **Logarithmic Spending:** The AI limits its spending mathematically so it doesn't instantly bankrupt the player's treasury. 
- **Strict Start Priority:** To prevent the AI from deadlocking itself, it bypasses the spending limit specifically for the *very first* Probe drone. It uses an `ignoreReserve` flag to forcefully queue the probe. Once the probe is queued, the AI unlocks and interleaves Construction and Mining drones normally.
- **Unit Assignment:** The AI dynamically scans for GatherableSupplies and automatically assigns idle Mining Drones. It explicitly targets supplies that are children of the `PlanetGenerator` to avoid mining invalid debris.


- **Expansion Priority:** When a new Command Post is established via the `EnergyPipelineManager`, it uses `BuildPriorityUnlockable` to queue a starter Probe. Both `GreedyAIController` and `AIController` explicitly check `BaseBuilding.IsFirstInQueueProbe()` to avoid filling the queue before this starter unit is registered.
## 2. Colony Expansion & Mobile Forge (FoundryCrawler & EnergyPipelineManager)
- **Pipeline Logistics:** The `EnergyPipelineManager` drives the expansion of the colony. It spawns the `FoundryCrawler` which slowly crawls along the pipeline path (`movementSpeed = 0.05f`).
- **Crawler Fuel Hoppers:** The crawler requires Regolith and Iron to move. It has internal hoppers (default reset: 500 Regolith, 200 Iron, with a max of 1000). The `maxRegolith` and `maxIron` capacities are forcefully overridden in `FoundryCrawler.Awake()` to guarantee the Unity Inspector doesn't accidentally load old, smaller prefab capacities.
- **Resource Spawning:** As the crawler moves, the Pipeline Manager exposes Regolith and Iron deposits along the path. These deposits are explicitly parented to the `PlanetGenerator` so the GreedyAI recognizes them.

## 3. Drone Routing & Economy (WorkerBrainController & Supplies)
- **Identification:** Drones perfectly identify what they are holding by checking both the `SupplySO.name` and the physical `GameObject.name` (fallback). 
- **Iron & Regolith:** Routed directly into the active Foundry Crawler's hoppers to fuel the pipeline expansion. Drones will actively avoid the crawler if its hoppers reach maximum capacity (1000).
- **Gas & Minerals:** Routed to the active Foundry Crawler as a centralized drop-off point, but bypass the physical hoppers. Instead, they instantly liquidate into the global **Biomass** economy by triggering the `GatherEventChannelSO`.

## 4. Game Over Logic (GameOverManager)
- **Depletion Checks:** The game continuously checks if there are valid ways to recover. If Biomass is low and all `GatherableSupply` nodes on the map are destroyed, it triggers Game Over.
- **Pipeline Protection:** To prevent false Game Overs when a drone fully depletes a fuel node, the `GameOverManager` explicitly checks if there is an active `EnergyPipelineManager` still expanding. If a pipeline is still building, it assumes more resources will be spawned shortly and aborts the Game Over.

## 5. UI & Selection Indicators
- **Standardized Outlines:** The `FoundryCrawler` uses C# Reflection to look inside the `constructionDronePrefab` and perfectly clone its standard selection indicator ring. This ensures the Crawler matches the stylistic visual outlines of all other units in the game, scaled perfectly to 1.5x to fit the Crawler's chassis without distorting into a dome.

## 6. Hero Drone (Mobile Command Center)
- **Hybrid Control:** `HeroDroneController` (on a duplicated drone prefab) lets the player pilot a "Hero" command drone with WASD (Riftbreaker style) while the mouse stays fully free for RTS edge-pan, click, and drag-select.
- **Input Routing:** `PlayerInput` exposes `useHeroControlMode` and a `heroDrone` slot. When enabled, WASD is suppressed from camera panning (`HandlePanning`) and instead converted to a **camera-relative** world direction fed to `HeroDroneController.SetMoveInput`. Pressing any movement key snaps the camera back onto the drone.
- **NavMesh Override:** On taking manual control the controller calls `WorkerBrainController.Halt()` and `AbstractUnit.Stop()`, then disables the `NavMeshAgent` so transform-driven movement does not fight pathfinding. On release it re-enables the agent and `Warp`s it back onto the NavMesh. If no NavMesh is nearby, movement falls back to free transform motion.
- **Commands:** The Hero Drone carries the base-building commands in its `AvailableCommands` array, so it issues Build commands like a Command Center via the existing command/selection pipeline.

## 7. Hero Drone Spawning & AI Exemption
- **Auto-Spawn:** `HeroDroneSpawner` (in the scene) listens for `PlanetGenerator.OnPlanetGenerated` and instantiates `Resources/Units/Hero Drone.prefab` at the first Player1 base (or map centre), sets Owner=Player1, and calls `PlayerInput.AssignHeroDrone()` so no manual scene wiring is needed.
- **Prefab:** `Hero Drone.prefab` is a duplicate of the Mining Drone (an AIR unit at flight height ~4) with `HeroDroneController` plus the Command Post's full `AvailableCommands` set.
- **Air-Unit Movement:** `HeroDroneController` moves the transform freely in XZ and samples the NavMesh ONLY to adopt hover height. It must NOT snap XZ to the nearest NavMesh point — air units sit on a small elevated NavMesh patch and a full snap pins them in place (zero movement).
- **AI Exemption:** `GreedyAIController` skips any Worker with a `HeroDroneController` in `AssignIdleWorkers`, `FindAvailableWorker`, `FindWorkerForExpansion`, and `HasAvailableBuilder`, so the automated economy never hijacks the player's Hero Drone for mining/building.

- **Smooth Movement (no rubber-banding):** The Hero Drone is moved by direct transform.position writes each frame and the camera hard-snaps to it. Its kinematic Rigidbody Interpolation MUST be None — Interpolate makes the rendered mesh lag behind the actual transform while the camera tracks the real position, producing a 'move forward then snap back' jitter. Do not enable Rigidbody interpolation on transform-driven units that a hard-follow camera tracks.
