using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    public class WorldProgressBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject container;

        public void Setup(Image fill, GameObject cont)
        {
            fillImage = fill;
            container = cont;
        }

        private void Awake()
{
            if (container != null) container.SetActive(false);
        }

        private void Start()
        {
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        private void LateUpdate()
{
            // Face the camera
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }

        public void SetProgress(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (fillImage != null)
            {
                fillImage.fillAmount = clamped;
            }

            if (container != null)
            {
                // Show if progress is significantly above 0
                bool active = clamped > 0.001f;
                if (container.activeSelf != active)
                {
                    container.SetActive(active);
                }
            }
        }
}
}
