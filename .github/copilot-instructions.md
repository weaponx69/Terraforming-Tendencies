# Copilot instructions for Terriforming Tendencies (Unity RTS)

This file gives concise, actionable context for automated coding agents to be productive in this Unity project.

- Project type: Unity 2020+ style C# RTS project using ScriptableObjects for data (SO suffix), a small EventBus (`Assets/Scripts/EventBus/Bus.cs`), and Behavior Graph / blackboard driven unit AI (`Assets/Scripts/Behavior/*`, `BehaviorGraphAgent`).
- Primary code areas:
  - Units and gameplay: `Assets/Scripts/Units/` (key: `AbstractUnit.cs`, `UnitSO.cs`, `AttackConfigSO.cs`)
  - AI behaviors: `Assets/Scripts/Behavior/` (look at `AttackTargetAction.cs`, `MoveToTargetGameObjectAction.cs`, `GatherSuppliesAction.cs`)
  - Event system: `Assets/Scripts/EventBus/Bus.cs` and event definitions under `Assets/Scripts/Events/`
  - UI: `Assets/Scripts/UI/` (event-driven via the Bus)
  - Tech/upgrades: `Assets/Scripts/TechTree/` (uses reflection-style modifiers like `UpgradeSO`)

Essentials an agent must know (quick):

1) Blackboards & BehaviorGraphAgent
  - Units use a `BehaviorGraphAgent` that exposes blackboard variables (see `AbstractUnit.cs` Awake/Move/Attack methods). Set variables with `graphAgent.SetVariableValue("Name", value)` and read with `GetVariable`.
  - Common blackboard names: `Command`, `TargetGameObject`, `TargetLocation`, `AttackConfig`, `NearbyEnemies`.
  - Behavior nodes in `Assets/Scripts/Behavior/` assume these variables exist — change both node and the code that sets variables together.

2) ScriptableObjects as authoritative data
  - Unit and attack configuration live in SOs (`UnitSO.cs`, `AttackConfigSO.cs`). Code copies/clones SOs at runtime (see `AbstractCommandable.cs` Awake) — don't modify shared SOs at runtime unless cloning is intended.
  - Upgrades apply changes to SOs (see `TechTree/UpgradeSO.cs`) and rely on reflection to target nested SO fields.

3) EventBus patterns
  - Use `Bus<T>.Raise(owner, event)` for firing and `Bus<T>.OnEvent[owner] += handler` or `Bus<T>.RegisterForAll` to subscribe. Many UI and system updates rely on these events (see `Player/PlayerInput.cs`, `UI/RuntimeUI.cs`).
  - When adding a new event type, add its event definition under `Assets/Scripts/Events/` and follow existing usage patterns.

4) Movement, NavMesh and Animations
  - Units require `NavMeshAgent` and animator interaction is via `AnimationConstants` (see `Assets/Scripts/Utilities/AnimationConstants.cs`). Keep animator parameter names and hashes in sync.

5) Concurrency and lifetime
  - Many components subscribe to Bus events in Start/Awake and unregister in OnDestroy. Mirror this in new components to avoid memory leaks.

Repository conventions & gotchas
  - Filenames and types follow GameDevTV naming (e.g., `AbstractUnit`, `UnitSO`). Look for `SO` suffix for ScriptableObjects and `Event` suffix for event types.
  - Use `TryGetComponent` when checking for interfaces (e.g., `IDamageable`) — many behaviors rely on components implementing interfaces rather than concrete types.
  - Physics queries often use `Physics.OverlapSphereNonAlloc` with pre-sized arrays (see `AttackTargetAction.cs`) — preserve allocation patterns when editing for performance.

Build / run / debug notes
  - Project is a Unity project; open in the Unity Editor that matches the project's Unity version. No build scripts in repo. Use the Unity Editor to compile, run scenes, and inspect GameObjects.
  - Quick code iteration: edit C# script, let Unity compile, then use Play mode. For headless runs or CI, export via Unity CLI (not included here).

Examples to reference when making changes
  - To set a unit moving: `AbstractUnit.MoveTo(Vector3)` which sets `TargetLocation` and `Command` on the BehaviorGraphAgent.
  - To apply damage within behavior node: `AttackTargetAction.ApplyDamage()` uses `IDamageable.TakeDamage(...)` and respects AoE via `Physics.OverlapSphereNonAlloc`.
  - To add a UI listener: subscribe to `Bus<SupplyEvent>.OnEvent[owner]` similar to `UI/RuntimeUI.cs`.

Editing guidance for agents
  - When modifying behavior nodes (`Assets/Scripts/Behavior/*`) update or search for all usages of the blackboard variable names; unit setup often happens in `AbstractUnit`.
  - Prefer non-allocating APIs (NonAlloc overloads) where existing code does. Maintain animator hash usage via `AnimationConstants`.
  - Keep ScriptableObject mutation explicit: clone SOs where the code currently clones (see `AbstractCommandable.cs`).

If something is missing or ambiguous, examine these files first:
  - `Assets/Scripts/Units/AbstractUnit.cs`
  - `Assets/Scripts/Units/UnitSO.cs`
  - `Assets/Scripts/Behavior/AttackTargetAction.cs`
  - `Assets/Scripts/EventBus/Bus.cs`
  - `Assets/Scripts/TechTree/UpgradeSO.cs`

Ask for guidance: If you plan a change that affects runtime data flow (blackboard variables, events, or SO shapes), request a short design note explaining where you'd update blackboard assignments, what events you'll raise, and how SOs will be updated.

---
If you'd like, I can shorten this to a 10-line quick cheat-sheet or expand specific sections (build/CI, tests, or adding events/blackboard variables).
