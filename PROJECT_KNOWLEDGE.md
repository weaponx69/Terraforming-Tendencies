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
