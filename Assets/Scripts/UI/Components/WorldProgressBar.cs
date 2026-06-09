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
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(progress);
            }

            if (container != null)
            {
                bool active = progress > 0 && progress < 1.0f;
                if (container.activeSelf != active) container.SetActive(active);
            }
        }
    }
}
