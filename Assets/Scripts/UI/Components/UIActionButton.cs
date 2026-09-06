using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
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
            SetCost(cost);
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
        }

        public void Disable()
        {
            SetIcon(null);
            SetLabel(null);
            SetCost(-1);
            ClearGoalAccent();
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }
            isActive = false;
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

            label.fontSize = 15f;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 16f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = Color.white;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(6f, 2f, 6f, 2f);

            RectTransform labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0.06f, 0.12f);
            labelRt.anchorMax = new Vector2(0.94f, 0.30f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            labelRt.pivot = new Vector2(0.5f, 0f);

            if (costLabel == null)
            {
                GameObject costGO = new GameObject("Cost", typeof(TextMeshProUGUI));
                costGO.transform.SetParent(transform, false);
                costLabel = costGO.GetComponent<TextMeshProUGUI>();
            }

            // Top-left cost chip — always above the icon, hard to miss.
            costLabel.fontSize = 16f;
            costLabel.fontStyle = FontStyles.Bold;
            costLabel.alignment = TextAlignmentOptions.TopLeft;
            costLabel.color = new Color(1f, 0.92f, 0.45f, 1f);
            costLabel.richText = true;
            costLabel.raycastTarget = false;
            costLabel.margin = new Vector4(8f, 6f, 4f, 2f);
            if (label != null && label.font != null) costLabel.font = label.font;

            RectTransform costRt = costLabel.rectTransform;
            costRt.anchorMin = new Vector2(0.04f, 0.72f);
            costRt.anchorMax = new Vector2(0.55f, 0.98f);
            costRt.offsetMin = Vector2.zero;
            costRt.offsetMax = Vector2.zero;
            costRt.pivot = new Vector2(0f, 1f);
            costLabel.transform.SetAsLastSibling();

            var costOutline = costLabel.GetComponent<UnityEngine.UI.Outline>();
            if (costOutline == null) costOutline = costLabel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            costOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            costOutline.effectDistance = new Vector2(1.5f, -1.5f);
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

        private void SetCost(int materialsCost)
        {
            EnsureCardTextLayout();
            if (costLabel == null) return;

            if (materialsCost < 0)
            {
                costLabel.gameObject.SetActive(false);
                costLabel.text = "";
                return;
            }

            // Never leave paid cards looking blank/"Free" due to a 0 read — floor to 1 for display.
            if (materialsCost == 0)
            {
                costLabel.gameObject.SetActive(true);
                costLabel.text = "Free";
                costLabel.color = new Color(0.65f, 0.95f, 0.7f, 1f);
                return;
            }

            costLabel.gameObject.SetActive(true);
            bool canAfford = Supplies.Materials != null
                && Supplies.Materials.TryGetValue(Owner.Player1, out int have)
                && have >= materialsCost;
            costLabel.text = $"{materialsCost} Mat";
            costLabel.color = canAfford
                ? new Color(1f, 0.92f, 0.45f, 1f)
                : new Color(1f, 0.45f, 0.4f, 1f);
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
            }
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
            {
                label.color = accent;
                label.richText = true;
            }

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
                {
                    goalBadge.color = accent;
                    goalBadge.SetText(TerraformingGoalColors.ShortLabel(goalKey));
                }
            }
        }

        private void ClearGoalAccent()
        {
            goalKey = string.Empty;
            if (label != null) label.color = Color.white;
            if (buttonImage != null) buttonImage.color = defaultButtonColor;
            if (goalOutline != null) goalOutline.enabled = false;
            if (goalBadge != null) goalBadge.gameObject.SetActive(false);
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
            goalBadge.fontSize = 12f;
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

        private string GetTooltipText(BaseCommand command, string cardGoal)
        {
            string tooltipText = command.Name + "\n";

            if (!string.IsNullOrEmpty(cardGoal) && TerraformingGoalColors.IsSectorCompletionGoal(cardGoal))
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
