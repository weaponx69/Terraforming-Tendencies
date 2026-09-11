using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// On-screen sector names (clickable) + Q/E prev/next + current sector readout.
    /// </summary>
    public class SectorTravelUI : MonoBehaviour
    {
        public static SectorTravelUI Instance { get; private set; }

        private readonly List<SectorLabel> labels = new();
        private TextMeshProUGUI currentSectorText;
        private Canvas overlayCanvas;
        private Camera cam;
        private int lastSectorCount = -1;

        private struct SectorLabel
        {
            public int Index;
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public Button Button;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<SectorTravelUI>() != null) return;
            var go = new GameObject("SectorTravelUI");
            go.AddComponent<SectorTravelUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureUi();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureUi()
        {
            if (overlayCanvas != null) return;

            var canvasGo = new GameObject("SectorTravelCanvas");
            canvasGo.transform.SetParent(transform, false);
            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 40;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            TMP_FontAsset font = null;
            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
            {
                if (tmp?.font != null)
                {
                    font = tmp.font;
                    break;
                }
            }

            // Current sector HUD (top-center).
            var hudGo = new GameObject("CurrentSector");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRt = hudGo.AddComponent<RectTransform>();
            hudRt.anchorMin = new Vector2(0.5f, 1f);
            hudRt.anchorMax = new Vector2(0.5f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.sizeDelta = new Vector2(420f, 36f);
            hudRt.anchoredPosition = new Vector2(0f, -86f);
            currentSectorText = hudGo.AddComponent<TextMeshProUGUI>();
            if (font != null) currentSectorText.font = font;
            currentSectorText.fontSize = 18f;
            currentSectorText.alignment = TextAlignmentOptions.Center;
            currentSectorText.color = new Color(0.75f, 0.9f, 1f, 0.95f);
            currentSectorText.raycastTarget = false;

            // Prev / Next buttons.
            CreateNavButton(canvasGo.transform, font, "◀ Q", new Vector2(-230f, -86f), () =>
            {
                PlayerInput.Instance?.PageSectors(-1);
            });
            CreateNavButton(canvasGo.transform, font, "E ▶", new Vector2(230f, -86f), () =>
            {
                PlayerInput.Instance?.PageSectors(1);
            });
        }

        private static void CreateNavButton(Transform parent, TMP_FontAsset font, string label, Vector2 anchored, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Nav_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(72f, 32f);
            rt.anchoredPosition = anchored;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        private void LateUpdate()
        {
            EnsureUi();
            if (cam == null) cam = Camera.main;
            RebuildLabelsIfNeeded();
            UpdateLabelPositions();
            UpdateCurrentSectorReadout();
        }

        private void RebuildLabelsIfNeeded()
        {
            var sm = SectorManager.Instance;
            int count = sm?.Sectors?.Count ?? 0;
            if (count == lastSectorCount && labels.Count == count) return;
            lastSectorCount = count;

            foreach (var label in labels)
            {
                if (label.Rect != null) Destroy(label.Rect.gameObject);
            }
            labels.Clear();
            if (count <= 0 || overlayCanvas == null) return;

            TMP_FontAsset font = currentSectorText != null ? currentSectorText.font : null;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var go = new GameObject($"SectorLabel_{i}");
                go.transform.SetParent(overlayCanvas.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(180f, 28f);
                var img = go.AddComponent<Image>();
                img.color = new Color(0.04f, 0.06f, 0.1f, 0.72f);
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SectorColonization.FocusCameraOnSector(index));

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(6f, 2f);
                textRt.offsetMax = new Vector2(-6f, -2f);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.fontSize = 13f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.85f, 0.92f, 1f, 1f);
                tmp.raycastTarget = false;
                tmp.text = SectorColonization.GetSectorDisplayName(index);

                labels.Add(new SectorLabel { Index = index, Rect = rt, Text = tmp, Button = btn });
            }
        }

        private void UpdateLabelPositions()
        {
            if (cam == null || labels.Count == 0) return;
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;

            for (int i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                if (label.Index >= sm.Sectors.Count || label.Rect == null) continue;
                var sector = sm.Sectors[label.Index];
                if (sector == null) continue;

                Vector3 world = sector.Center + Vector3.up * 8f;
                if (SectorColonization.TryGetCommandPostFocusPosition(sector, out Vector3 focus))
                    world = focus + Vector3.up * 8f;

                Vector3 screen = cam.WorldToScreenPoint(world);
                bool visible = screen.z > 0f
                    && screen.x > -40f && screen.x < Screen.width + 40f
                    && screen.y > -40f && screen.y < Screen.height + 40f;
                label.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                label.Rect.position = screen;
                if (label.Text != null)
                    label.Text.text = SectorColonization.GetSectorDisplayName(label.Index);
            }
        }

        private void UpdateCurrentSectorReadout()
        {
            if (currentSectorText == null) return;
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null || sm.Sectors.Count == 0)
            {
                currentSectorText.text = string.Empty;
                return;
            }

            var nearest = sm.GetNearestSector(PlayerInput.GetCameraFocusPosition());
            int idx = nearest != null ? sm.Sectors.IndexOf(nearest) : 0;
            currentSectorText.text = SectorColonization.GetSectorDisplayName(Mathf.Max(0, idx)) + "   <size=70%>(Q / E)</size>";
        }
    }
}

