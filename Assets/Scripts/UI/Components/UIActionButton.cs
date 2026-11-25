using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIActionButton : MonoBehaviour, IUIElement<BaseCommand, IEnumerable<AbstractCommandable>, UnityAction>, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Tooltip tooltip;

        private bool isActive;
        public bool IsActive => isActive;
        private RectTransform rectTransform;
        private Button button;
        private Image buttonImage;
        private Outline goalOutline;
        private TextMeshProUGUI goalBadge;
        private TextMeshProUGUI costLabel;
        private Color defaultButtonColor = Color.white;
        private string goalKey = string.Empty;
        private bool picked;
        private int slotHandIndex = -1;
        private bool slotIsSurvey;

        /// <summary>Hand index bound to this slot (UI playthrough).</summary>
        public int HandIndexForPlaythrough => slotHandIndex;

        private static readonly string MATERIALS_FORMAT = "{0} Materials";
        private static readonly string DEPENDENCY_FORMAT_NO_COMMA = "<color=#AC0000>{0}</color>.";
        private static readonly string DEPENDENCY_FORMAT_COMMA = "<color=#AC0000>{0}</color>, ";

        private void Awake()
        {
            button = GetComponent<Button>();
            rectTransform = GetComponent<RectTransform>();
            buttonImage = GetComponent<Image>();
            if (buttonImage != null)
            {
                defaultButtonColor = buttonImage.color;
            }
            if (button == null)
            {
                Debug.LogWarning($"[UIActionButton] Missing Button component on {name}. The [RequireComponent] attribute should auto-add one.", this);
            }
            Disable();
        }

        public void EnableFor(BaseCommand command, IEnumerable<AbstractCommandable> selectedUnits, UnityAction onClick)
        {
            EnableFor(command, selectedUnits, onClick, null, -1);
        }

        public void EnableFor(
            BaseCommand command,
            IEnumerable<AbstractCommandable> selectedUnits,
            UnityAction onClick,
            string cardGoal)
        {
            EnableFor(command, selectedUnits, onClick, cardGoal, -1);
        }

        public void EnableFor(
            BaseCommand command,
            IEnumerable<AbstractCommandable> selectedUnits,
            UnityAction onClick,
            string cardGoal,
            int materialsCostOverride)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            SetIcon(command.Icon);
            EnsureCardTextLayout();
            SetLabel(command.Name);
            int cost = materialsCostOverride >= 0 ? materialsCostOverride : ResolveMaterialsCost(command);
            SetCost(command, cost);
            button.interactable = selectedUnits == null || selectedUnits.Any((unit) => !command.IsLocked(new CommandContext(unit, new RaycastHit())));
            button.onClick.AddListener(onClick);
            isActive = true;
            if (buttonImage != null)
            {
                // Readable card plate — translucent so icon/text stay clear.
                Color c = defaultButtonColor;
                c.a = Mathf.Clamp(c.a, 0.55f, 0.92f);
                if (c.a < 0.4f) c.a = 0.7f;
                buttonImage.color = c;
                buttonImage.raycastTarget = true;
            }
            ApplyGoalAccent(cardGoal);

            if (command is BuildBuildingCommand buildCmd && buildCmd.Building != null)
            {
                ColonyActManager.GetTileValues(buildCmd.Building, out int score, out _, out string tag);
                EnsureGoalBadge();
                if (goalBadge != null)
                {
                    goalBadge.gameObject.SetActive(true);
                    // Heat/Atmos: spell out the partner so pink Atmos cards are not read as Water.
                    if (tag == "Heat")
                    {
                        goalBadge.SetText($"+{score}\nPlay w/ Atmos\n→ Water card");
                        goalBadge.fontSize = 12f;
                        StretchGoalBadgeForCombo();
                    }
                    else if (tag == "Air")
                    {
                        goalBadge.SetText($"+{score}\nPlay w/ Heat\n→ Water card");
                        goalBadge.fontSize = 12f;
                        StretchGoalBadgeForCombo();
                    }
                    else if (!string.IsNullOrEmpty(goalKey))
                    {
                        goalBadge.SetText($"+{score}\n{TerraformingGoalColors.ShortLabel(goalKey)}");
                        goalBadge.fontSize = 14f;
                        ResetGoalBadgeLayout();
                    }
                    else
                    {
                        goalBadge.SetText($"+{score}");
                        goalBadge.fontSize = 14f;
                        ResetGoalBadgeLayout();
                    }
                }
            }

            if (tooltip != null)
            {
                try
                {
                    tooltip.SetText(GetTooltipText(command, cardGoal));
                }
                catch (System.Exception)
                {
                    // Tooltip text component may not be properly set up — ignore
                }
            }

            RefreshCardTextContrast();
        }

        public void Disable()
        {
            SetIcon(null);
            SetLabel(null);
            SetCost(null, -1);
            ClearGoalAccent();
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }
            isActive = false;
            SetPicked(false);
            slotHandIndex = -1;
            slotIsSurvey = false;
            CancelInvoke();

            // Empty slots should not leave opaque chrome boxes on screen.
            if (buttonImage != null)
            {
                Color c = buttonImage.color;
                c.a = 0f;
                buttonImage.color = c;
                buttonImage.raycastTarget = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActive && tooltip != null)
            {
                Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
            CancelInvoke();
        }

        private void ShowTooltip()
        {
            if (tooltip != null)
            {
                tooltip.Show();
                tooltip.RectTransform.position = new Vector2(
                    rectTransform.position.x + rectTransform.rect.width / 2f,
                    rectTransform.position.y + rectTransform.rect.height / 2f
                );
            }
        }

        /// <summary>
        /// Keep title + cost inside the card face (previous layout hung text below the button).
        /// </summary>
        private void EnsureCardTextLayout()
        {
            if (label == null)
            {
                GameObject labelGO = new GameObject("Label", typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(transform, false);
                label = labelGO.GetComponent<TextMeshProUGUI>();
            }

            label.fontSize = 22f;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 22f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = Color.black;
            label.fontStyle = FontStyles.Bold;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = new Vector4(8f, 4f, 8f, 6f);

            RectTransform labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.04f, 0.02f);
            labelRt.anchorMax = new Vector2(0.96f, 0.32f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            labelRt.pivot = new Vector2(0.5f, 0f);
            RefreshCardTextContrast();

            if (costLabel == null)
            {
                GameObject costGO = new GameObject("Cost", typeof(TextMeshProUGUI));
                costGO.transform.SetParent(transform, false);
                costLabel = costGO.GetComponent<TextMeshProUGUI>();
            }

            // Top-left cost chip — always above the icon, hard to miss.
            costLabel.fontSize = 20f;
            costLabel.fontStyle = FontStyles.Bold;
            costLabel.alignment = TextAlignmentOptions.TopLeft;
            costLabel.color = Color.black;
            costLabel.richText = true;
            costLabel.raycastTarget = false;
            costLabel.margin = new Vector4(10f, 8f, 4f, 2f);
            if (label != null && label.font != null) costLabel.font = label.font;
            RectTransform costRt = costLabel.rectTransform;
            costRt.anchorMin = new Vector2(0.04f, 0.70f);
            costRt.anchorMax = new Vector2(0.62f, 0.98f);
            costRt.offsetMin = Vector2.zero;
            costRt.offsetMax = Vector2.zero;
            costRt.pivot = new Vector2(0f, 1f);
            label.transform.SetAsLastSibling();
            costLabel.transform.SetAsLastSibling();
        }

        public void BindSlot(int handIndex, bool surveyCard)
        {
            slotHandIndex = handIndex;
            slotIsSurvey = surveyCard;
        }

        public void RefreshPicked(int pickedHand, bool surveyLive)
        {
            bool on = isActive && slotHandIndex >= 0
                && (slotHandIndex == pickedHand || (slotIsSurvey && surveyLive));
            SetPicked(on);
        }

        public void SetPicked(bool value)
        {
            if (picked == value) return;
            picked = value;
            EnsureGoalOutline();
            if (goalOutline != null)
            {
                goalOutline.enabled = value;
                goalOutline.effectColor = new Color(1f, 0.82f, 0.15f, 1f);
                goalOutline.effectDistance = new Vector2(4f, -4f);
            }
            if (!value)
                transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!picked)
                return;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6.5f));
            float scale = 1.04f + bob * 0.08f;
            transform.localScale = new Vector3(scale, scale + bob * 0.04f, 1f);
        }

        void RefreshCardTextContrast()
        {
            Color plate = buttonImage != null ? buttonImage.color : defaultButtonColor;
            if (label != null) ApplyReadableCardText(label, plate);
            if (costLabel != null) ApplyReadableCardText(costLabel, plate);
            if (goalBadge != null) ApplyReadableCardText(goalBadge, plate);
        }

        /// <summary>Light text on dark card plates; dark text on pale plates.</summary>
        static void ApplyReadableCardText(TextMeshProUGUI tmp, Color plateColor)
        {
            if (tmp == null) return;
            float lum = 0.2126f * plateColor.r + 0.7152f * plateColor.g + 0.0722f * plateColor.b;
            bool darkPlate = lum < 0.48f;
            tmp.color = darkPlate ? new Color(0.97f, 0.98f, 1f, 1f) : new Color(0.08f, 0.08f, 0.1f, 1f);
            var outline = tmp.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = tmp.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = darkPlate
                ? new Color(0f, 0f, 0f, 0.88f)
                : new Color(1f, 1f, 1f, 0.78f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.enabled = true;
        }

        private void SetLabel(string text)
        {
            EnsureCardTextLayout();
            if (label != null)
            {
                label.text = text ?? "";
                label.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void SetCost(BaseCommand command, int materialsCost)
        {
            EnsureCardTextLayout();
            if (costLabel == null) return;

            if (materialsCost < 0)
            {
                costLabel.gameObject.SetActive(false);
                costLabel.text = "";
                return;
            }

            costLabel.gameObject.SetActive(true);
            int weeks = 1;
            if (command is BuildBuildingCommand bbc && bbc.Building != null)
                weeks = CardDeckController.GetWeekCost(bbc.Building);

            string weekPart = FormatWeekCost(weeks);
            if (materialsCost > 0)
                costLabel.text = $"{weekPart}\n{materialsCost} Mat";
            else
                costLabel.text = weekPart;
            RefreshCardTextContrast();
        }

        private static string FormatWeekCost(int weeks)
        {
            if (weeks <= 0) return "0 Weeks";
            if (weeks == 1) return "1 Week";
            return $"{weeks} Weeks";
        }

        private static int ResolveMaterialsCost(BaseCommand command)
        {
            if (command is BuildBuildingCommand buildingCommand && buildingCommand.Building != null)
            {
                return ReservedSiteBuildUtility.GetMaterialsCost(buildingCommand.Building);
            }

            if (command is BuildUnitCommand unitCommand && unitCommand.Unit?.Cost != null)
            {
                return Mathf.FloorToInt(
                    unitCommand.Unit.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic
                    + unitCommand.Unit.Cost.Gas * Supplies.GasToMaterialsRateStatic);
            }

            if (command is PlayCardCommand playCard)
            {
                return Mathf.Max(0, playCard.MaterialsCost);
            }

            return 0;
        }

        private void SetIcon(Sprite icon)
        {
            if (this.icon == null) return;

            if (icon == null)
            {
                this.icon.enabled = false;
            }
            else
            {
                this.icon.sprite = icon;
                this.icon.enabled = true;
                this.icon.color = Color.white;
                this.icon.preserveAspect = true;
                this.icon.type = Image.Type.Simple;
            }
        }

        /// <summary>
        /// Prologue hand: show the mineral price. Grey the card when short but keep clicks —
        /// PlayBuildingCard shows the same failure message.
        /// </summary>
        public void ApplyPrologueMinerals(int cost, int have)
        {
            if (cost <= 0 || !isActive) return;
            bool shortStock = have < cost;
            if (costLabel != null)
            {
                costLabel.gameObject.SetActive(true);
                costLabel.text = shortStock ? $"Need {cost}\nhave {have}" : $"{cost} minerals";
                costLabel.color = shortStock ? new Color(1f, 0.62f, 0.55f, 1f) : costLabel.color;
            }
            if (!shortStock)
            {
                if (button != null)
                    button.interactable = true;
                if (buttonImage != null)
                {
                    Color c = defaultButtonColor;
                    c.a = Mathf.Clamp(c.a, 0.55f, 0.92f);
                    if (c.a < 0.4f) c.a = 0.7f;
                    buttonImage.color = c;
                }
                if (icon != null)
                    icon.color = Color.white;
                RefreshCardTextContrast();
                return;
            }

            if (button != null)
                button.interactable = true;
            if (buttonImage != null)
                buttonImage.color = new Color(0.38f, 0.38f, 0.42f, 0.92f);
            if (icon != null)
                icon.color = new Color(0.45f, 0.45f, 0.48f, 0.9f);
            if (goalOutline != null)
                goalOutline.effectColor = new Color(0.3f, 0.3f, 0.32f, 0.5f);
            RefreshCardTextContrast();
            if (tooltip != null)
                tooltip.SetText($"Need {cost} minerals to build this. You have {have}. End Week while a mineral mine works, or place a cheaper Solar Array first.");
        }

        private void ApplyGoalAccent(string goal)
        {
            goalKey = TerraformingGoalColors.IsSectorCompletionGoal(goal) ? goal : string.Empty;
            if (string.IsNullOrEmpty(goalKey))
            {
                ClearGoalAccent();
                return;
            }

            Color accent = TerraformingGoalColors.ForGoal(goalKey);

            if (label != null)
                label.richText = true;

            if (buttonImage != null)
            {
                Color plate = Color.Lerp(defaultButtonColor, accent, 0.28f);
                plate.a = Mathf.Clamp(buttonImage.color.a, 0.55f, 0.92f);
                buttonImage.color = plate;
            }

            EnsureGoalOutline();
            if (goalOutline != null)
            {
                goalOutline.enabled = !string.IsNullOrEmpty(goalKey);
                goalOutline.effectColor = accent;
            }

            EnsureGoalBadge();
            if (goalBadge != null)
            {
                bool show = !string.IsNullOrEmpty(goalKey);
                goalBadge.gameObject.SetActive(show);
                if (show)
                    goalBadge.SetText(TerraformingGoalColors.ShortLabel(goalKey));
            }

            RefreshCardTextContrast();
        }

        private void ClearGoalAccent()
        {
            goalKey = string.Empty;
            if (buttonImage != null) buttonImage.color = defaultButtonColor;
            if (goalOutline != null) goalOutline.enabled = false;
            if (goalBadge != null) goalBadge.gameObject.SetActive(false);
            RefreshCardTextContrast();
        }

        private void EnsureGoalOutline()
        {
            if (goalOutline != null) return;
            goalOutline = GetComponent<Outline>();
            if (goalOutline == null)
            {
                goalOutline = gameObject.AddComponent<Outline>();
            }
            goalOutline.effectDistance = new Vector2(2f, -2f);
            goalOutline.useGraphicAlpha = true;
        }

        private void EnsureGoalBadge()
        {
            if (goalBadge != null) return;

            GameObject badgeGO = new GameObject("Goal Badge", typeof(TextMeshProUGUI));
            badgeGO.transform.SetParent(transform, false);
            goalBadge = badgeGO.GetComponent<TextMeshProUGUI>();
            goalBadge.fontSize = 14f;
            goalBadge.fontStyle = FontStyles.Bold;
            goalBadge.alignment = TextAlignmentOptions.TopRight;
            goalBadge.richText = true;
            goalBadge.raycastTarget = false;

            RectTransform rt = goalBadge.rectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.86f);
            rt.anchorMax = new Vector2(0.95f, 0.98f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (label != null && label.font != null)
            {
                goalBadge.font = label.font;
            }

            badgeGO.SetActive(false);
        }

        /// <summary>Give Heat/Air combo hints enough vertical room on the card face.</summary>
        private void StretchGoalBadgeForCombo()
        {
            if (goalBadge == null) return;
            RectTransform rt = goalBadge.rectTransform;
            rt.anchorMin = new Vector2(0.04f, 0.72f);
            rt.anchorMax = new Vector2(0.96f, 0.98f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            goalBadge.alignment = TextAlignmentOptions.TopRight;
            goalBadge.textWrappingMode = TextWrappingModes.Normal;
            goalBadge.overflowMode = TextOverflowModes.Overflow;
        }

        private void ResetGoalBadgeLayout()
        {
            if (goalBadge == null) return;
            RectTransform rt = goalBadge.rectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.86f);
            rt.anchorMax = new Vector2(0.95f, 0.98f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private string GetTooltipText(BaseCommand command, string cardGoal)
        {
            string tooltipText = command.Name + "\n";

            BuildingSO building = null;
            if (command is BuildBuildingCommand bbc) building = bbc.Building;

            if (building != null)
            {
                ColonyActManager.GetTileValues(building, out int score, out float hab, out string tag);
                tooltipText = $"<b>+{score} Score</b>  [{tag}]\n{command.Name}\n";
                int weeks = CardDeckController.GetWeekCost(building);
                if (weeks <= 0)
                    tooltipText += "Costs <b>0 Weeks</b> when played.\n";
                else if (weeks == 1)
                    tooltipText += "Costs <b>1 Week</b> when played.\n";
                else
                    tooltipText += $"Costs <b>{weeks} Weeks</b> when played.\n";
                int matCost = ReservedSiteBuildUtility.GetMaterialsCost(building);
                if (matCost > 0)
                    tooltipText += $"Costs <b>{matCost} Materials</b> to place.\n";
                float upkeep = PowerGridManager.GetBuildingPowerUpkeep(building);
                if (upkeep > 0f)
                    tooltipText += $"Needs <b>{upkeep:0.#} Power</b> upkeep to place.\n";
                else
                    tooltipText += "No power upkeep (places freely).\n";
                tooltipText += "Stack near other tiles — <b>combos</b> drive score and faster climate.\n";
                tooltipText += "Right-click building → <b>Demolish</b> (Materials refund). Delete key also works.\n";
                if (hab > 0f) tooltipText += $"Habitability +{hab:F0}\n";

                if (tag == "Heat" || tag == "Air" || tag == "Water")
                    tooltipText += "<b>Climate combo:</b> Edge-join Heat/Air/Water for rate boosts + missing channel cards.\n";
                else if (tag == "Power")
                    tooltipText += "<b>Combo:</b> Next to Industry → Life card; next to Anchor → Mine or Heat card.\n";
                else if (tag == "Industry")
                    tooltipText += "<b>Combo:</b> Next to Power → Life (Oxygen) card.\n";
                else if (tag == "Life")
                    tooltipText += "<b>Combo:</b> Next to Water → housing card.\n";
                else if (BuildingSiteRegistry.IsMineBuilding(building))
                    tooltipText += "Builds automatically on a matching discovered deposit.\n";
            }
            else if (!string.IsNullOrEmpty(cardGoal) && TerraformingGoalColors.IsSectorCompletionGoal(cardGoal))
            {
                tooltipText =
                    $"{TerraformingGoalColors.Colorize(TerraformingGoalColors.ShortLabel(cardGoal), cardGoal)}\n" +
                    $"{command.Name}\n";
            }

            int cost = ResolveMaterialsCost(command);
            if (cost > 0)
            {
                tooltipText += string.Format(MATERIALS_FORMAT, cost) + ". ";
            }

            if (command.IsLocked(new CommandContext(Owner.Player1, null, new RaycastHit()))
                && command is IUnlockableCommand unlockableCommand)
            {
                UnlockableSO[] dependencies = unlockableCommand.GetUnmetDependencies(Owner.Player1);

                if (dependencies.Length > 0)
                {
                    tooltipText += "\nRequires: ";
                }

                for (int i = 0; i < dependencies.Length; i++)
                {
                    tooltipText += i == dependencies.Length - 1
                        ? string.Format(DEPENDENCY_FORMAT_NO_COMMA, dependencies[i].Name)
                        : string.Format(DEPENDENCY_FORMAT_COMMA, dependencies[i].Name);
                }
            }

            if (command is PlayCardCommand)
            {
                return string.IsNullOrEmpty(cardGoal) || !TerraformingGoalColors.IsSectorCompletionGoal(cardGoal)
                    ? command.Name
                    : $"{TerraformingGoalColors.Colorize(TerraformingGoalColors.ShortLabel(cardGoal), cardGoal)}\n{command.Name}";
            }

            return tooltipText;
        }
    }
}
