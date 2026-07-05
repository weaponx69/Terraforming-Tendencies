using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// UI overlay shown when a sector is explored.
    /// Displays what resource nodes were found, what feature was discovered,
    /// and what climate bonuses were granted.
    /// </summary>
    public class ExplorationDiscoveryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI sectorInfoText;
        [SerializeField] private Transform resourceListContainer;
        [SerializeField] private Transform bonusListContainer;
        [SerializeField] private GameObject listItemPrefab;
        [SerializeField] private Button dismissButton;

        private static ExplorationDiscoveryUI _instance;
        public static ExplorationDiscoveryUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ExplorationDiscoveryUI (auto)");
                    _instance = go.AddComponent<ExplorationDiscoveryUI>();
                    DontDestroyOnLoad(go);
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private void Initialize()
        {
            // Create a canvas for the overlay
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            // Create panel
            panel = new GameObject("DiscoveryPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.15f, 0.2f);
            panelRt.anchorMax = new Vector2(0.85f, 0.8f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0.85f);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = new Vector2(20, 0);
            titleRt.offsetMax = new Vector2(-20, -10);
            titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 32;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;

            // Sector info
            var infoGo = new GameObject("SectorInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
            infoGo.transform.SetParent(panel.transform, false);
            var infoRt = infoGo.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0, 0.72f);
            infoRt.anchorMax = new Vector2(1, 0.85f);
            infoRt.offsetMin = new Vector2(20, 0);
            infoRt.offsetMax = new Vector2(-20, -10);
            sectorInfoText = infoGo.GetComponent<TextMeshProUGUI>();
            sectorInfoText.fontSize = 20;
            sectorInfoText.alignment = TextAlignmentOptions.Center;
            sectorInfoText.color = new Color(0.7f, 0.7f, 1f);

            // Resource list label
            var resLabel = new GameObject("ResourceLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            resLabel.transform.SetParent(panel.transform, false);
            var resLabelRt = resLabel.GetComponent<RectTransform>();
            resLabelRt.anchorMin = new Vector2(0.05f, 0.65f);
            resLabelRt.anchorMax = new Vector2(0.45f, 0.72f);
            resLabelRt.offsetMin = Vector2.zero;
            resLabelRt.offsetMax = Vector2.zero;
            var resLabelText = resLabel.GetComponent<TextMeshProUGUI>();
            resLabelText.text = "Resources Discovered:";
            resLabelText.fontSize = 18;
            resLabelText.color = new Color(0.5f, 1f, 0.5f);
            resLabelText.fontStyle = FontStyles.Bold;

            // Resource list container
            var resContainer = new GameObject("ResourceList", typeof(RectTransform));
            resContainer.transform.SetParent(panel.transform, false);
            var resContainerRt = resContainer.GetComponent<RectTransform>();
            resContainerRt.anchorMin = new Vector2(0.05f, 0.35f);
            resContainerRt.anchorMax = new Vector2(0.45f, 0.65f);
            resContainerRt.offsetMin = Vector2.zero;
            resContainerRt.offsetMax = Vector2.zero;
            resourceListContainer = resContainer.transform;

            // Bonus list label
            var bonusLabel = new GameObject("BonusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            bonusLabel.transform.SetParent(panel.transform, false);
            var bonusLabelRt = bonusLabel.GetComponent<RectTransform>();
            bonusLabelRt.anchorMin = new Vector2(0.55f, 0.65f);
            bonusLabelRt.anchorMax = new Vector2(0.95f, 0.72f);
            bonusLabelRt.offsetMin = Vector2.zero;
            bonusLabelRt.offsetMax = Vector2.zero;
            var bonusLabelText = bonusLabel.GetComponent<TextMeshProUGUI>();
            bonusLabelText.text = "Climate Bonuses:";
            bonusLabelText.fontSize = 18;
            bonusLabelText.color = new Color(1f, 0.8f, 0.3f);
            bonusLabelText.fontStyle = FontStyles.Bold;

            // Bonus list container
            var bonusContainer = new GameObject("BonusList", typeof(RectTransform));
            bonusContainer.transform.SetParent(panel.transform, false);
            var bonusContainerRt = bonusContainer.GetComponent<RectTransform>();
            bonusContainerRt.anchorMin = new Vector2(0.55f, 0.35f);
            bonusContainerRt.anchorMax = new Vector2(0.95f, 0.65f);
            bonusContainerRt.offsetMin = Vector2.zero;
            bonusContainerRt.offsetMax = Vector2.zero;
            bonusListContainer = bonusContainer.transform;

            // Dismiss button
            var btnGo = new GameObject("DismissButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panel.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.35f, 0.05f);
            btnRt.anchorMax = new Vector2(0.65f, 0.15f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;
            btnGo.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f);

            var btnText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnText.transform.SetParent(btnGo.transform, false);
            var btnTextRt = btnText.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;
            var btnTmp = btnText.GetComponent<TextMeshProUGUI>();
            btnTmp.text = "Dismiss";
            btnTmp.fontSize = 24;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            dismissButton = btnGo.GetComponent<Button>();
            dismissButton.onClick.AddListener(Hide);

            // Create list item prefab
            listItemPrefab = new GameObject("ListItem", typeof(RectTransform), typeof(TextMeshProUGUI));
            listItemPrefab.transform.SetParent(panel.transform, false);
            var itemRt = listItemPrefab.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(200, 30);
            var itemTmp = listItemPrefab.GetComponent<TextMeshProUGUI>();
            itemTmp.fontSize = 16;
            itemTmp.color = Color.white;
            listItemPrefab.SetActive(false);

            panel.SetActive(false);
        }

        /// <summary>
        /// Show the discovery UI for a single explored node, its connections, and bonus rewards.
        /// </summary>
        public void Show(int sectorIndex, List<SectorNode> exploredNodes, ExplorationNodeSO[] bonuses)
        {
            if (panel == null) Initialize();

            panel.SetActive(true);
            titleText.text = $"Node Explored!";
            sectorInfoText.text = $"Sector {sectorIndex} — new discoveries mapped.";

            // Clear previous items
            foreach (Transform child in resourceListContainer) Destroy(child.gameObject);
            foreach (Transform child in bonusListContainer) Destroy(child.gameObject);

            // Show the explored node + its connections
            foreach (var node in exploredNodes)
            {
                if (node == null) continue;

                // Explored node
                string label = node.labelOverride;
                if (string.IsNullOrEmpty(label)) label = node.type.ToString();

                var item = Instantiate(listItemPrefab, resourceListContainer);
                item.SetActive(true);
                item.GetComponent<TMPro.TextMeshProUGUI>().text = $"★ {label} — {node.flavorText}";
                item.GetComponent<TMPro.TextMeshProUGUI>().color = Color.green;

                // Show connections as "?" markers
                foreach (var conn in node.connections)
                {
                    if (conn == null || conn.isExplored) continue;
                    string connLabel = conn.labelOverride;
                    if (string.IsNullOrEmpty(connLabel)) connLabel = conn.type.ToString();
                    var connItem = Instantiate(listItemPrefab, resourceListContainer);
                    connItem.SetActive(true);
                    connItem.GetComponent<TMPro.TextMeshProUGUI>().text = $"  ? {connLabel} (unexplored)";
                    connItem.GetComponent<TMPro.TextMeshProUGUI>().color = Color.yellow;
                }
            }

            // Bonus rewards
            foreach (var bonus in bonuses)
            {
                if (bonus == null) continue;
                var bonusItem = Instantiate(listItemPrefab, bonusListContainer);
                bonusItem.SetActive(true);
                bonusItem.GetComponent<TMPro.TextMeshProUGUI>().text = $"• {bonus.nodeName}: {bonus.description}";
                bonusItem.GetComponent<TMPro.TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.3f);
            }

            // Pause the game while showing discovery
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}