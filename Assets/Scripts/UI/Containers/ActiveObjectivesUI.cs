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
        private bool layoutReady;

        private void Start()
        {
            SetupLayout();
        }

        private void SetupLayout()
        {
            if (layoutReady) return;
            layoutReady = true;

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(380f, 280f);
            rect.anchoredPosition = new Vector2(-20f, -150f);

            background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.04f, 0.07f, 0.92f);
            background.raycastTarget = false;

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
            headerRt.anchorMin = new Vector2(0f, 0.86f);
            headerRt.anchorMax = new Vector2(1f, 1.0f);
            headerRt.sizeDelta = new Vector2(-28f, 0f);
            headerRt.anchoredPosition = new Vector2(14f, -4f);

            headerText = headerGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) headerText.font = projectFont;
            headerText.fontSize = 18f;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            headerText.color = new Color(0.55f, 0.95f, 1f, 1f);
            headerText.text = "ACTIVE OBJECTIVES";
            headerText.raycastTarget = false;

            GameObject bodyGO = new GameObject("Body Text");
            bodyGO.transform.SetParent(transform, false);
            RectTransform bodyRt = bodyGO.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0.0f);
            bodyRt.anchorMax = new Vector2(1f, 0.86f);
            bodyRt.sizeDelta = new Vector2(-28f, -8f);
            bodyRt.anchoredPosition = new Vector2(14f, 4f);

            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) bodyText.font = projectFont;
            bodyText.fontSize = 15f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 1.2f;
            bodyText.color = Color.white;
            bodyText.richText = true;
            bodyText.raycastTarget = false;
        }

        private void Update()
        {
            UpdateObjectivesText();
        }

        private void UpdateObjectivesText()
        {
            if (bodyText == null) return;

            if (headerText != null)
                headerText.text = "COLONY ACTS";

            if (ColonyActManager.Instance != null)
            {
                bodyText.SetText(ColonyActManager.Instance.BuildObjectivesText());
                return;
            }

            bodyText.SetText("<color=#C8D0D8>Colony Act manager starting…</color>");
        }
    }
}

