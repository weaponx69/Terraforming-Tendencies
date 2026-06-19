using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    public class GlobalCommanderUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI allowanceValueText;
        [SerializeField] private Slider allowanceSlider;

        private TextMeshProUGUI objectivesHeaderText;
        private TextMeshProUGUI objectivesBodyText;

        private void Start()
        {
            ReorganizeLayoutAndCreateObjectiveTexts();

            if (allowanceSlider != null)
            {
                allowanceSlider.onValueChanged.AddListener(HandleSliderValueChanged);
            }
        }

        private void ReorganizeLayoutAndCreateObjectiveTexts()
        {
            // Find existing components
            Transform titleTrans = transform.Find("Title Text");
            Transform budgetTrans = transform.Find("Budget Text");
            Transform sliderTrans = transform.Find("Budget Slider");

            // Re-anchor existing components to top half of the screen
            if (titleTrans != null && titleTrans.TryGetComponent<RectTransform>(out var rtTitle))
            {
                rtTitle.anchorMin = new Vector2(0f, 0.85f);
                rtTitle.anchorMax = new Vector2(1f, 1.00f);
                rtTitle.sizeDelta = new Vector2(-40f, 0f);
                rtTitle.anchoredPosition = new Vector2(20f, -5f);
            }
            if (budgetTrans != null && budgetTrans.TryGetComponent<RectTransform>(out var rtBudget))
            {
                rtBudget.anchorMin = new Vector2(0f, 0.70f);
                rtBudget.anchorMax = new Vector2(1f, 0.85f);
                rtBudget.sizeDelta = new Vector2(-40f, 0f);
                rtBudget.anchoredPosition = new Vector2(20f, -5f);
            }
            if (sliderTrans != null && sliderTrans.TryGetComponent<RectTransform>(out var rtSlider))
            {
                rtSlider.anchorMin = new Vector2(0f, 0.55f);
                rtSlider.anchorMax = new Vector2(1f, 0.70f);
                rtSlider.sizeDelta = new Vector2(-40f, -5f);
                rtSlider.anchoredPosition = new Vector2(20f, -2f);
            }

            // Create Objectives Header Text if it doesn't exist
            Transform headerTrans = transform.Find("Objectives Header");
            if (headerTrans == null)
            {
                GameObject headerGO = new GameObject("Objectives Header");
                headerGO.transform.SetParent(transform, false);
                var rtHeader = headerGO.AddComponent<RectTransform>();
                rtHeader.anchorMin = new Vector2(0f, 0.38f);
                rtHeader.anchorMax = new Vector2(1f, 0.53f);
                rtHeader.sizeDelta = new Vector2(-40f, 0f);
                rtHeader.anchoredPosition = new Vector2(20f, -5f);

                objectivesHeaderText = headerGO.AddComponent<TextMeshProUGUI>();
                if (titleTrans != null && titleTrans.TryGetComponent<TextMeshProUGUI>(out var titleTmp))
                {
                    objectivesHeaderText.font = titleTmp.font;
                    objectivesHeaderText.fontSize = titleTmp.fontSize * 0.9f;
                }
                objectivesHeaderText.alignment = TextAlignmentOptions.Left;
                objectivesHeaderText.fontStyle = FontStyles.Bold;
                objectivesHeaderText.color = new Color(0.3f, 0.9f, 1f, 1f); // Neon Sci-fi blue
                objectivesHeaderText.text = "ACTIVE MISSION OBJECTIVES:";
            }
            else
            {
                objectivesHeaderText = headerTrans.GetComponent<TextMeshProUGUI>();
            }

            // Create Objectives Body Text if it doesn't exist
            Transform bodyTrans = transform.Find("Objectives Body");
            if (bodyTrans == null)
            {
                GameObject bodyGO = new GameObject("Objectives Body");
                bodyGO.transform.SetParent(transform, false);
                var rtBody = bodyGO.AddComponent<RectTransform>();
                rtBody.anchorMin = new Vector2(0f, 0.02f);
                rtBody.anchorMax = new Vector2(1f, 0.38f);
                rtBody.sizeDelta = new Vector2(-40f, 0f);
                rtBody.anchoredPosition = new Vector2(20f, 0f);

                objectivesBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
                if (budgetTrans != null && budgetTrans.TryGetComponent<TextMeshProUGUI>(out var budgetTmp))
                {
                    objectivesBodyText.font = budgetTmp.font;
                    objectivesBodyText.fontSize = budgetTmp.fontSize * 0.85f;
                }
                objectivesBodyText.alignment = TextAlignmentOptions.TopLeft;
                objectivesBodyText.textWrappingMode = TextWrappingModes.Normal;
                objectivesBodyText.lineSpacing = 1.15f;
                objectivesBodyText.color = Color.white;
            }
            else
            {
                objectivesBodyText = bodyTrans.GetComponent<TextMeshProUGUI>();
            }
        }

        public void EnableFor(AbstractCommandable item)
        {
            gameObject.SetActive(true);
            ReorganizeLayoutAndCreateObjectiveTexts();
            if (titleText != null)
            {
                titleText.SetText("UNIVERSAL COMMAND CENTER");
            }
            UpdateSliderFromController();
            UpdateObjectivesText();
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
            {
                UpdateSliderFromController();
                UpdateObjectivesText();
            }
        }

        private void UpdateObjectivesText()
        {
            if (objectivesBodyText == null) return;

            var sb = new System.Text.StringBuilder();

            // 1. Sector Milestone/Goal
            if (GameDevTV.RTS.Player.GenerationManager.Instance != null)
            {
                string desc = GameDevTV.RTS.Player.GenerationManager.Instance.CurrentMilestoneDescription;
                sb.AppendLine($"<color=#FFD700>Sector Milestone:</color> {desc}");
            }
            else
            {
                sb.AppendLine("<color=#FFD700>Sector Milestone:</color> Establish colony and secure sector.");
            }

            sb.AppendLine();

            // 2. Drafted Blueprint Card
            var card = GameDevTV.RTS.Player.BlueprintDraftManager.LastDraftedCard;
            if (card != null)
            {
                sb.AppendLine($"<color=#55FF55>Drafted Blueprint:</color> {card.cardName}");
                sb.AppendLine($"<color=#CCCCCC>{card.cardDescription}</color>");
            }
            else
            {
                sb.AppendLine("<color=#888888>Drafted Blueprint: No active project drafted yet.</color>");
            }

            objectivesBodyText.SetText(sb.ToString());
        }

        private void UpdateSliderFromController()
        {
            if (GreedyAIController.Instance != null)
            {
                float currentAllowance = GreedyAIController.Instance.AISpendingAllowance;
                if (allowanceSlider != null && !Mathf.Approximately(allowanceSlider.value, currentAllowance))
                {
                    allowanceSlider.SetValueWithoutNotify(currentAllowance);
                }

                if (allowanceValueText != null)
                {
                    int percentage = Mathf.RoundToInt(currentAllowance * 100f);
                    // Color code based on spending level
                    string colorHex = percentage > 66 ? "#00FF00" : (percentage > 33 ? "#FFFF00" : "#FF5500");
                    allowanceValueText.SetText($"AI Budget Limit: <color={colorHex}>{percentage}%</color>");
                }
            }
            else
            {
                if (allowanceValueText != null)
                {
                    allowanceValueText.SetText("AI Budget Limit: <color=#888888>N/A</color>");
                }
            }
        }

        private void HandleSliderValueChanged(float value)
        {
            if (GreedyAIController.Instance != null)
            {
                GreedyAIController.Instance.AISpendingAllowance = value;
            }
        }

        private void OnDestroy()
        {
            if (allowanceSlider != null)
            {
                allowanceSlider.onValueChanged.RemoveListener(HandleSliderValueChanged);
            }
        }
    }
}