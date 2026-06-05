using System.Collections.Generic;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// A single growth-package card. Lists each item in the package as a toggleable row
    /// so the player can veto individual units, with a live cost total. The Build button
    /// invokes the callback; the proposal's per-item Enabled flags carry the player's choices.
    /// </summary>
    public class ProposalEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI siteNameText;
        [SerializeField] private TextMeshProUGUI resourceCountText; // reused as the live total
        [SerializeField] private Button selectButton;

        // Whether a drone is currently available to build worker-built items (e.g. Oxygen Processor).
        private bool buildersAvailable;
        // Per-row re-evaluators for builder-gated items, re-run when availability changes.
        private readonly List<System.Action> gatedRows = new List<System.Action>();

        public void Setup(ExpansionProposal proposal, System.Action onSelect)
        {
            gatedRows.Clear();
            buildersAvailable = GreedyAIController.Instance != null && GreedyAIController.Instance.HasAvailableBuilder();

            // Header
if (siteNameText != null)
            {
                string header = proposal.SiteName;
                if (proposal.IsExpansion && proposal.ResourceCount > 0)
                    header += $" ({proposal.ResourceCount} nearby)";
                siteNameText.text = header;
            }

            // Items container (created once, between header and total)
            Transform container = transform.Find("Items");
            if (container == null)
            {
                GameObject go = new GameObject("Items", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 3;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                var le = go.AddComponent<LayoutElement>();
                le.flexibleHeight = 1;
                container = go.transform;
            }
            foreach (Transform c in container) Destroy(c.gameObject);

            // One toggleable row per item
            foreach (var item in proposal.Items)
                CreateRow(container, item, () => UpdateTotal(proposal));

            // Order: header(0), items(1), total(2), button(3)
            container.SetSiblingIndex(1);
            if (resourceCountText != null) resourceCountText.transform.SetSiblingIndex(2);
            if (selectButton != null) selectButton.transform.SetSiblingIndex(3);

            UpdateTotal(proposal);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelect?.Invoke());
                var lbl = selectButton.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.text = "Build Selected";
            }
        }

        private void UpdateTotal(ExpansionProposal p)
        {
            if (resourceCountText != null)
                resourceCountText.text = $"<b>Total: {p.EnabledCost}</b>";
        }

        private void CreateRow(Transform parent, PackageItem item, System.Action onChanged)
        {
            GameObject row = new GameObject("Row_" + item.Name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 26;
            le.flexibleHeight = 0;

            var img = row.GetComponent<Image>();
            var btn = row.GetComponent<Button>();
            btn.targetGraphic = img;

            GameObject labelGO = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(row.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6, 0); lrt.offsetMax = new Vector2(-6, 0);

            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 9; tmp.fontSizeMax = 14;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;

            System.Action refresh = () =>
            {
                if (item.Enabled)
                {
                    img.color = new Color(0.20f, 0.45f, 0.30f, 1f);
                    tmp.text = $"<b>[x]</b> {item.Name}  ({item.Cost})";
                    tmp.color = Color.white;
                }
                else
                {
                    img.color = new Color(0.35f, 0.20f, 0.20f, 1f);
                    tmp.text = $"[  ] <s>{item.Name}  ({item.Cost})</s>";
                    tmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            };
            refresh();

            btn.onClick.AddListener(() =>
            {
                item.Enabled = !item.Enabled;
                refresh();
                onChanged?.Invoke();
            });
        }
    }
}