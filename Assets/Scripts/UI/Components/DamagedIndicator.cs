using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Automatically monitors a building's health status and displays a visual indicator 
    /// (such as a floating wrench icon or text) when the building is damaged.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class DamagedIndicator : MonoBehaviour
    {
        [Tooltip("The GameObject representing the 'damaged' visual indicator (e.g., a floating sprite/canvas).")]
        [SerializeField] private GameObject visualIndicator;

        [Tooltip("Height offset above the building's pivot to position the indicator.")]
        [SerializeField] private float heightOffset = 4.2f;

        private BaseBuilding building;
        private BuildingProgress.BuildingState lastState = BuildingProgress.BuildingState.Building;
        private int lastHealth = 0;

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();
        }

        private void Start()
        {
            // If no indicator was assigned in the inspector, we can procedurally create a simple one!
            if (visualIndicator == null)
            {
                CreateDefaultIndicator();
            }

            // Subscribe to health changes for reactive updates
            building.OnHealthUpdated += HandleHealthUpdated;

            lastState = building.Progress.State;
            lastHealth = building.CurrentHealth;

            // Start in the correct state
            UpdateIndicatorState();
        }

        private void OnDestroy()
        {
            if (building != null)
            {
                building.OnHealthUpdated -= HandleHealthUpdated;
            }
        }

        private void HandleHealthUpdated(AbstractCommandable commandable, int oldHealth, int newHealth)
        {
            UpdateIndicatorState();
        }

        private void Update()
        {
            if (building == null) return;

            BuildingProgress.BuildingState currentState = building.Progress.State;
            int currentHealth = building.CurrentHealth;

            if (currentState != lastState || currentHealth != lastHealth)
            {
                lastState = currentState;
                lastHealth = currentHealth;
                UpdateIndicatorState();
            }
        }

        private void UpdateIndicatorState()
        {
            if (visualIndicator == null) return;

            // A building is considered 'damaged' if:
            // 1. It is fully constructed.
            // 2. Its current health is strictly less than max health.
            bool isDamaged = building.Progress.State == BuildingProgress.BuildingState.Completed && 
                             building.CurrentHealth < building.MaxHealth;

            visualIndicator.SetActive(isDamaged);
        }

        private float GetBuildingHeight()
        {
            // 1. If it has SmokestackVisuals (procedural height), query its Height
            if (TryGetComponent<SmokestackVisuals>(out var sv))
            {
                return sv.Height;
            }

            float maxHeight = 0f;

            // 2. Check all colliders in children and self
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                float localY = transform.InverseTransformPoint(col.bounds.max).y;
                if (localY > maxHeight)
                {
                    maxHeight = localY;
                }
            }

            // 3. Check all renderers (visual meshes) in children and self
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                // Skip indicators, canvas, or sprite renderers to avoid height inflation
                string nameLower = rend.gameObject.name.ToLower();
                if (nameLower.Contains("indicator") || nameLower.Contains("selection") || rend is SpriteRenderer)
                    continue;

                float localY = transform.InverseTransformPoint(rend.bounds.max).y;
                if (localY > maxHeight)
                {
                    maxHeight = localY;
                }
            }

            if (maxHeight > 0f)
            {
                return maxHeight;
            }

            // 4. Default fallback
            return heightOffset;
        }

        private void CreateDefaultIndicator()
        {
            // Create a child container
            GameObject container = new GameObject("DamagedIndicator_Container");
            container.transform.SetParent(transform, false);
            
            // Set position dynamically above the top of the building (slightly higher than the unpowered indicator to prevent overlapping)
            container.transform.localPosition = Vector3.up * (GetBuildingHeight() + 2.6f);

            // Set up a billboard Canvas
            Canvas canvas = container.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            // Set a small, polished scale for World Space UI
            container.transform.localScale = Vector3.one * 0.015f;

            // Make it face the camera
            container.AddComponent<FaceCamera>();

            // Create a Text GameObject
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(container.transform, false);
            
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = "🔧 REPAIR REQUIRED";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.6f, 0f); // Orange warning color

            // Style layout
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 50f);

            visualIndicator = container;
            visualIndicator.SetActive(false);
        }
    }
}
