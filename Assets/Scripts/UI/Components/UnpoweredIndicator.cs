using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Automatically monitors a building's power status and displays a visual indicator 
    /// (such as a floating icon or text) when the building is unpowered.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class UnpoweredIndicator : MonoBehaviour
    {
        [Tooltip("The GameObject representing the 'unpowered' visual indicator (e.g., a floating sprite/canvas).")]
        [SerializeField] private GameObject visualIndicator;

        [Tooltip("Height offset above the building's pivot to position the indicator.")]
        [SerializeField] private float heightOffset = 4f;

        private BaseBuilding building;
        private PowerNode powerNode;
        private BuildingProgress.BuildingState lastState = BuildingProgress.BuildingState.Building;
        private bool lastPoweredState = false;

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

            EnsurePowerNodeCached();

            lastState = building.Progress.State;
            lastPoweredState = (powerNode != null && powerNode.IsPowered);

            // Start in the correct state
            UpdateIndicatorState();
        }

        private void EnsurePowerNodeCached()
        {
            if (powerNode == null)
            {
                powerNode = GetComponent<PowerNode>();
                if (powerNode != null)
                {
                    powerNode.OnPowerStateChanged += HandlePowerStateChanged;
                }
            }
        }

        private void OnDestroy()
        {
            if (powerNode != null)
            {
                powerNode.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        private void HandlePowerStateChanged(bool isPowered)
        {
            UpdateIndicatorState();
        }

        private void Update()
        {
            EnsurePowerNodeCached();

            BuildingProgress.BuildingState currentState = building.Progress.State;
            bool currentPowered = (powerNode != null && powerNode.IsPowered);

            if (currentState != lastState || currentPowered != lastPoweredState)
            {
                lastState = currentState;
                lastPoweredState = currentPowered;
                UpdateIndicatorState();
            }
        }

        private void UpdateIndicatorState()
        {
            if (visualIndicator == null) return;

            EnsurePowerNodeCached();

            // A building is considered 'unpowered' if:
            // 1. It is fully constructed (not a ghost/under construction).
            // 2. Its config requires power (PowerUpkeep > 0).
            // 3. The PowerNode indicates it is not powered.
            bool needsPower = building.BuildingSO != null && 
                              building.BuildingSO.BuildingConfig != null && 
                              building.BuildingSO.BuildingConfig.PowerUpkeep > 0;

            bool isUnpowered = building.Progress.State == BuildingProgress.BuildingState.Completed && 
                               needsPower && 
                               (powerNode == null || !powerNode.IsPowered);

            visualIndicator.SetActive(isUnpowered);
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
            GameObject container = new GameObject("UnpoweredIndicator_Container");
            container.transform.SetParent(transform, false);
            
            // Set position dynamically above the top of the building
            container.transform.localPosition = Vector3.up * (GetBuildingHeight() + 2.0f);

            // Set up a billboard Canvas
            Canvas canvas = container.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            // Set a small, polished scale for World Space UI (0.015 is crisp and standard)
            container.transform.localScale = Vector3.one * 0.015f;

            // Make it face the camera using our existing FaceCamera utility
            container.AddComponent<FaceCamera>();

            // Create a Text GameObject
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(container.transform, false);
            
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = "⚡ NO POWER";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.red;

            // Style layout
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 50f);

            visualIndicator = container;
            visualIndicator.SetActive(false);
        }
    }
}