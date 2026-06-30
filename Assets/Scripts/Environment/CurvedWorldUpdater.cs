using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)] // Ensure this runs AFTER Cinemachine's LateUpdate
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public class CurvedWorldUpdater : MonoBehaviour
    {
#if UNITY_EDITOR
        static CurvedWorldUpdater()
        {
            // Disable shader curvature in Edit Mode to keep placement visible
            Shader.SetGlobalFloat("_CurveStrength", 0f);
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode || 
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                Shader.SetGlobalFloat("_CurveStrength", 0f);
            }
        }
#endif

        [Header("Curved World Settings")]
        [Tooltip("How strongly the world bends downwards. Recommended: 0.001 - 0.005")]
        public float CurveStrength = 0.003f;
        
        [Tooltip("Use this transform as the top of the sphere. If null, uses the Main Camera.")]
        public Transform CurveOrigin;

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Shader.SetGlobalFloat("_CurveStrength", 0f);
                return;
            }
#endif

            Transform origin = CurveOrigin;

            if (origin == null)
            {
                if (Camera.main != null)
                {
                    origin = Camera.main.transform;
                }
                else
                {
                    return; // No camera available, do nothing
                }
            }

            // Pass the data to the shader globally
            Shader.SetGlobalVector("_CurveOrigin", origin.position);
            Shader.SetGlobalFloat("_CurveStrength", CurveStrength);
        }
    }
}
