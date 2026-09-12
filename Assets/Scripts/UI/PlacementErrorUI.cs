using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Shows placement / play failures as a bright on-screen toast
    /// (and mirrors to the Colony Acts status banner).
    /// </summary>
    public class PlacementErrorUI : MonoBehaviour
    {
        private const float Lifetime = 5f;

        private static PlacementErrorUI instance;

        private Canvas overlayCanvas;
        private RectTransform panelRt;
        private TextMeshProUGUI label;
        private Image panelBg;
        private float hideAt;

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
            }
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
            hideAt = Time.unscaledTime + Lifetime;
            panelRt.gameObject.SetActive(true);
            PositionPanel();

            ColonyActManager.Instance?.ShowStatusBanner($"<color=#ffccaa>{message}</color>", 4.5f);
        }

        private void PositionPanel()
        {
            if (panelRt == null) return;
            // Fixed lower-center — always readable regardless of click world position.
            panelRt.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.32f);
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
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(640f, 110f);

            panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.55f, 0.08f, 0.08f, 0.95f);
            panelBg.raycastTarget = false;

            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.45f, 0.35f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 12f);
            trt.offsetMax = new Vector2(-16f, -12f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.color = Color.white;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            panelGo.SetActive(false);
        }
    }
}
