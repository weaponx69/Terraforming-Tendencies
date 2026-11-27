using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>World labels stay readable when zoomed out; scale with camera distance.</summary>
    public class MainGameBillboardLabel : MonoBehaviour
    {
        [SerializeField] float referenceDistance = 42f;
        [SerializeField] float worldScale = 1.15f;
        [SerializeField] float minWorldScale = 0.75f;
        [SerializeField] float maxWorldScale = 4.5f;

        float verticalJitter;

        /// <summary>When false, only scale/rotate (for ? markers that already set world position).</summary>
        public bool AnchorToGround = true;

        public void SetVerticalJitter(float meters) => verticalJitter = meters;

        public void SetReadability(float refDistance, float minScale, float scale = 1.65f)
        {
            referenceDistance = refDistance;
            minWorldScale = minScale;
            worldScale = scale;
        }

        public static MainGameBillboardLabel Ensure(GameObject host)
        {
            if (host == null) return null;
            if (!host.TryGetComponent(out MainGameBillboardLabel label))
                label = host.AddComponent<MainGameBillboardLabel>();
            return label;
        }

        /// <summary>Readable world-space TMP toward the gameplay camera.</summary>
        public static void FaceWorldTmp(Transform target, Camera cam)
        {
            if (target == null || cam == null) return;
            target.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }

        /// <summary>Canvas / non-TMP labels: match camera bearing.</summary>
        public static void ApplyScreenAligned(Transform target, Camera cam)
        {
            if (target == null || cam == null) return;
            target.rotation = cam.transform.rotation;
        }

        void LateUpdate()
        {
            if (!MainGameController.IsActive) return;
            var cam = PlayerInput.Instance != null ? PlayerInput.Instance.GameplayCamera : Camera.main;
            if (cam == null) return;

            if (AnchorToGround && transform.parent == null)
            {
                Vector3 basePos = transform.position;
                float groundY = ColonyTileGrid.SampleGroundY(basePos);
                Vector3 p = transform.position;
                p.y = groundY + verticalJitter;
                transform.position = p;
            }

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float scale = Mathf.Clamp(dist / referenceDistance * worldScale, minWorldScale, maxWorldScale);
            transform.localScale = Vector3.one * scale;

            if (TryGetComponent<MainGameWorldQuestionLabel>(out _))
                return;
            transform.rotation = cam.transform.rotation;
        }
    }
}