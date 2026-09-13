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
            if (camTransform == null && Camera.main != null)
                camTransform = Camera.main.transform;

            if (camTransform != null)
            {
                // Screen-aligned billboard — same orientation as the camera, always faces the player.
                transform.rotation = camTransform.rotation;
            }
        }
}
}
