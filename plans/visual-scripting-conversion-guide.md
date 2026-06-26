# Unity Visual Scripting Conversion Pipeline — Reusable Guide

> Derived from the calibration run on `DepletionVisuals.cs` and `GatherableSupply.cs`.
> Apply this pattern to every file in the categorized roadmap.

---

## Phase 0 — One-Time Project Setup (Already Done)

- [x] [`VisualScriptingAttributes.cs`](Assets/Scripts/VisualScriptingAttributes.cs) — permanent runtime stub, no `#if` guards
- [x] [`MainGame.asmdef`](Assets/Scripts/MainGame.asmdef) — references `Unity.VisualScripting.Core`, `.Flow`, `.State`
- [x] `ProjectSettings/VisualScriptingSettings.asset` — `MainGame` in `assemblyOptions`

---

## Phase 1 — C# Attribute Injection (per file)

### Step 1.1 — Read the file

Open the target `.cs` file. Identify:
- **Heavy logic** that stays in C#: `Mathf.*`, `for`/`foreach` loops, `Destroy()` cascades, `WorldToScreenPoint`, procedural mesh generation, coroutine state machines
- **Public surface** to expose to VS: `[SerializeField]` fields, computed properties, public methods that act as atomic operations

### Step 1.2 — Add the using directive

```csharp
using Unity.VisualScripting;
```

Place it after the last existing `using` line. No `#if` guards — the stub handles fallback.

### Step 1.3 — Decorate the class

```csharp
[IncludeInSettings(true)]
public class YourClass : MonoBehaviour
```

### Step 1.4 — Decorate fields and properties

```csharp
[Inspectable]
[field: SerializeField] public int SomeValue { get; set; }

[Inspectable]
public float ComputedRatio => (max > 0) ? (float)current / max : 1f;

[Inspectable]
public bool IsExhausted => current <= 0;
```

### Step 1.5 — Decorate public methods

```csharp
/// <summary>Brief description for the VS node tooltip.</summary>
[Inspectable]
public void DoAtomicOperation() { /* heavy logic stays here */ }
```

### Step 1.6 — Verify compilation

Check the Unity console for zero `CS0246` errors before proceeding.

---

## Phase 2 — VS Type Registration (per file, only if not auto-discovered)

If the type doesn't appear in the VS fuzzy search after `UnitBase.Rebuild()`, register it programmatically:

```csharp
// Run via Unity_RunCommand
using Unity.VisualScripting;

var cfg = BoltCore.Configuration;
var myType = typeof(GameDevTV.RTS.Environment.YourClass);

if (!cfg.typeOptions.Contains(myType))
    cfg.typeOptions.Add(myType);

cfg.Save();
UnitBase.Rebuild();
```

---

## Phase 3 — Flow Graph Specification (per file)

Create a companion `.graph` file in the same folder as the C# script. Document:

1. **Architecture contract** — what stays in C# (never replicated in nodes)
2. **Node map** — ASCII diagram of the graph topology
3. **Custom events** — names and suggested listeners
4. **Machine setup** — which GameObject gets the ScriptMachine component

Template:

```
# YourClass_Flow.graph — Node Specification
#
# ARCHITECTURE CONTRACT:
#   [list what stays in C#]
#
# NODE MAP:
#   [Update] → [GetComponent<YourClass>] → [GetMember: X] → [Branch] → ...
#
# CUSTOM EVENTS:
#   "OnSomethingHappened" → [suggested reaction]
#
# MACHINE SETUP:
#   ScriptMachine on [prefab name] root, Source = Graph
```

---

## Phase 4 — Verification Checklist

- [ ] `using Unity.VisualScripting;` present, no `#if` guards
- [ ] `[IncludeInSettings(true)]` on the class
- [ ] `[Inspectable]` on all public fields, computed properties, and key methods
- [ ] Heavy math/loops untouched in C#
- [ ] Zero `CS0246` errors in console
- [ ] Type appears in VS Type Options (`Edit > Project Settings > Visual Scripting`)
- [ ] Members appear in VS fuzzy search (search by member name, not type name)
- [ ] `.graph` spec file written in same folder

---

## Quick Reference — Decorated Files So Far

| File | `[Inspectable]` Members |
|------|------------------------|
| [`DepletionVisuals.cs`](Assets/Scripts/Environment/DepletionVisuals.cs) | `minScaleFactor`, `maxScaleFactor`, `DepletionRatio` |
| [`GatherableSupply.cs`](Assets/Scripts/Environment/GatherableSupply.cs) | `Supply`, `Amount`, `IsBusy`, `IsVisible`, `DepletionRatio`, `IsExhausted`, `BeginGather()`, `EndGather()`, `AbortGather()`, `SetVisible()`, `ToggleColliders()` |

---

## Next Files in Queue (from Roadmap)

### 🟩 Component / Triggers — Environment

| Priority | File | Key surface to expose |
|----------|------|----------------------|
| 1 | `PowerNode.cs` | `IsPowered`, `ConnectedGrids`, `PowerDraw` |
| 2 | `LifeSupportNode.cs` | `Radius`, `IsOperational`, `DecayProtectionActive` |
| 3 | `PipelineSegment.cs` | `IsBuilt`, `Progress`, `ConnectedCrawler` |
| 4 | `BatteryNode.cs` | `StoredPower`, `Capacity`, `ChargeRate` |
| 5 | `HiddenResource.cs` | `IsDiscovered`, `DiscoveryProgress` |
| 6 | `GrowingVegetation.cs` | `BiomassContribution`, `GrowthStage` |
| 7 | `NaturalEventImpact.cs` | `ImpactRadius`, `DamagePerSecond` |
| 8 | `MapWrapper.cs` | `WrapRadius`, `IsWrapping` |

### 🟦 State / Managers

| Priority | File | Key surface to expose |
|----------|------|----------------------|
| 9 | `GenerationManager.cs` | `CurrentGeneration`, `IsExpansionPhase`, `MilestoneProgress` |
| 10 | `SectorManager.cs` | `UnlockedSectors`, `ActiveSector` |
| 11 | `Supplies.cs` | `Materials`, `Biomass`, `Power`, `Oxygen`, `Population` |
| 12 | `PowerGridManager.cs` | `TotalGeneration`, `TotalUpkeep`, `NetSurplus` |