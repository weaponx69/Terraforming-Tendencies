using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI;

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
            rect.sizeDelta = new Vector2(340f, 220f);
            rect.anchoredPosition = new Vector2(-20f, -140f);

            background = gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);

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

            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject headerGO = new GameObject("Header Text");
            headerGO.transform.SetParent(transform, false);
            RectTransform headerRt = headerGO.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.82f);
            headerRt.anchorMax = new Vector2(1f, 1.0f);
            headerRt.sizeDelta = new Vector2(-30f, 0f);
            headerRt.anchoredPosition = new Vector2(15f, -5f);

            headerText = headerGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) headerText.font = projectFont;
            headerText.fontSize = 17f;
            headerText.alignment = TextAlignmentOptions.Left;
            headerText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            headerText.color = new Color(0f, 0.9f, 1f, 1f);
            headerText.text = "ACTIVE OBJECTIVES";

            GameObject bodyGO = new GameObject("Body Text");
            bodyGO.transform.SetParent(transform, false);
            RectTransform bodyRt = bodyGO.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0.0f);
            bodyRt.anchorMax = new Vector2(1f, 0.82f);
            bodyRt.sizeDelta = new Vector2(-30f, -10f);
            bodyRt.anchoredPosition = new Vector2(15f, 5f);

            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (projectFont != null) bodyText.font = projectFont;
            bodyText.fontSize = 14.5f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 1.15f;
            bodyText.color = Color.white;
            bodyText.richText = true;
        }

        private void Update()
        {
            UpdateObjectivesText();
        }

        private void UpdateObjectivesText()
        {
            if (bodyText == null) return;

            var sb = new System.Text.StringBuilder();

            if (GenerationManager.Instance != null)
            {
                var gm = GenerationManager.Instance;
                string milestoneGoal = TerraformingGoalColors.GoalKeyForMilestone(gm.CurrentMilestoneType);
                string goalHex = TerraformingGoalColors.ToHex(TerraformingGoalColors.ForGoal(milestoneGoal));
                string primaryGoal = gm.CurrentMilestoneDescription;
                int climateIdx = primaryGoal.IndexOf(" (Temp", System.StringComparison.Ordinal);
                if (climateIdx > 0) primaryGoal = primaryGoal.Substring(0, climateIdx);
                sb.AppendLine($"{TerraformingGoalColors.Colorize("Goal:", milestoneGoal)} <color={goalHex}>{primaryGoal}</color>");

                if (!gm.IsExpansionPhase)
                {
                    AppendClimateLine(sb, "TEMPERATURE", "Temp",
                        Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal) ? tVal : -60f,
                        gm.GetTargetTemperature(gm.CurrentGeneration),
                        "{0:F1}°C / {1:F1}°C");

                    AppendClimateLine(sb, "ATMOSPHERE", "Atmos",
                        Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal) ? aVal : 0.01f,
                        gm.GetTargetAtmosphere(gm.CurrentGeneration),
                        "{0:F2} atm / {1:F2} atm");

                    AppendClimateLine(sb, "WATER", "Water",
                        Supplies.Water.TryGetValue(Owner.Player1, out float wVal) ? wVal : 0f,
                        gm.GetTargetWater(gm.CurrentGeneration),
                        "{0:F0}% / {1:F0}%");

                    sb.AppendLine();
                    sb.AppendLine("<size=12><color=#AAAAAA>Matching hand cards use the same colors.</color></size>");
                }
            }
            else
            {
                sb.AppendLine($"{TerraformingGoalColors.Colorize("Goal:", "COMMAND POST")} Secure sector and expand.");
            }

            bodyText.SetText(sb.ToString());
        }

        private static void AppendClimateLine(
            System.Text.StringBuilder sb,
            string goalKey,
            string label,
            float current,
            float target,
            string valueFormat)
        {
            bool met = current >= target;
            Color valueColor = met ? TerraformingGoalColors.MetValue : TerraformingGoalColors.UnmetValue;
            string valueText = string.Format(valueFormat, current, target);
            sb.AppendLine(
                $"  • {TerraformingGoalColors.Colorize(label + ":", goalKey)} " +
                $"{TerraformingGoalColors.Colorize(valueText, valueColor)}");
        }
    }
}
