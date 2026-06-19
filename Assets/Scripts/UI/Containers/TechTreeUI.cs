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
        
        [Header("Grid Layout Overrides")]
        [Tooltip("If true, the script will forcefully resize the grid cells to match the values below.")]
        [SerializeField] private bool overrideGridCellSize = true;
        [SerializeField] private Vector2 gridCellSize = new Vector2(350f, 140f);
        [SerializeField] private Vector2 gridSpacing = new Vector2(20f, 20f);

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

        private void Awake()
        {
            if (panel == null)
            {
                var t = transform.Find("Panel") ?? transform.Find("Tech Tree Panel");
                if (t != null) panel = t.gameObject;
                else if (transform.childCount > 0) panel = transform.GetChild(0).gameObject;
            }

            if (closeButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.Contains("Close", System.StringComparison.OrdinalIgnoreCase) ||
                        b.name.Contains("Back", System.StringComparison.OrdinalIgnoreCase))
                    {
                        closeButton = b;
                        break;
                    }
                }
            }

            if (tcBalanceText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.name.Contains("Balance", System.StringComparison.OrdinalIgnoreCase) ||
                        t.name.Contains("TC", System.StringComparison.OrdinalIgnoreCase) ||
                        t.name.Contains("Coin", System.StringComparison.OrdinalIgnoreCase))
                    {
                        tcBalanceText = t;
                        break;
                    }
                }
            }

            if (techTreeSO == null)
            {
                var configs = Resources.FindObjectsOfTypeAll<TechTreeSO>();
                if (configs.Length > 0) techTreeSO = configs[0];
            }
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
                if (!panel.activeInHierarchy)
                {
                    Debug.LogError("[TechTreeUI] CRITICAL: Tech Tree Panel was activated, but is NOT active in the hierarchy! A parent GameObject is likely disabled.");
                }
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
            gameObject.SetActive(false); // Also disable the root GameObject to release UI raycasts!
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

            // Apply manual sizing overrides if requested
            if (overrideGridCellSize)
            {
                if (contentContainer.TryGetComponent<GridLayoutGroup>(out var grid))
                {
                    grid.cellSize = gridCellSize;
                    grid.spacing = gridSpacing;
                }
                else if (contentContainer.TryGetComponent<VerticalLayoutGroup>(out var vlg))
                {
                    vlg.spacing = gridSpacing.y;
                }
            }

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
                        
                        if (overrideGridCellSize)
                        {
                            var le = itemObj.GetComponent<UnityEngine.UI.LayoutElement>();
                            if (le == null) le = itemObj.AddComponent<UnityEngine.UI.LayoutElement>();
                            le.minWidth = gridCellSize.x;
                            le.minHeight = gridCellSize.y;
                            le.preferredWidth = gridCellSize.x;
                            le.preferredHeight = gridCellSize.y;
                        }

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
