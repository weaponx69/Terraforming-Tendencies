# Terriforming Tendencies Project Knowledge

## Health Bar System
- **Purpose**: Visual representation of colony integrity (health) in the UI
- **Components**:
  - `HealthBar.cs`: C# script managing health percentage calculation
  - `ProgressBar.prefab`: UI element displaying the health bar
  - `HealthBarContainer.prefab`: Container for positioning the health bar in the UI
- **Integration**:
  - Health percentage calculated via `currentHealth / maxHealth`
  - Progress bar updates in real-time through `SetProgress()` method

## Visual Scripting Implementation
- **HealthBarLogic.asset**: Visual script node for health bar logic
- **Key Nodes**:
  - `ExposedReference` for health bar object and health values
  - `OnBehaviourPlay` method for real-time updates
  - `UnityEngine.UI.Image` component for visual representation

## Testing
- **Test Case**: Verify health percentage updates when `currentHealth` changes
- **Expected Behavior**: Progress bar reflects current integrity percentage accurately

## Notes
- Health bar is positioned in the UI container on the left side
- Progress bar uses `fillAmount` for percentage display
- Visual script nodes are connected to the Progress Bar prefab

## Dependencies
- Requires Unity UI Toolkit
- Uses `UnityEngine.UI.Image` component for rendering
- Visual script nodes must be assigned in the Unity Editor

## Integrity Bar ↔ DecayStarter Wiring

### Full Event Chain
1. **`DecayStarter.Update()`** fires every 0.1s → calls `TakeDamage(5)` on itself (500 max HP) → calls `Supplies.UpdateIntegrity(Owner, ratio * 100f)` where ratio = `CurrentHealth / MaxHealth`
2. **`Supplies.UpdateIntegrity()`** clamps to 0 and fires `Supplies.OnIntegrityChanged`
3. **`ColonyIntegrityBar.HandleIntegrityChanged()`** sets `_targetFill = newValue / 100f`
4. **`ColonyIntegrityBar.Update()`** lerps `_currentFill` → `_targetFill` and sets `fillImage.fillAmount` + applies color transitions

### Bug Fixed (2026-06-30)
`SurvivalManager.SurvivalLoop()` was calling `Supplies.CalculateIntegrity()` (aggregates ALL commandable HP — mostly full → ~100%) and writing that back every second, **overwriting** `DecayStarter`'s per-tick writes and keeping the bar near 100%.

**Fix**: `SurvivalManager` now drains integrity by `integrityDrainRate * tickRate` from the current value each tick, rather than recalculating from commandable health. This means:
- `DecayStarter` writes drive the bar directly without being overwritten
- `SurvivalManager` provides an additional constant drain on top
- Both drain in the same direction; neither resets

### Key Files
- `Assets/Scripts/Environment/DecayStarter.cs` — ticks integrity down via its own HP ratio
- `Assets/Scripts/UI/Components/ColonyIntegrityBar.cs` — `Filled` Image bar subscribed to `Supplies.OnIntegrityChanged`
- `Assets/Scripts/Player/SurvivalManager.cs` — drain-based loop (NOT recalculate-based)
- `Assets/Scripts/Player/Supplies.cs` — `UpdateIntegrity()` + `OnIntegrityChanged` event

## Version History
- 2026-06-29: Initial implementation of health bar system
- 2026-06-29: Visual script version created for easier maintenance
- 2026-06-30: Fixed SurvivalManager overwriting DecayStarter integrity writes; switched to drain-based approach