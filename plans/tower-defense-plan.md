# Tower Defense Implementation Plan for Terriforming Tendencies

## Overview
Transform the existing survival RTS mechanics into a tower defense experience where players must strategically place defensive structures to protect their colony from waves of enemies.

## Core Changes Needed

### 1. Tower Defense Building Types
- **Basic Tower**: Low cost, fast attack, short range
- **Heavy Tower**: High cost, slow attack, long range, splash damage
- **Support Tower**: Buffs nearby towers, heals buildings
- **Resource Tower**: Generates resources over time

### 2. Enemy Wave System
- Modify NaturalEventManager to spawn enemy units instead of natural disasters
- Create different enemy types with varying health, speed, and abilities
- Implement wave progression with increasing difficulty

### 3. Pathfinding and Targeting
- Define enemy paths toward the colony
- Implement tower targeting logic (first, last, closest, strongest)
- Add range indicators for tower placement

### 4. UI Enhancements
- Tower selection menu
- Wave information display
- Tower upgrade interface
- Resource management for tower costs

### 5. Game Flow Changes
- Between rounds: Tower placement phase
- During rounds: Enemy waves attack
- Wave completion rewards resources for more towers

## Implementation Steps

### Phase 1: Tower Building System
1. Create TowerSO scriptable objects for different tower types
2. Modify BuildingSO to include tower-specific properties (range, damage, fire rate)
3. Update BuildBuildingCommand to handle tower placement
4. Create tower prefabs with appropriate visuals and scripts

### Phase 2: Enemy Wave System
1. Create EnemySO scriptable objects for enemy types
2. Modify NaturalEventManager to spawn enemy waves
3. Implement enemy pathfinding toward colony center
4. Create wave progression system

### Phase 3: Tower Logic
1. Create TowerAttack script for targeting and firing
2. Implement different tower behaviors (single target, AoE, support)
3. Add tower upgrade system
4. Create visual effects for attacks

### Phase 4: Integration and Testing
1. Balance tower costs, damage, and range
2. Test wave difficulty progression
3. Ensure resource economy supports tower building
4. Polish visual and audio feedback

## Files to Create/Modify

### New Files:
- Scripts/Units/TowerSO.cs
- Scripts/Units/EnemySO.cs
- Scripts/Behavior/TowerAttackAction.cs
- Scripts/Commands/TowerBuildCommand.cs
- Scripts/Environment/WaveManager.cs
- Scripts/Enemy/BaseEnemy.cs
- Scripts/Enemy/EnemyTypes/*

### Modified Files:
- Scripts/Commands/BuildBuildingCommand.cs (add tower handling)
- Scripts/Environment/NaturalEventManager.cs (convert to wave system)
- Scripts/Units/BuildingSO.cs (add tower properties)
- Tech Trees/Human Tech Tree.asset (add tower unlocks)
- UI/Prefabs/* (tower selection UI)

## Dependencies
- Existing building placement system
- Resource management (Biomass/Materials)
- Health system for colony defense
- Event system for wave triggering

## Success Criteria
- Players can place different tower types during build phase
- Enemy waves follow predictable paths toward colony
- Towers automatically target and attack enemies
- Waves increase in difficulty over time
- Colony survives based on tower placement strategy
- Resource economy supports meaningful tower choices