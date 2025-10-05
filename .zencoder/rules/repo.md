---
description: Repository Information Overview
alwaysApply: true
---

# Terriforming Tendencies Information

## Summary
Terriforming Tendencies is a Unity-based Real-Time Strategy (RTS) game project. It features a behavior-driven AI system using ScriptableObjects for data configuration, an EventBus for communication, and NavMesh for unit movement. The game includes various unit types (Workers, Riflemen, Grenadiers), buildings, and resource gathering mechanics.

## Structure
- **Assets**: Core Unity assets including scripts, prefabs, and resources
- **Scripts**: C# code organized by functionality (Behavior, Units, EventBus, etc.)
- **Units**: Unit definitions, animations, and configurations
- **Textures**: Game textures and rendering resources
- **Shaders**: Custom shader definitions for visual effects
- **Packages**: Unity package dependencies

## Language & Runtime
**Language**: C# (.NET)
**Unity Version**: Unity 2020+ (based on package dependencies)
**Build System**: Unity Editor
**Package Manager**: Unity Package Manager

## Dependencies
**Main Dependencies**:
- com.unity.ai.navigation (2.0.5) - NavMesh for unit pathfinding
- com.unity.behavior (1.0.5) - Behavior system for unit AI
- com.unity.cinemachine (3.1.1) - Advanced camera control
- com.unity.inputsystem (1.13.0) - Input handling
- com.unity.render-pipelines.universal (17.0.3) - Rendering pipeline
- com.unity.ugui (2.0.0) - UI framework

**Development Dependencies**:
- com.unity.ide.visualstudio (2.0.22)
- com.unity.ide.rider (3.0.31)
- com.unity.test-framework (1.4.6)

## Architecture
**Core Systems**:
- **ScriptableObject Data**: Configuration via UnitSO, AttackConfigSO, etc.
- **Behavior System**: BehaviorGraphAgent with blackboard variables
- **Event System**: Bus<T> pattern for decoupled communication
- **Unit Framework**: AbstractUnit base class with specialized implementations

## Key Components
**Units & Combat**:
- Units use NavMeshAgent for movement and BehaviorGraphAgent for AI
- Combat system with area-of-effect damage and projectile handling
- Unit types include Workers (resource gathering), Riflemen, Grenadiers, and Air Transport

**AI & Behavior**:
- Behavior nodes (AttackTargetAction, MoveToTargetGameObjectAction)
- Blackboard variables for state management (Command, TargetGameObject, TargetLocation)
- Sensor system for enemy detection (DamageableSensor)

**Resource & Tech System**:
- Supply gathering mechanics (GatherableSupply, IGatherable)
- Tech tree with upgrades (TechTreeSO, UpgradeSO)
- Reflection-based modifier system for unit improvements

**UI & Input**:
- Event-driven UI updates via Bus system
- Input handling through Unity's Input System

## Build & Usage
The project is designed to be opened and run in the Unity Editor matching the project's Unity version (2020+). No custom build scripts are included in the repository.

**Development Workflow**:
```bash
# Open project in Unity Editor
# Make code changes
# Let Unity compile
# Use Play mode to test changes
```

## Testing
Testing is supported through Unity's Test Framework (com.unity.test-framework 1.4.6), though no specific test files were identified in the repository scan.