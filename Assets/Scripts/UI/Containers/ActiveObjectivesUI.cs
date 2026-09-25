using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI.Containers
{
    public class ActiveObjectivesUI : MonoBehaviour
    {
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI bodyText;
        private Image background;
        private ScrollRect scrollRect;
        private RectTransform contentRt;
        private bool layoutReady;

        private void Start()
        {
            SetupLayout();
        }

        private void SetupLayout()
        {
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            // Right column, below top chrome; height fits the game view so text stays on-screen.
            float topPad = 110f;
            float bottomPad = 96f;
            float height = Mathf.Clamp(Screen.height - topPad - bottomPad, 280f, 640f);
            rect.sizeDelta = new Vector2(460f, height);
            rect.anchoredPosition = new Vector2(-12f, -topPad);

            if (layoutReady) return;
            layoutReady = true;

            background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.04f, 0.07f, 0.92f);
            // Need raycasts so the mouse wheel can scroll while hovering the panel.
            background.raycastTarget = true;

            // Own overlay so sector Q/E / world labels never draw on top of Acts text.
            var overlay = gameObject.GetComponent<Canvas>();
            if (overlay == null) overlay = gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 200;
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            TMP_FontAsset projectFont = null;
            var allTmp = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            foreach (var tmp in allTmp)
            {
                if (tmp != null && tmp.font != null)
                {
                    projectFont = tmp.font;
                    break;
                }
            }

            var outline = gameObject.GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.55f, 0.85f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject headerGO = new GameObject("Header Text");
            headerGO.transform.SetParent(transform, false);
            RectTransform headerRt = headerGO.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.90f);
            headerRt.anchorMax = new Vector2(1f, 1.0f);
            headerRt.sizeDelta = new Vector2(-28f, 0f);
            headerRt.anchoredPosition = new Vector2(14f, -4f);

            headerText = headerGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) headerText.font = projectFont;
            headerText.fontSize = 18f;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            headerText.color = new Color(0.55f, 0.95f, 1f, 1f);
            headerText.text = "COLONY ACTS";
            headerText.raycastTarget = false;

            GameObject scrollGo = new GameObject("Scroll View", typeof(RectTransform));
            scrollGo.transform.SetParent(transform, false);
            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0.0f);
            scrollRt.anchorMax = new Vector2(1f, 0.90f);
            scrollRt.offsetMin = new Vector2(10f, 8f);
            scrollRt.offsetMax = new Vector2(-10f, -4f);

            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollBg.raycastTarget = true;

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            var viewportImg = viewportGo.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImg.raycastTarget = true;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject bodyGO = new GameObject("Body Text");
            bodyGO.transform.SetParent(contentGo.transform, false);
            RectTransform bodyRt = bodyGO.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = new Vector2(0f, 0f);

            var bodyFitter = bodyGO.AddComponent<ContentSizeFitter>();
            bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) bodyText.font = projectFont;
            bodyText.fontSize = 15f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.lineSpacing = 4f;
            bodyText.color = Color.white;
            bodyText.richText = true;
            bodyText.raycastTarget = false;
            var bodyOutline = bodyGO.AddComponent<Outline>();
            bodyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            bodyOutline.effectDistance = new Vector2(1.2f, -1.2f);

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
        }

        private void Update()
        {
            // Keep panel height matched to the current Game view.
            var rect = transform as RectTransform;
            if (rect != null)
            {
                float topPad = 110f;
                float bottomPad = 96f;
                float height = Mathf.Clamp(Screen.height - topPad - bottomPad, 280f, 640f);
                if (!Mathf.Approximately(rect.sizeDelta.y, height))
                    rect.sizeDelta = new Vector2(460f, height);
                if (!Mathf.Approximately(rect.anchoredPosition.y, -topPad))
                    rect.anchoredPosition = new Vector2(-12f, -topPad);
            }
            UpdateObjectivesText();
        }

        private void UpdateObjectivesText()
        {
            if (bodyText == null) return;

            if (headerText != null)
                headerText.text = "COLONY ACTS";

            string next;
            if (ColonyActManager.Instance != null)
                next = ColonyActManager.Instance.BuildObjectivesText();
            else
                next = "<color=#C8D0D8>Colony Act manager starting…</color>";

            if (bodyText.text != next)
            {
                bodyText.SetText(next);
                bodyText.ForceMeshUpdate();
                // Keep content height in sync so ScrollRect can scroll the full text.
                float h = Mathf.Max(bodyText.preferredHeight + 8f, 40f);
                if (contentRt != null)
                    contentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                var bodyRt = bodyText.rectTransform;
                bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }
        }
    }
}
