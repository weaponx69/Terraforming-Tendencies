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
    /// Focused sector name shows briefly then fades so it does not block the view.
    /// Q/E buttons dock on the left under Weeks Left so they never cover Colony Acts.
    /// </summary>
    public class SectorTravelUI : MonoBehaviour
    {
        public static SectorTravelUI Instance { get; private set; }

        private const float FocusHoldSeconds = 2f;
        private const float FocusFadeSeconds = 0.75f;

        /// <summary>Right HUD (Colony Acts) width + pad — world chips stay left of this.</summary>
        private const float RightHudReservePx = 500f;

        private readonly List<SectorLabel> labels = new();
        private TextMeshProUGUI currentSectorText;
        private CanvasGroup currentSectorGroup;
        private RectTransform currentSectorRt;
        private RectTransform navPrevRt;
        private RectTransform navNextRt;
        private Canvas overlayCanvas;
        private Camera cam;
        private int lastSectorCount = -1;
        private int displayedFocusIndex = -1;
        private float focusShownAt = -999f;

        private struct SectorLabel
        {
            public int Index;
            public RectTransform Rect;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public Button Button;
            public Image Background;
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
            if (overlayCanvas != null)
            {
                LayoutTopSectorChrome();
                return;
            }

            var canvasGo = new GameObject("SectorTravelCanvas");
            canvasGo.transform.SetParent(transform, false);
            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below Acts/hand so sector chrome never paints over Colony Acts text.
            overlayCanvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
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

            // Current sector HUD (top-center) — fades after focus change.
            var hudGo = new GameObject("CurrentSector");
            hudGo.transform.SetParent(canvasGo.transform, false);
            currentSectorRt = hudGo.AddComponent<RectTransform>();
            currentSectorRt.anchorMin = new Vector2(0.5f, 1f);
            currentSectorRt.anchorMax = new Vector2(0.5f, 1f);
            currentSectorRt.pivot = new Vector2(0.5f, 1f);
            currentSectorRt.sizeDelta = new Vector2(360f, 42f);
            currentSectorRt.anchoredPosition = new Vector2(0f, -86f);
            currentSectorGroup = hudGo.AddComponent<CanvasGroup>();
            currentSectorGroup.blocksRaycasts = false;
            currentSectorText = hudGo.AddComponent<TextMeshProUGUI>();
            if (font != null) currentSectorText.font = font;
            currentSectorText.fontSize = 20f;
            currentSectorText.alignment = TextAlignmentOptions.Center;
            currentSectorText.color = new Color(0.75f, 0.9f, 1f, 0.95f);
            currentSectorText.raycastTarget = false;

            // Q/E dock left under Weeks Left — never beside Colony Acts.
            navPrevRt = CreateLeftNavButton(canvasGo.transform, font, "◀ Q", 18f, () =>
            {
                PlayerInput.Instance?.PageSectors(-1);
            });
            navNextRt = CreateLeftNavButton(canvasGo.transform, font, "E ▶", 114f, () =>
            {
                PlayerInput.Instance?.PageSectors(1);
            });
            LayoutTopSectorChrome();
        }

        private static RectTransform CreateLeftNavButton(
            Transform parent,
            TMP_FontAsset font,
            string label,
            float x,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Nav_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(88f, 40f);
            rt.anchoredPosition = new Vector2(x, -230f);
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
            tmp.fontSize = 18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return rt;
        }

        private void LayoutTopSectorChrome()
        {
            if (currentSectorRt != null)
                currentSectorRt.anchoredPosition = new Vector2(0f, -86f);

            if (navPrevRt != null)
            {
                navPrevRt.anchorMin = new Vector2(0f, 1f);
                navPrevRt.anchorMax = new Vector2(0f, 1f);
                navPrevRt.pivot = new Vector2(0f, 1f);
                navPrevRt.anchoredPosition = new Vector2(18f, -230f);
            }
            if (navNextRt != null)
            {
                navNextRt.anchorMin = new Vector2(0f, 1f);
                navNextRt.anchorMax = new Vector2(0f, 1f);
                navNextRt.pivot = new Vector2(0f, 1f);
                navNextRt.anchoredPosition = new Vector2(114f, -230f);
            }
        }

        private void LateUpdate()
        {
            EnsureUi();
            if (cam == null) cam = Camera.main;
            LayoutTopSectorChrome();
            RebuildLabelsIfNeeded();
            UpdateFocusFade();
            UpdateLabelPositions();
            UpdateCurrentSectorReadout();
            CullWorldLabelsOverActs();
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
                rt.sizeDelta = new Vector2(220f, 36f);
                var group = go.AddComponent<CanvasGroup>();
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
                tmp.fontSize = 18f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.85f, 0.92f, 1f, 1f);
                tmp.raycastTarget = false;
                tmp.text = SectorColonization.GetSectorDisplayName(index);

                labels.Add(new SectorLabel
                {
                    Index = index,
                    Rect = rt,
                    Group = group,
                    Text = tmp,
                    Button = btn,
                    Background = img
                });
            }
        }

        private void UpdateFocusFade()
        {
            int focus = ResolveFocusSectorIndex();
            if (focus != displayedFocusIndex)
            {
                displayedFocusIndex = focus;
                focusShownAt = Time.unscaledTime;
            }

            float age = Time.unscaledTime - focusShownAt;
            float alpha;
            if (age <= FocusHoldSeconds)
                alpha = 1f;
            else if (age >= FocusHoldSeconds + FocusFadeSeconds)
                alpha = 0f;
            else
                alpha = 1f - ((age - FocusHoldSeconds) / FocusFadeSeconds);

            if (currentSectorGroup != null)
                currentSectorGroup.alpha = alpha;

            for (int i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                if (label.Group == null) continue;
                bool isFocus = label.Index == displayedFocusIndex;
                float a = isFocus ? alpha : 0.55f;
                label.Group.alpha = a;
                label.Group.blocksRaycasts = a > 0.05f;
                label.Group.interactable = a > 0.05f;
            }
        }

        /// <summary>Call when Q/E or minimap jumps to a sector so the name shows then fades.</summary>
        public void NotifySectorFocused(int sectorIndex)
        {
            if (sectorIndex < 0) return;
            displayedFocusIndex = sectorIndex;
            focusShownAt = Time.unscaledTime;
            if (currentSectorGroup != null) currentSectorGroup.alpha = 1f;
        }

        private static int ResolveFocusSectorIndex()
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null || sm.Sectors.Count == 0) return -1;

            var nearest = sm.GetNearestSector(PlayerInput.GetCameraFocusPosition());
            if (nearest == null) return 0;
            return Mathf.Max(0, sm.Sectors.IndexOf(nearest));
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
                bool inActsColumn = IsInActsColumn(screen);
                bool visible = screen.z > 0f
                    && screen.x > -40f && screen.x < Screen.width + 40f
                    && screen.y > -40f && screen.y < Screen.height + 40f
                    && !inActsColumn;
                label.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                label.Rect.position = screen;
                if (label.Text != null)
                    label.Text.text = SectorColonization.GetSectorDisplayName(label.Index);
            }
        }

        private readonly HashSet<TextMeshPro> mutedWorldLabels = new();

        /// <summary>
        /// Hide floating world TMP (nexus / feature names) when they project over Colony Acts.
        /// </summary>
        private void CullWorldLabelsOverActs()
        {
            if (cam == null) return;

            var stillMuted = new HashSet<TextMeshPro>();
            foreach (var tmp in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude))
            {
                if (tmp == null) continue;
                Transform root = tmp.transform;
                while (root.parent != null
                       && !root.name.StartsWith("QuestionMark_", System.StringComparison.Ordinal))
                    root = root.parent;
                if (!root.name.StartsWith("QuestionMark_", System.StringComparison.Ordinal))
                    continue;

                Vector3 screen = cam.WorldToScreenPoint(root.position);
                bool overActs = screen.z > 0f && IsInActsColumn(screen);
                if (overActs)
                {
                    if (tmp.enabled)
                        tmp.enabled = false;
                    mutedWorldLabels.Add(tmp);
                    stillMuted.Add(tmp);
                }
            }

            // Restore labels that moved clear of the Acts column.
            mutedWorldLabels.RemoveWhere(tmp =>
            {
                if (tmp == null) return true;
                if (stillMuted.Contains(tmp)) return false;
                tmp.enabled = true;
                return true;
            });
        }

        private static bool IsInActsColumn(Vector3 screen)
        {
            return screen.x >= Screen.width - RightHudReservePx
                && screen.y >= Screen.height * 0.25f;
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

            int idx = displayedFocusIndex >= 0 ? displayedFocusIndex : 0;
            currentSectorText.text = SectorColonization.GetSectorDisplayName(idx) + "   <size=70%>(Q / E)</size>";
        }
    }
}
