using System;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Represents a single selectable card slot inside the DraftingUI card container.
    ///
    /// Wire in Inspector (on the prefab):
    ///   - cardNameText    : large card title TextMeshProUGUI
    ///   - descriptionText : body description TextMeshProUGUI
    ///   - iconImage       : card artwork Image
    ///   - selectButton    : the Button component the player clicks
    ///   - glowOutline     : an Image or Outline used for hover highlight (optional)
    /// </summary>
    public class CardSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Card Display")]
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button selectButton;

        [Header("Hover Effect")]
        [SerializeField] private float hoverScaleMultiplier = 1.07f;
        [SerializeField] private float hoverAnimSpeed = 8f;
        [SerializeField] private Image glowOutline;

        private BlueprintCardSO cardData;
        private Action<BlueprintCardSO> onSelected;
        private Vector3 baseScale;
        private Vector3 targetScale;

        private void Awake()
        {
            baseScale = transform.localScale;
            targetScale = baseScale;
        }

        private void Update()
        {
            // Smooth scale interpolation for hover pop
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * hoverAnimSpeed);
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void Initialize(BlueprintCardSO card, Action<BlueprintCardSO> selectionCallback)
        {
            cardData = card;
            onSelected = selectionCallback;

            if (cardNameText != null)
            {
                string goal = card.GetCardGoal();
                Color accent = TerraformingGoalColors.ForGoal(goal);
                cardNameText.color = accent;
                cardNameText.richText = true;
                cardNameText.SetText(
                    $"{TerraformingGoalColors.Colorize("[" + TerraformingGoalColors.ShortLabel(goal) + "]", goal)} {card.cardName}");
            }

            if (descriptionText != null)
                descriptionText.SetText(card.cardDescription);

            if (iconImage != null)
            {
                iconImage.sprite = card.icon;
                iconImage.enabled = card.icon != null;
            }

            if (selectButton != null)
                selectButton.onClick.AddListener(HandleClick);

            if (glowOutline != null)
            {
                glowOutline.enabled = false;
                glowOutline.color = TerraformingGoalColors.ForGoal(card.GetCardGoal());
            }
        }

        // ── Hover ──────────────────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = baseScale * hoverScaleMultiplier;
            if (glowOutline != null)
                glowOutline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = baseScale;
            if (glowOutline != null)
                glowOutline.enabled = false;
        }

        // ── Selection ──────────────────────────────────────────────────────────────

        private void HandleClick()
        {
            if (selectButton != null)
                selectButton.interactable = false;

            onSelected?.Invoke(cardData);
        }
    }
}
