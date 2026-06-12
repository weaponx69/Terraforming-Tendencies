using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    public class FaceCamera : MonoBehaviour
    {
        private Transform camTransform;

        private void Start()
        {
            if (Camera.main != null)
                camTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (camTransform != null)
            {
                // Smoothly face the camera without flipping
                transform.rotation = camTransform.rotation;
            }
        }
}
}
