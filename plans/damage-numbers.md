# Damage Number (Juicy Subtraction Sign) Plan

## Overview
A floating "-X" text that appears over commandables when they take damage, floats upward, and fades out.

## Files to Create

### 1. `Assets/Scripts/UI/Components/DamageNumberUI.cs` — NEW
A component that controls the floating animation:
- Stores the damage amount
- Animates upward (Vector3.up * speed) over ~1 second
- Fades alpha from 1 to 0
- Destroys itself (or returns to pool) when animation completes
- Configurable: float speed, fade speed, random horizontal offset

```csharp
public class DamageNumberUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float floatSpeed = 1.5f;
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float randomOffsetRange = 0.5f;

    private float elapsed;
    private CanvasGroup canvasGroup;

    public void Show(int damage, Vector3 worldPosition)
    {
        // Position with random horizontal offset
        Vector3 offset = new Vector3(
            Random.Range(-randomOffsetRange, randomOffsetRange),
            0,
            Random.Range(-randomOffsetRange, randomOffsetRange)
        );
        transform.position = worldPosition + offset;

        label.text = $"-{damage}";
        elapsed = 0f;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / fadeDuration;

        // Float upward
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Fade out
        canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
```

### 2. `Assets/Prefabs/UI/DamageNumber.prefab` — NEW
A world-space Canvas prefab with:
- **Root**: GameObject with `DamageNumberUI` component
- **Child**: `Canvas` (World Space, 100x30 size)
  - **Child**: `TextMeshPro - Damage` — large bold font, red color, centered alignment

## File to Modify

### 3. `Assets/Scripts/Units/AbstractCommandable.cs` — MODIFY
In `TakeDamage()` (line 142), add a spawn call after applying damage:

**After line 147** (`CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, CurrentHealth);`):
```csharp
// Spawn floating damage number
DamageNumberUI.Spawn(transform.position + Vector3.up * heightOffset, damage);
```

## Optional: Simple Object Pool
Since damage numbers are spawned frequently, a lightweight pool prevents GC spikes. Can be added as a static `DamageNumberUI.Spawn()` that reuses instances.

## Visual Polish Ideas
- **Color by severity**: Small damage = white, medium = yellow, large = red
- **Critical hit**: "CRITICAL!" text + larger scale + screen shake
- **Font size scaling**: Bigger numbers for bigger damage
- **Rounded corners**: Damage values like "0.5" or "2.5" for decay damage

## Execution Order
1. Create `DamageNumberUI.cs` script
2. Create `DamageNumber.prefab` with the script + TextMeshPro + Canvas
3. Modify `AbstractCommandable.TakeDamage()` to spawn the prefab
4. Test with decay damage, combat, and natural events