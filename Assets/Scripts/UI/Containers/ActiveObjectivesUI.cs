using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    public class ActiveObjectivesUI : MonoBehaviour
    {
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI bodyText;
        private Image background;

        private void Start()
        {
            SetupLayout();
        }

        private void SetupLayout()
        {
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(340f, 150f);
            rect.anchoredPosition = new Vector2(-20f, -140f); // Position below the Supplies Bar

            background = gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.08f, 0.75f); // 75% opacity dark obsidian
            
            TMP_FontAsset projectFont = null;
            var allTmp = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tmp in allTmp)
            {
                if (tmp != null && tmp.font != null)
                {
                    projectFont = tmp.font;
                    break;
                }
            }

            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.15f, 0.22f, 1f); // subtle tech grey/blue outline
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject headerGO = new GameObject("Header Text");
            headerGO.transform.SetParent(transform, false);
            RectTransform headerRt = headerGO.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.8f);
            headerRt.anchorMax = new Vector2(1f, 1.0f);
            headerRt.sizeDelta = new Vector2(-30f, 0f);
            headerRt.anchoredPosition = new Vector2(15f, -5f);

            headerText = headerGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) headerText.font = projectFont;
            headerText.fontSize = 13.5f;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            headerText.color = new Color(0f, 0.9f, 1f, 1f); // Tech Cyan
            headerText.text = "ACTIVE OBJECTIVES";

            GameObject bodyGO = new GameObject("Body Text");
            bodyGO.transform.SetParent(transform, false);
            RectTransform bodyRt = bodyGO.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0.0f);
            bodyRt.anchorMax = new Vector2(1f, 0.8f);
            bodyRt.sizeDelta = new Vector2(-30f, -10f);
            bodyRt.anchoredPosition = new Vector2(15f, 5f);

            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) bodyText.font = projectFont;
            bodyText.fontSize = 11.5f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.lineSpacing = 1.15f;
            bodyText.color = Color.white;
        }

        private void Update()
        {
            UpdateObjectivesText();
        }

        private void UpdateObjectivesText()
        {
            if (bodyText == null) return;

            var sb = new System.Text.StringBuilder();

            if (GameDevTV.RTS.Player.GenerationManager.Instance != null)
            {
                string desc = GameDevTV.RTS.Player.GenerationManager.Instance.CurrentMilestoneDescription;
                sb.AppendLine($"<color=#FFD700>Goal:</color> {desc}");
            }
            else
            {
                sb.AppendLine("<color=#FFD700>Goal:</color> Secure sector and expand.");
            }

            sb.AppendLine();

            var card = GameDevTV.RTS.Player.BlueprintDraftManager.LastDraftedCard;
            if (card != null)
            {
                sb.AppendLine($"<color=#55FF55>Blueprint:</color> {card.cardName}");
                sb.AppendLine($"<color=#CCCCCC>{card.cardDescription}</color>");
            }
            else
            {
                sb.AppendLine("<color=#888888>Blueprint: No active tech drafted.</color>");
            }

            bodyText.SetText(sb.ToString());
        }
    }
}