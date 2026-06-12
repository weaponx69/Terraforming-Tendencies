using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [ExecuteAlways]
    public class CurvedWorldUpdater : MonoBehaviour
    {
        [Header("Curved World Settings")]
        [Tooltip("How strongly the world bends downwards. Recommended: 0.001 - 0.005")]
        public float CurveStrength = 0.003f;
        
        [Tooltip("Use this transform as the top of the sphere. If null, uses the Main Camera.")]
        public Transform CurveOrigin;

        private void Update()
        {
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
