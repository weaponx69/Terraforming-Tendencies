# Project Overview
- Game Title: Terraforming Tendencies
- High-Level Concept: RTS game with procedural planet generation and resource management.
- Players: Single player.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
- Generate planet surface.
- Scatter rocks, features, and resources.
- Players gather resources (Gas, Minerals).

# Key Asset & Context
- `PlanetConfig.cs`: Holds data for map size, density, and prefabs.
- `PlanetGenerator.cs`: Generates the mesh and scatters features.
- `Planet 1 - Easy.asset`: The main configuration asset.

# Implementation Steps
1. **Modify `PlanetGenerator.cs`**:
    - Add `private void ScatterResources()` method.
    - This method will loop `Config.ResourceCount` times.
    - In each loop, it will pick a random prefab from `Config.ResourcePrefabs`.
    - It will find a random position (respecting the exclusion zone and min spacing).
    - It will instantiate the resource and ensure it has necessary components (like `HiddenResource` if applicable, though `GatherableSupply` is likely enough).
    - Resources should also be "ghosted" for seamless wrapping, just like the rocks.
    - Update `GeneratePlanet()` to call `ScatterResources()`.
2. **Verify `PlanetConfig`**:
    - Ensure `ResourceCount` and `ResourcePrefabs` are correctly set (already verified as 80 and Gas/Minerals).

# Verification & Testing
- Use the "Generate Planet (Editor)" context menu in the Unity Editor to verify that resources are appearing.
- Check the console for any errors during generation.
- Ensure resources are scattered randomly across the planet.
