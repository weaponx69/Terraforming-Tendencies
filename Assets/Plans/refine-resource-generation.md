# Project Overview
- Game Title: Terraforming Tendencies
- High-Level Concept: An RTS game where players terraform a planet and manage resources like Gas and Minerals.
- Players: Single player (implied by RTS mechanics and campaign manager).
- Render Pipeline: URP (confirmed by project settings).
- Target Platform: PC (Linux/Standalone).

# Game Mechanics
## Core Gameplay Loop
- Generate a procedural planet surface.
- Scatter environment features (rocks) and resources (Gas, Minerals).
- Players likely gather resources to build and terraform.

## Controls and Input Methods
- New Input System is active.
- RTS-style mouse/keyboard controls.

# UI
- Menu for procedural asset generation.
- HUD for resource management (implied by tech tree and supply scripts).

# Key Asset & Context
- `PlanetConfig.cs`: ScriptableObject holding map settings and prefab arrays.
- `PlanetGenerator.cs`: Handles mesh generation and feature scattering.
- `Minerals.prefab`, `Gas.prefab`, `PoisonGas.prefab`: Resource prefabs.
- `RandomRock_...`, `RandomCrystal_...`: Procedural features.

# Implementation Steps
1. **Refine `PlanetConfig` Arrays**: 
    - Ensure `ResourcePrefabs` contains `Minerals`, `Gas`, and `RandomCrystal` prefabs.
    - Remove `Minerals` and `RandomCrystal` from `SurfaceFeaturePrefabs` so they aren't scattered as static rocks.
2. **Implement Resource Scattering in `PlanetGenerator.cs`**:
    - Add a `ScatterResources()` method to `PlanetGenerator`.
    - Use `Config.ResourcePrefabs` and `Config.ResourceCount` to spawn gatherable resources.
    - Resources should be spawned similarly to features but using the dedicated resource count.
3. **Update `AutoFixPrefabs.cs`**:
    - Ensure it correctly populates `ResourcePrefabs` with the appropriate assets.
    - Ensure `SurfaceFeaturePrefabs` only contains rocks/decorative features.
4. **Cleanup**: 
    - Verify that `PlanetGenerator` calls `ScatterResources` during generation.
    - Ensure tinting logic is applied correctly (or skipped for resources).

# Verification & Testing
- Use the context menu "Generate Planet (Editor)" on the PlanetManager to verify scattering.
- Check that resources (Minerals/Gas) appear on the planet surface.
- Verify that resources have the `GatherableSupply` component (already exists on prefabs).
- Confirm that procedural crystals/minerals are not tinted brown like the rocks.
