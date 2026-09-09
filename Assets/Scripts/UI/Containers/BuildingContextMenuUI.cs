using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Right-click context menu on player buildings: Repair / Demolish.
    /// </summary>
    public class BuildingContextMenuUI : MonoBehaviour
    {
        public static BuildingContextMenuUI Instance { get; private set; }

        private BaseBuilding target;
        private RectTransform panelRt;
        private TextMeshProUGUI titleText;
        private Button repairButton;
        private TextMeshProUGUI repairLabel;
        private Button demolishButton;
        private TextMeshProUGUI demolishLabel;
        private Canvas rootCanvas;
        private bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(BuildingContextMenuUI));
            go.AddComponent<BuildingContextMenuUI>();
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!built || panelRt == null || !panelRt.gameObject.activeSelf) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverMenu())
                    Hide();
            }

            if (target == null)
                Hide();
        }

        public void Show(BaseBuilding building, Vector2 screenPosition)
        {
            if (building == null || building is GlobalCommander) return;
            if (building.Owner != Owner.Player1) return;

            EnsureBuilt();
            target = building;

            string name = building.ResolvedBuildingSO != null
                ? building.ResolvedBuildingSO.Name
                : building.name;
            titleText.text = name;

            bool canRepair = building.Progress.State == BuildingProgress.BuildingState.Completed
                && building.CurrentHealth < building.MaxHealth;
            repairButton.gameObject.SetActive(canRepair);
            if (canRepair)
                repairLabel.text = "Repair (2 Mat)";

            int cost = ReservedSiteBuildUtility.GetMaterialsCost(building.ResolvedBuildingSO);
            float rate = building.Progress.State == BuildingProgress.BuildingState.Building ? 0.75f : 0.5f;
            int refund = Mathf.Max(0, Mathf.RoundToInt(cost * rate));
            demolishLabel.text = refund > 0 ? $"Demolish (+{refund} Mat)" : "Demolish";

            panelRt.gameObject.SetActive(true);
            PositionNear(screenPosition);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        }

        public void Hide()
        {
            target = null;
            if (panelRt != null)
                panelRt.gameObject.SetActive(false);
        }

        private void OnRepairClicked()
        {
            if (target != null)
                target.TryRepair();
            Hide();
        }

        private void OnDemolishClicked()
        {
            var b = target;
            Hide();
            b?.TryDemolish(refund: true);
        }

        private bool IsPointerOverMenu()
        {
            if (EventSystem.current == null || panelRt == null) return false;
            var ped = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject != null
                    && results[i].gameObject.transform.IsChildOf(panelRt))
                    return true;
            }
            return false;
        }

        private void PositionNear(Vector2 screenPosition)
        {
            if (panelRt == null || rootCanvas == null) return;

            Camera cam = null;
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                screenPosition,
                cam,
                out Vector2 local);

            // Keep menu on-screen.
            float w = panelRt.sizeDelta.x;
            float h = panelRt.sizeDelta.y;
            Vector2 pivotBias = new Vector2(12f, -12f);
            Vector2 pos = local + pivotBias;
            var canvasRt = rootCanvas.transform as RectTransform;
            Vector2 canvasSize = canvasRt != null ? canvasRt.rect.size : new Vector2(Screen.width, Screen.height);
            pos.x = Mathf.Clamp(pos.x, -canvasSize.x * 0.5f + 8f, canvasSize.x * 0.5f - w - 8f);
            pos.y = Mathf.Clamp(pos.y, -canvasSize.y * 0.5f + h + 8f, canvasSize.y * 0.5f - 8f);
            panelRt.anchoredPosition = pos;
        }

        private void EnsureBuilt()
        {
            if (built && panelRt != null) return;
            built = true;

            Canvas mainCanvas = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (c != null && c.gameObject.name == "Runtime UI UGUI")
                {
                    mainCanvas = c;
                    break;
                }
            }
            if (mainCanvas == null)
                mainCanvas = Object.FindAnyObjectByType<Canvas>();

            if (mainCanvas == null)
            {
                var canvasGo = new GameObject("Building Context Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                rootCanvas = canvasGo.GetComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.sortingOrder = 1200;
                DontDestroyOnLoad(canvasGo);
            }
            else
            {
                rootCanvas = mainCanvas;
            }

            TMP_FontAsset font = null;
            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
            {
                if (tmp != null && tmp.font != null)
                {
                    font = tmp.font;
                    break;
                }
            }

            var panelGo = new GameObject("Building Context Menu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(rootCanvas.transform, false);
            panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.sizeDelta = new Vector2(220f, 0f);

            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.96f);
            bg.raycastTarget = true;

            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            titleText = CreateLabel(panelGo.transform, "Title", font, 16f, FontStyles.Bold);
            titleText.color = new Color(0.85f, 0.92f, 1f, 1f);

            repairButton = CreateMenuButton(panelGo.transform, "Repair Button", out repairLabel, font);
            repairButton.onClick.AddListener(OnRepairClicked);

            demolishButton = CreateMenuButton(panelGo.transform, "Demolish Button", out demolishLabel, font);
            demolishButton.onClick.AddListener(OnDemolishClicked);
            demolishLabel.color = new Color(1f, 0.55f, 0.45f, 1f);

            panelGo.SetActive(false);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 26f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateMenuButton(Transform parent, string name, out TextMeshProUGUI label, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 34f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.14f, 0.16f, 0.2f, 1f);
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.36f, 1f);
            colors.pressedColor = new Color(0.1f, 0.12f, 0.16f, 1f);
            btn.colors = colors;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 0f);
            lrt.offsetMax = new Vector2(-10f, 0f);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = 15f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            return btn;
        }
    }
}
