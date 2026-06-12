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
- **Hero Drone Check:** Integrates `heroDroneAlive` into the recovery calculation so that a "no resources" game over is not triggered if standard mining units are dead but the player's Hero Drone is still active and capable of harvesting.
- **Quit & Scene Unload Safety:** Employs static `isQuitting` tracking via `Application.quitting` to automatically suppress any loss-checking or GameOver events during scene teardown, editor playmode transition, or application exit. This prevents false Game Over prompts during destruction of scene objects on shutdown.

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

### Game Over Robustness & Safety
- **Hero Drone Exemption:** Added `heroDroneAlive` verification during `CheckNoRecovery` to prevent premature Game Over triggers if the Hero Drone is still alive and capable of collecting scattered materials, even with 0 workers and < 400 Biomass.
- **Application Quit/Scene Unload False Triggers:** Solved a critical issue where `OnDestroy` callbacks on `GatherableSupply` nodes during editor playmode exit/scene unloads triggered a cascade of `SupplyDepletedEvent`s, resulting in a false "Resources depleted" Game Over screen being drawn right as the game shut down. Added static `isQuitting` tracking linked to `Application.quitting` to abort checks when exiting.

## 9. Errors Encountered & Resolutions

Verbatim compile/shader errors seen during the Hero Drone work and cleanup, with their fix or current status. Console is currently clean (0 errors).

### Fixed by us
- `PlanetGenerator.cs(569,30): error CS0111: Type 'PlanetGenerator' already defines a member called 'ScatterResources' with the same parameter types`
  - Cause: an empty `ScatterResources()` stub duplicated the real implementation.
  - Resolution: removed the empty stub, kept the full implementation.

