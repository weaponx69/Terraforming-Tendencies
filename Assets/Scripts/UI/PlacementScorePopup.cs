using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>Combolands-style floating +score number at a world placement.</summary>
    public class PlacementScorePopup : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private float life = 1.15f;
        private float age;
        private Vector3 worldPos;
        private Vector3 drift;
        private Camera cam;

        public static void Spawn(Vector3 worldPosition, int score)
        {
            if (score == 0) return;
            var go = new GameObject("PlacementScorePopup");
            var popup = go.AddComponent<PlacementScorePopup>();
            popup.Init(worldPosition, score);
        }

        private void Init(Vector3 worldPosition, int score)
        {
            worldPos = worldPosition + Vector3.up * 4f;
            drift = Vector3.up * 6f;
            cam = Camera.main;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4500;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var textGo = new GameObject("Score");
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 48f);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 0.92f, 0.35f, 1f);
            label.text = score > 0 ? $"+{score}" : score.ToString();
            label.raycastTarget = false;

            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
            {
                if (tmp != null && tmp.font != null)
                {
                    label.font = tmp.font;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / life);
            worldPos += drift * Time.unscaledDeltaTime;

            if (cam == null) cam = Camera.main;
            if (cam != null && label != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(worldPos);
                if (screen.z < 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                label.rectTransform.position = screen;
                Color c = label.color;
                c.a = 1f - t;
                label.color = c;
                label.fontSize = Mathf.Lerp(32f, 18f, t);
            }

            if (age >= life)
                Destroy(gameObject);
        }
    }
}
