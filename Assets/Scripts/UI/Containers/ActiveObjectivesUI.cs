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
                    float temp = Supplies.Temperature.TryGetValue(Owner.Player1, out float tVal) ? tVal : -60f;
                    float atmos = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float aVal) ? aVal : 0.01f;
                    float water = Supplies.Water.TryGetValue(Owner.Player1, out float wVal) ? wVal : 0f;

                    bool tempMet = AppendClimateLine(sb, "TEMPERATURE",
                        temp, gm.BaselineTemperature, gm.GetRoundTemperatureTarget(),
                        "{0:F1}°C / {1:F1}°C ({2})");

                    bool atmosMet = AppendClimateLine(sb, "ATMOSPHERE",
                        atmos, gm.BaselineAtmosphere, gm.GetRoundAtmosphereTarget(),
                        "{0:F2} atm / {1:F2} atm ({2})");

                    bool waterMet = AppendClimateLine(sb, "WATER",
                        water, gm.BaselineWater, gm.GetRoundWaterTarget(),
                        "{0:F0}% / {1:F0}% ({2})");

                    float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
                    sb.AppendLine();
                    if (progress >= 1f || gm.IsBetweenRounds)
                    {
                        sb.AppendLine("<color=#66F273><b>All sector goals met — advancing…</b></color>");
                    }
                    else
                    {
                        var waiting = new System.Collections.Generic.List<string>(3);
                        if (!tempMet) waiting.Add("Temp");
                        if (!atmosMet) waiting.Add("Atmos");
                        if (!waterMet) waiting.Add("Water");
                        if (waiting.Count > 0)
                        {
                            sb.AppendLine(
                                $"<color=#FFD080>Sector finishes when <b>all three</b> are green. Still need: {string.Join(", ", waiting)}</color>");
                        }
                        else if (!string.IsNullOrEmpty(bottleneck))
                        {
                            sb.AppendLine(
                                $"<color=#FFD080>Still need primary goal: {bottleneck}</color>");
                        }
                    }

                    sb.AppendLine("<size=13><color=#C8D0D8>Each sector needs its own climate gains — prior sectors do not count.</color></size>");
                    sb.AppendLine("<size=13>" + TerraformingGoalColors.BuildLegendLine(
                        milestoneGoal, "TEMPERATURE", "ATMOSPHERE", "WATER") + "</size>");
                }
            }
            else
            {
                sb.AppendLine($"{TerraformingGoalColors.Colorize("Goal:", "COMMAND POST")} Secure sector and expand.");
            }

            bodyText.SetText(sb.ToString());
        }

        /// <returns>True when this climate line is met (shown green / DONE).</returns>
        private static bool AppendClimateLine(
            System.Text.StringBuilder sb,
            string goalKey,
            float current,
            float baseline,
            float roundTarget,
            string valueFormat)
        {
            bool met = !GenerationManager.IsUnmetSectorGoal(goalKey);
            Color valueColor = met ? TerraformingGoalColors.MetValue : TerraformingGoalColors.UnmetValue;
            float remaining = Mathf.Max(0f, roundTarget - current);
            string status = met
                ? "DONE ✓"
                : goalKey == "TEMPERATURE"
                    ? $"need +{remaining:F0} more"
                    : goalKey == "ATMOSPHERE"
                        ? $"need +{remaining:F2} more"
                        : $"need +{remaining:F0} more";
            string valueText = string.Format(valueFormat, current, roundTarget, status);
            string label = TerraformingGoalColors.DisplayName(goalKey);
            sb.AppendLine(
                $"  • {TerraformingGoalColors.Colorize(label + ":", goalKey)} " +
                $"{TerraformingGoalColors.Colorize(valueText, valueColor)}");
            return met;
        }
    }
}

