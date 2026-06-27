using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Drives per-plant growth animation via Vector3.Lerp in Update().
    /// <para>
    /// Heavy logic (shader/material loops in ApplyColorTint, Update Lerp math,
    /// VegetationManager polling) stays in C#. VS reads <see cref="GrowthProgress"/>
    /// and calls <see cref="SetDuration"/> / <see cref="SetTargetScale"/> as atomic ops.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    public class GrowingVegetation : MonoBehaviour
    {
        [Inspectable]
        [SerializeField] private float growthDuration = 30f;

        [Inspectable]
        [SerializeField] private Vector3 targetScale = Vector3.one;
        
        /// <summary>Normalised growth progress [0, 1]. 1 = fully grown.</summary>
        [Inspectable]
        public float GrowthProgress { get => growthProgress; set => growthProgress = value; }
        private float growthProgress = 0f;
        private Vector3 initialScale;

        /// <summary>Sets the total growth duration in seconds. Callable from a Flow Graph.</summary>
        [Inspectable]
        public void SetDuration(float duration)
        {
            growthDuration = duration;
        }

        /// <summary>Sets the target scale at full growth. Callable from a Flow Graph.</summary>
        [Inspectable]
        public void SetTargetScale(Vector3 scale)
        {
            targetScale = scale;
        }

        /// <summary>
        /// Applies an emission color tint to all child renderers.
        /// Heavy shader/material loops stay in C#.
        /// </summary>
        [Inspectable]
        public void ApplyColorTint(Color color)
        {
            Shader curvedShader = Shader.Find("Custom/URP_CurvedWorld");
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                Material mat = r.material; // Instance
                
                // Swap to curved shader so newly spawned vegetation curves with the planet
                if (curvedShader != null && mat.shader != curvedShader)
                {
                    Texture mainTex = null;
                    if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");
                    if (mainTex == null && mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    if (mainTex == null) mainTex = mat.mainTexture;

                    Color mainColor = Color.white;
                    if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");

                    mat.shader = curvedShader;

                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    mat.SetColor("_BaseColor", mainColor);
                }

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
