using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using System.Collections.Generic;

namespace GameDevTV.RTS.UI.Containers
{
    public class TechTreeUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TechTreeSO techTreeSO;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject techTreeItemPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI tcBalanceText;
        private GameObject summaryPanel;

        private void OnEnable()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            GenerationManager.OnTerraCoinsChanged += UpdateBalanceText;
            UpdateBalanceText(GenerationManager.Instance != null ? GenerationManager.Instance.TotalTerraCoins : 0);
        }

        private void OnDisable()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            GenerationManager.OnTerraCoinsChanged -= UpdateBalanceText;
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Open(GameObject parentPanel)
        {
            Debug.Log("[TechTreeUI] Open called.");
            summaryPanel = parentPanel;

            // Ensure the root GameObject is active, otherwise setting the child panel to active does nothing!
            if (!gameObject.activeSelf)
            {
                Debug.LogWarning("[TechTreeUI] Root GameObject was disabled! Enabling it now...");
                gameObject.SetActive(true);
            }

            if (panel != null) 
            {
                panel.SetActive(true);
            }
            else
            {
                Debug.LogError("[TechTreeUI] 'panel' reference is NULL! Please assign it in the Inspector.");
            }

            if (techTreeSO == null || contentContainer == null || techTreeItemPrefab == null)
            {
                Debug.LogError($"[TechTreeUI] Missing references! techTreeSO: {techTreeSO != null}, container: {contentContainer != null}, prefab: {techTreeItemPrefab != null}");
            }

            PopulateUpgrades();
            UpdateBalanceText(GenerationManager.Instance != null ? GenerationManager.Instance.TotalTerraCoins : 0);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (summaryPanel != null) summaryPanel.SetActive(true);
        }

        private void UpdateBalanceText(int balance)
        {
            if (tcBalanceText != null)
            {
                tcBalanceText.text = $"Available TC: {balance}";
            }
        }

        private void PopulateUpgrades()
        {
            if (techTreeSO == null || contentContainer == null || techTreeItemPrefab == null) return;

            // Clear existing items
            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }

            // Find available upgrades
            foreach (var unlockable in techTreeSO.AllUnlockables)
            {
                if (unlockable is UpgradeSO upgrade)
                {
                    bool isUnlocked = techTreeSO.IsUnlocked(Owner.Player1, upgrade);
                    bool isResearched = techTreeSO.IsResearched(Owner.Player1, upgrade);

                    if (isUnlocked && !isResearched)
                    {
                        GameObject itemObj = Instantiate(techTreeItemPrefab, contentContainer);
                        if (itemObj.TryGetComponent<TechTreeItemUI>(out var itemUI))
                        {
                            itemUI.Setup(upgrade);
                        }
                    }
                }
            }
        }
    }
}
