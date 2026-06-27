using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI.Components
{
    public class CardUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI effectText;
        [SerializeField] private Button playButton;

        private CardSO currentCard;

        private static readonly Color ColorCommon = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color ColorUncommon = new Color(0.2f, 0.8f, 0.3f, 1f);
        private static readonly Color ColorRare = new Color(0.2f, 0.5f, 1f, 1f);
        private static readonly Color ColorEpic = new Color(0.8f, 0.2f, 1f, 1f);

        public void Setup(CardSO card)
        {
            currentCard = card;
            if (card == null) return;

            if (nameText != null) nameText.text = card.CardName;
            if (iconImage != null && card.Icon != null) iconImage.sprite = card.Icon;
            if (costText != null) costText.text = $"Cost: {card.PlayCost}";
            if (effectText != null) effectText.text = BuildEffectDescription(card);
            if (rarityBorder != null) rarityBorder.color = GetRarityColor(card.Rarity);

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }

            UpdateInteractable();
        }

        private void UpdateInteractable()
        {
            if (playButton != null && currentCard != null)
            {
                bool canPlay = CardDeckManager.Instance != null && CardDeckManager.Instance.CanPlayCard(currentCard);
                playButton.interactable = canPlay;
            }
        }

        private void OnPlayClicked()
        {
            if (currentCard != null && CardDeckManager.Instance != null)
            {
                CardDeckManager.Instance.PlayCard(currentCard);
            }
        }

        private string BuildEffectDescription(CardSO card)
        {
            string effect = card.EffectType switch
            {
                CardEffectType.Biomass => $"+{card.EffectAmount:F0} Biomass",
                CardEffectType.Oxygen => $"+{card.EffectAmount:F0} Oxygen",
                CardEffectType.Power => $"+{card.EffectAmount:F0} Power",
                CardEffectType.Population => $"+{card.EffectAmount:F0} Pop",
                CardEffectType.Materials => $"+{card.EffectAmount:F0} Mats",
                CardEffectType.Temperature => $"+{card.EffectAmount:F1} Temp",
                CardEffectType.Atmosphere => $"+{card.EffectAmount:F2} Atmos",
                CardEffectType.Water => $"+{card.EffectAmount:F0} Water",
                _ => ""
            };

            if (!string.IsNullOrEmpty(effect))
                return $"<b>{card.CardName}</b>\n{effect}\n{card.Description}";

            return $"<b>{card.CardName}</b>\n{card.Description}";
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => ColorCommon,
                CardRarity.Uncommon => ColorUncommon,
                CardRarity.Rare => ColorRare,
                CardRarity.Epic => ColorEpic,
                _ => ColorCommon
            };
        }
    }
}