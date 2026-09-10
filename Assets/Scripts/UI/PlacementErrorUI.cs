using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Shows placement / play failures next to the problem (world or screen),
    /// not under the top status HUD.
    /// </summary>
    public class PlacementErrorUI : MonoBehaviour
    {
        private const float Lifetime = 4.5f;
        private const float RiseSpeed = 0.55f;

        private static PlacementErrorUI instance;

        private Canvas overlayCanvas;
        private RectTransform panelRt;
        private TextMeshProUGUI label;
        private Image panelBg;
        private float hideAt;
        private Vector3? followWorld;
        private Vector3 driftOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<PlacementErrorUI>() != null) return;
            var go = new GameObject("PlacementErrorUI");
            go.AddComponent<PlacementErrorUI>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            instance = this;
            EnsureUi();
        }

        private void OnEnable()
        {
            ExplorationManager.OnExplorationFailed += HandleFailed;
            ExplorationManager.OnPlacementFailed += HandlePlacementFailed;
        }

        private void OnDisable()
        {
            ExplorationManager.OnExplorationFailed -= HandleFailed;
            ExplorationManager.OnPlacementFailed -= HandlePlacementFailed;
        }

        private void Update()
        {
            if (panelRt == null || !panelRt.gameObject.activeSelf) return;
            if (Time.unscaledTime >= hideAt)
            {
                panelRt.gameObject.SetActive(false);
                followWorld = null;
                return;
            }

            driftOffset += Vector3.up * (RiseSpeed * Time.unscaledDeltaTime);
            PositionPanel();
        }

        private void HandleFailed(string message) => Show(message, null);

        private void HandlePlacementFailed(string message, Vector3 worldPos) => Show(message, worldPos);

        public static void Show(string message, Vector3? worldPos)
        {
            if (instance == null)
            {
                var go = new GameObject("PlacementErrorUI");
                instance = go.AddComponent<PlacementErrorUI>();
                DontDestroyOnLoad(go);
            }
            instance.ShowInternal(message, worldPos);
        }

        private void ShowInternal(string message, Vector3? worldPos)
        {
            EnsureUi();
            if (label == null || panelRt == null) return;

            label.text = message ?? string.Empty;
            followWorld = worldPos;
            driftOffset = Vector3.up * 1.2f;
            hideAt = Time.unscaledTime + Lifetime;
            panelRt.gameObject.SetActive(true);
            PositionPanel();
        }

        private void PositionPanel()
        {
            if (panelRt == null) return;
            Camera cam = Camera.main;
            Vector2 screen;

            if (followWorld.HasValue && cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(followWorld.Value + driftOffset);
                if (sp.z < 0.1f)
                {
                    // Behind camera — fall back to lower-center HUD.
                    screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.28f);
                }
                else
                {
                    screen = new Vector2(sp.x, sp.y);
                }
            }
            else
            {
                // Clear of top resource strip / objectives.
                screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.30f);
            }

            // Keep on-screen with padding.
            float pad = 24f;
            screen.x = Mathf.Clamp(screen.x, pad, Screen.width - pad);
            screen.y = Mathf.Clamp(screen.y, pad, Screen.height - pad - 120f);

            panelRt.position = screen;
        }

        private void EnsureUi()
        {
            if (panelRt != null) return;

            var canvasGo = new GameObject("PlacementErrorCanvas");
            canvasGo.transform.SetParent(transform, false);
            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 5000;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("PlacementErrorPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.sizeDelta = new Vector2(520f, 90f);

            panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.55f, 0.08f, 0.08f, 0.92f);
            panelBg.raycastTarget = false;

            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.45f, 0.35f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14f, 10f);
            trt.offsetMax = new Vector2(-14f, -10f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22f;
            label.color = Color.white;
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            panelGo.SetActive(false);
        }
    }
}
