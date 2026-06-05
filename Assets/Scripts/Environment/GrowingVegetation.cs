using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class GrowingVegetation : MonoBehaviour
    {
        [SerializeField] private float growthDuration = 30f; // Default to 30 seconds for better feedback
        [SerializeField] private Vector3 targetScale = Vector3.one;
        
        public float GrowthProgress { get => growthProgress; set => growthProgress = value; }
        private float growthProgress = 0f;
        private Vector3 initialScale;

        public void SetDuration(float duration)
        {
            growthDuration = duration;
        }

        public void SetTargetScale(Vector3 scale)
        {
            targetScale = scale;
        }

        public void ApplyColorTint(Color color)
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                Material mat = r.material; // Instance
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    // Multiply color to ensure it glows and drowns out the texture's yellow
                    mat.SetColor("_EmissionColor", color * 1.5f);
                }
            }
        }

        private void Start()
        {
            // Randomize duration slightly for variety
            growthDuration *= Random.Range(0.8f, 1.2f);
            
            // Apply a subtle scale variety (±20%) instead of the massive multipliers
            targetScale *= Random.Range(0.8f, 1.2f);
            
            // If we haven't set progress yet, start at zero
            if (growthProgress <= 0)
                transform.localScale = Vector3.zero;
            else
                transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, growthProgress);
        }

        private static VegetationManager _managerInstance;
        private static VegetationManager Manager
        {
            get
            {
                if (_managerInstance == null) _managerInstance = Object.FindAnyObjectByType<VegetationManager>();
                return _managerInstance;
            }
        }

        private void Update()
        {
            float multiplier = 1f;
            var vm = Manager;
            
            if (vm != null && vm.useManualGrowthControl)
            {
                growthProgress = vm.manualGrowthProgress;
                transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, growthProgress);
                return;
            }

            if (growthProgress < 1f)
            {
                if (vm != null) multiplier = vm.globalGrowthMultiplier;

                growthProgress += (Time.deltaTime * multiplier) / growthDuration;
                transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, growthProgress);
            }
            else
            {
                // If not manual control, we can disable the script to save CPU
                if (vm == null || !vm.useManualGrowthControl)
                    enabled = false;
            }
        }
    }
}
