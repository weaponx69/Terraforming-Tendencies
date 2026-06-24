using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField] private Vector2 padding = new (9, 8);
        [SerializeField] private RectTransform mask;
        private RectTransform maskParentRectTransform;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (maskParentRectTransform != null) return;
            if (mask == null)
            {
                Debug.LogError($"Progress bar {name} is missing a mask! This progress bar will not work!");
                return;
            }

            maskParentRectTransform = mask.parent != null ? mask.parent.GetComponent<RectTransform>() : null;
        }

        public void SetProgress(float progress)
        {
            EnsureInitialized();
            if (maskParentRectTransform == null || mask == null) return;

            Vector2 parentSize = maskParentRectTransform.sizeDelta;
            Vector2 targetSize = parentSize - padding * 2;

            targetSize.x *= Mathf.Clamp01(progress);

            mask.offsetMin = padding;
            mask.offsetMax = new Vector2(padding.x + targetSize.x - parentSize.x, -padding.y);
        }
    }
}
