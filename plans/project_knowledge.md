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

## Version History
- 2026-06-29: Initial implementation of health bar system
- 2026-06-29: Visual script version created for easier maintenance