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

- **Hero Drone Camera Smoothness:** The CinemachineBrain UpdateMethod MUST be Late Update (not Smart Update). The camera follow target (cameraTarget) is a kinematic Rigidbody; Smart Update resolves the camera in FixedUpdate for Rigidbody targets, but the Hero Drone moves in Update and cameraTarget is repositioned in PlayerInput.LateUpdate. That timing mismatch causes camera stutter at framerates above the physics step. Late Update makes the camera resolve every rendered frame after the target is positioned.

## 8. Recent Fixes Changelog

This section is a flat, copy-paste-friendly list of fixes applied during the Hero Drone work and related cleanup.

### Probe Build Order (race condition)
- Symptom: New Command Posts did not build the starter Probe first; the AI queue-jumped.
- Cause: `GreedyAIController` filled a newly completed Command Post's queue before the expansion's priority Probe was registered.
- Fix: Added `BaseBuilding.IsFirstInQueueProbe()`. `GreedyAIController` and `AIController` now skip buildings whose first queued item is the Probe. Added regression test `ColonyExpansionTests.ColonyExpansion_BuildsProbeDroneFirst`.

### Hero Drone Feature (mobile command center)
- `HeroDroneController.cs`: WASD piloting; receives a camera-relative world vector via `SetMoveInput`.
- `PlayerInput.cs`: added `useHeroControlMode` flag + `heroDrone` slot + `AssignHeroDrone()`; WASD is rerouted from camera panning to the drone; mouse (edge-pan, click, drag-select) stays free.
- `HeroDroneSpawner.cs`: auto-spawns `Resources/Units/Hero Drone.prefab` on `PlanetGenerator.OnPlanetGenerated`, sets Owner=Player1, links it into `PlayerInput`. No manual scene wiring needed.
- `Hero Drone.prefab`: duplicate of the Mining Drone (AIR unit, flight height ~4) with `HeroDroneController` + the Command Post's full `AvailableCommands`.
- AI exemption: `GreedyAIController` ignores any Worker with a `HeroDroneController` so the economy never hijacks it.

### Hero Drone Movement (air-unit pinning)
- Symptom: Drone responded to input but did not actually move.
- Cause: Air units sit on a small elevated NavMesh patch; snapping XZ to the nearest NavMesh point pinned the drone. Also `AbstractUnit.Update` re-enables a disabled agent every frame.
- Fix: Move the transform freely in XZ; sample the NavMesh ONLY for hover height. Leave the agent enabled but decoupled (`updatePosition`/`updateRotation = false`) instead of disabling it.

### Hero Drone Camera Stutter (the real fix)
- Symptom: Camera moved forward then snapped back while piloting.
- Root cause: The camera follow target (`cameraTarget`) is a kinematic Rigidbody. The hero follow wrote `cameraTarget.position` (Rigidbody API), which DEFERS the transform sync to the next physics step (`Physics.autoSyncTransforms` is off by default). The `CinemachineBrain` (LateUpdate) reads the transform, so it saw a stale position on non-physics frames.
- Fix: Move the follow target via `cameraTarget.transform.position` (immediate), not `Rigidbody.position` (deferred). Also confirmed `CinemachineBrain.UpdateMethod = Late Update` and Rigidbody Interpolation = None for transform-driven units tracked by a hard-follow camera.
- Note: This SUPERSEDES the earlier interpolation/Smart-Update theories — the decisive cause was Rigidbody.position vs transform.position timing.

### Compile Error: duplicate ScatterResources (CS0111)
- `PlanetGenerator.cs` had two `ScatterResources()` methods (an empty stub + the real implementation). Removed the empty stub; kept the full implementation.

### Shader Error: CurvedWorld ShadowCaster (_LightDirection undeclared)
- `Assets/Shaders/CurvedWorld.shader` ShadowCaster pass used `_LightDirection` without declaring it (its custom vertex program does not include URP's `ShadowCasterPass.hlsl`).
- Fix: Declared `_LightDirection` and `_LightPosition` globals; select direction for directional vs punctual (`_CASTING_PUNCTUAL_LIGHT_SHADOW`) shadows; added the near-plane clamp from URP's standard pass.

### Hero Drone Resource Mechanic & Global Scattering
- **Frenzy Economy**: The `FoundryCrawler` pipeline no longer auto-spawns resources next to the crawler. Instead, 250 instances of Iron and Regolith are scattered globally in `PlanetGenerator.cs` (`ScatterFuelResources()`).
- **Hero Hauler**: `HeroDrone.cs` now has inventory capacity (max 25), and `HeroDroneController.cs` automatically vacuums nearby resources via an `OverlapSphere` in `HandleAutoInteraction()`. The Hero Drone can manually drop these off at the Crawler or Command Post.

### Curved World Visuals
- Added an "Animal Crossing" style spherical planet illusion without breaking the flat NavMesh.
- Created `CurvedWorld.shader` (a custom URP Lit shader that offsets Y-vertices downward based on squared distance from camera).
- Created `CurvedWorldUpdater.cs` to globally track the camera's position.
- Updated `PlanetGenerator.cs` to automatically sweep the entire scene and replace default URP shaders with the curved shader upon generation.

### Assorted Compiler Fixes
- `PlanetGenerator.cs`: Removed duplicate method definitions (`FixPreplacedGatherables`, `EnsureGatherableSupply`, `ScatterResources`) and fixed missing variable assignments. Fixed a missing property reference by changing `FloraPrefabs` to `EnvironmentPrefabs`.
- `HeroDroneController.cs`: Fixed a missing namespace by using `GameDevTV.RTS.Units.Owner`.
- `PlayerInput.cs`: Removed an invalid cast warning (`evt.Unit is FoundryCrawler`); `FoundryCrawler` inherits from `AbstractCommandable`, not `AbstractUnit`.
- `FoundryCrawler.cs`: Removed the unused `isProducing` field and deleted old `ExposeForgeDeposits()` pipeline spawning logic.
