using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

namespace GameDevTV.RTS.UI.Containers
{
    public class TechTreeItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;

        private UpgradeSO currentUpgrade;
        private int tcCost;

        public void Setup(UpgradeSO upgrade)
        {
            currentUpgrade = upgrade;
            
            if (nameText != null) nameText.text = upgrade.Name;
            if (iconImage != null && upgrade.Icon != null) iconImage.sprite = upgrade.Icon;

            // Use Minerals cost as the TC cost. Fallback to 1 if not set.
            tcCost = (upgrade.Cost != null) ? upgrade.Cost.Minerals : 1;
            
            if (costText != null) costText.text = $"{tcCost} TC";

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
            }

            UpdateButtonState(GenerationManager.Instance != null ? GenerationManager.Instance.TotalTerraCoins : 0);
        }

        private void OnEnable()
        {
            GenerationManager.OnTerraCoinsChanged += UpdateButtonState;
        }

        private void OnDisable()
        {
            GenerationManager.OnTerraCoinsChanged -= UpdateButtonState;
        }

        private void UpdateButtonState(int currentTC)
        {
            if (buyButton != null)
            {
                buyButton.interactable = currentTC >= tcCost;
            }
        }

        private void OnBuyClicked()
        {
            if (GenerationManager.Instance != null && GenerationManager.Instance.SpendTerraCoins(tcCost))
            {
                // Fire the event to unlock the tech
                Bus<UpgradeResearchedEvent>.Raise(new UpgradeResearchedEvent(Owner.Player1, currentUpgrade));
                Debug.Log($"[TechTreeUI] Purchased upgrade: {currentUpgrade.Name} for {tcCost} TC");
                
                // Hide or disable the button once bought
                if (buyButton != null) buyButton.interactable = false;
                if (costText != null) costText.text = "PURCHASED";
            }
        }
    }
}
