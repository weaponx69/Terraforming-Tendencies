using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
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
        private RectTransform rectTransform;
        private Button button;
        private Image buttonImage;
        private Outline goalOutline;
        private TextMeshProUGUI goalBadge;
        private Color defaultButtonColor = Color.white;
        private string goalKey = string.Empty;

        private static readonly string BIOMASS_FORMAT = "{0} <color=#7A5A00>Biomass</color>. ";
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
            EnableFor(command, selectedUnits, onClick, null);
        }

        public void EnableFor(
            BaseCommand command,
            IEnumerable<AbstractCommandable> selectedUnits,
            UnityAction onClick,
            string cardGoal)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            SetIcon(command.Icon);
            SetLabel(command.Name);
            button.interactable = selectedUnits == null || selectedUnits.Any((unit) => !command.IsLocked(new CommandContext(unit, new RaycastHit())));
            button.onClick.AddListener(onClick);
            isActive = true;
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
            ClearGoalAccent();
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }
            isActive = false;
            CancelInvoke();
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

        private void SetLabel(string text)
        {
            if (label == null)
            {
                GameObject labelGO = new GameObject("Label", typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(transform, false);
                label = labelGO.GetComponent<TextMeshProUGUI>();
                label.fontSize = 14;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.richText = true;
                RectTransform rt = label.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.offsetMin = new Vector2(0, -20);
                rt.offsetMax = new Vector2(0, 0);
            }

            if (label != null)
            {
                label.text = text ?? "";
            }
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
                buttonImage.color = Color.Lerp(defaultButtonColor, accent, 0.35f);
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
            goalBadge.fontSize = 10f;
            goalBadge.fontStyle = FontStyles.Bold;
            goalBadge.alignment = TextAlignmentOptions.TopRight;
            goalBadge.richText = true;
            goalBadge.raycastTarget = false;

            RectTransform rt = goalBadge.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-2f, -2f);
            rt.sizeDelta = new Vector2(-4f, 14f);

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

            SupplyCostSO supplyCost = null;
            if (command is BuildUnitCommand unitCommand)
            {
                supplyCost = unitCommand.Unit.Cost;
            }
            else if (command is BuildBuildingCommand buildingCommand)
            {
                supplyCost = buildingCommand.Building.Cost;
            }

            if (supplyCost != null)
            {
                int cost = Mathf.FloorToInt(supplyCost.Minerals * Supplies.MineralsToMaterialsRateStatic
                                      + supplyCost.Gas * Supplies.GasToMaterialsRateStatic);
                if (cost > 0)
                {
                    tooltipText += string.Format(BIOMASS_FORMAT, cost);
                }
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