- `Shader error in 'Custom/URP_CurvedWorld': undeclared identifier '_LightDirection' at Assets/Shaders/CurvedWorld.shader(161) (on glcore)` (Pass: ShadowCaster)
  - Cause: custom ShadowCaster vertex program used `_LightDirection` without declaring it (does not include URP's `ShadowCasterPass.hlsl`).
  - Resolution: declared `_LightDirection` and `_LightPosition`; selected direction for directional vs punctual shadows (`_CASTING_PUNCTUAL_LIGHT_SHADOW`); added the near-plane clamp.

### Previously flagged, since resolved (console now clean)
These appeared transiently while code was mid-refactor. They are no longer present; re-investigate only if they reappear.
- `PlanetGenerator.cs(342,21): error CS0103: The name 'ScatterFlora' does not exist in the current context`
- `EnergyPipelineManager.cs (multiple lines): error CS0103: The name 'neededSegments' does not exist in the current context`
- `FoundryCrawler.cs(174,33): error CS1061: 'EnergyPipelineManager' does not contain a definition for 'ExposeForgeDeposits'`
- `HeroDroneController.cs(291,71): error CS0234: The type or namespace name 'Owner' does not exist in the namespace 'GameDevTV.RTS.Player'`
  - Note if this recurs: `Owner` lives in a different namespace (e.g. `GameDevTV.RTS.Units`); reference it with the correct namespace rather than `GameDevTV.RTS.Player.Owner`.

## 10. Hero Drone Harvesting & HUD Updates

### Harvesting Fixes
- **Interaction Radius:** Increased `interactionRadius` in `HeroDroneController.cs` from 5f to 6f to reliably reach ground resources while hovering.
- **Auto-Discovery:** Added logic to `HeroDroneController.HandleAutoInteraction` to call `HiddenResource.Discover()` on nearby resources. This enables their colliders, allowing the drone's physics-based vacuum to detect them.
- **Resource Spawning:** Fixed `PlanetGenerator.ScatterFuelResources` loading paths. It now correctly looks in `Gatherable Supplies 1/Iron` and `Gatherable Supplies 1/Regolith` to match the project's prefab folder structure.

### HUD Updates
- **Hero Cargo Category:** Displays current cargo held by the Hero Drone (e.g., "12/25 Iron"). Only visible when carrying resources.
- **Probe Progress Category:** Displays the analysis percentage of the most advanced active Probe (e.g., "Scanning: 85%").
- **Logic:** `RuntimeUI.cs` now auto-links these containers by name and subscribes to `HeroDrone.OnCargoChanged` to provide real-time updates.
- **Visuals:** Added `Hero Cargo Container` and `Probe Progress Container` to the `Supplies Bar` in the scene, styled to match existing resource containers.

## 11. Hero Drone Delayed Harvesting & Feedback Juice

### Harvesting Refinements
- **Delayed Collection:** Vacuuming resources is no longer instant. It now requires the drone to remain stationary (within a **1.0 unit tolerance** to accommodate hovering drift) for **5 seconds** over a resource.
- **Progress Bar:** A world-space progress bar (cloned from the Probe prefab) appears above the Hero Drone during the harvesting process. It is dynamically configured as a horizontal filled image and linked directly to the controller.
- **Dynamic Cargo Text:** A custom billboarding text (`Cargo Text`) displays directly beneath the Hero Drone, updating in real-time when resources are carried (e.g., `12/25 Iron`). It automatically hides when empty.
- **Robust Vacuum Logic:** Handled a potential `InvalidOperationException` by copying the `GatherableSupply.ActiveSupplies` collection before discovery scanning, preventing collection modification errors during runtime.
- **Popup Feedback:** Fixed an issue where the floating popups spawned inside the drone's collision mesh. Popups are now assigned a standard fallback font asset and offset **4 units upward**, rendering cleanly above the unit.

## 12. HUD Standardization & Colony Expansion Integration

### Category Re-Alignment
- **Centered Layout:** Changed all top-bar HUD containers to use a vertically-stacked layout where the **Header Label** (e.g., OXYGEN) is centered at the top and the **Metric** (Icon + Value) is horizontally aligned and centered directly beneath it.
- **Fixed Stretched Icons:** Locked all HUD icons to a standard **24x24** pixel size via `LayoutElement` components, preventing layout groups from stretching them.
- **Auto-Wiring:** Implemented self-wiring in `RuntimeUI.Awake()` to automatically detect, link, and subscribe to UI containers in the scene hierarchy upon initialization.

### Expansion Progress Integration
- **Sectors Metric Restore:** Restored the colony expansion progress metric back to its original integrated display inside the "Sectors" label (e.g., `0/4 (Exp: 50%)`).
- **Layout Width Adjustments:** Increased the width of the Sectors HUD container to **220** to ensure the integrated text has sufficient space and never clips. Deactivated the redundant separate Expansion container in the scene.

## 13. Persistent Audio System (AudioManager)

### Soundtrack Integration
- **Soundtrack:** Generated a 2-minute-56-second high-quality Stereo WAV space ambient soundtrack at `Assets/Resources/Audio/Music/AtmosphericSoundtrack.wav`.
- **Persistent BGM:** Created `AudioManager.cs` as a persistent Singleton (`DontDestroyOnLoad`) that handles the soundtrack.
- **Zero-Setup Startup:** Uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` to automatically spawn and play the BGM immediately upon launching the game, regardless of whether starting from the Main Menu or Gameplay scenes.
- **Smooth Volume Fading:** Features a built-in volume fader that smoothly transitions the audio from 0 to 0.5 volume over **3.0 seconds** for a professional, cinematic entrance. Includes pause and resume capabilities.

## 14. World-Space Foundry Crawler Metrics

### Production Display
- **FoundryWorldUI:** Added a custom billboarding world-space canvas (`FoundryWorldUI.cs`) to the side of the `Foundry` crawler prefab.
- **Real-Time Display:** Displays live stats for **Regolith**, **Iron**, and **Pipes Buffer** in the game world, allowing players to monitor production status at a glance without selecting the unit.
- **Color Coding:** Color-coded resources to match the game's theme (Yellow for Regolith, Gray for Iron, and Cyan for Pipes).
- **Lowered Close Positioning:** Adjusted the local position of the `World UI` child on the `Foundry` prefab from `(0, 5, 0)` down to `(0, 0.55, 0)`. Because the root prefab has a scale of `8.0`, the original offset placed the status text `40.0` meters in the air. Lowering it to `0.55` places it exactly `0.64` world meters above the top of the `3.76` meter high forge roof, creating a tight, snug, and highly readable look.


