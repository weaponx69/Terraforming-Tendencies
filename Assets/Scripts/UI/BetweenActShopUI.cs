using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Permanent between-sector shop: after clearing an Act, spend Materials on
    /// offer cards, then continue into the next sector with Solar + Command Post seeded.
    /// </summary>
    public class BetweenActShopUI : MonoBehaviour
    {
        public static BetweenActShopUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int OfferCount = 3;
        private const int RerollCost = 25;
        private const float ShopPriceScale = 0.85f;

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI materialsText;
        private TextMeshProUGUI hintText;
        private readonly List<OfferSlot> slots = new();
        private readonly List<BlueprintCardSO> currentOffers = new();
        private Button continueButton;
        private Button rerollButton;
        private float savedTimeScale = 1f;

        private struct OfferSlot
        {
            public GameObject Root;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI Desc;
            public Button Buy;
            public BlueprintCardSO Card;
            public int Cost;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<BetweenActShopUI>() != null) return;
            var go = new GameObject("BetweenActShopUI");
            go.AddComponent<BetweenActShopUI>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Instance = this;
            EnsureUi();
            Hide();
        }

        private void OnEnable()
        {
            ColonyActManager.OnBetweenActShopRequested += Open;
        }

        private void OnDisable()
        {
            ColonyActManager.OnBetweenActShopRequested -= Open;
        }

        public void Open()
        {
            // Rebuild each open so layout fixes apply even if a prior oversized panel was cached.
            RebuildUi();
            IsOpen = true;
            savedTimeScale = Time.timeScale;
            if (Time.timeScale > 0.01f)
                Time.timeScale = 0f;

            RefreshOffers(forceNew: true);
            UpdateHeader();
            root.SetActive(true);
            Debug.Log("[BetweenActShop] Opened between-sector shop.");
        }

        public void Hide()
        {
            IsOpen = false;
            if (root != null) root.SetActive(false);
        }

        private void RebuildUi()
        {
            if (root != null)
            {
                Destroy(root);
                root = null;
            }
            slots.Clear();
            titleText = null;
            materialsText = null;
            hintText = null;
            continueButton = null;
            rerollButton = null;
            EnsureUi();
        }

        private void ContinueToNextSector()
        {
            Hide();
            Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
            ColonyActManager.Instance?.CompleteBetweenActShopAndAdvance();
        }

        private void UpdateHeader()
        {
            int mats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            int cleared = ColonyActManager.Instance != null ? ColonyActManager.Instance.CurrentAct : 0;
            int next = cleared + 1;
            if (titleText != null)
                titleText.text = $"SECTOR SUPPLY DEPOT\nAct {cleared} cleared → preparing Act {next}";
            if (materialsText != null)
                materialsText.text = $"Materials: {mats}";
            if (hintText != null)
                hintText.text = "Buy tiles for the next sector, then continue. Solar + Command Post are granted when you leave.";
        }

        private void RefreshOffers(bool forceNew)
        {
            if (forceNew || currentOffers.Count == 0)
                BuildOfferList();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (i >= currentOffers.Count || currentOffers[i] == null)
                {
                    slot.Root.SetActive(false);
                    slots[i] = slot;
                    continue;
                }

                BlueprintCardSO card = currentOffers[i];
                int cost = PriceFor(card);
                slot.Card = card;
                slot.Cost = cost;
                slot.Root.SetActive(true);
                if (slot.Title != null) slot.Title.text = string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName;
                if (slot.Price != null) slot.Price.text = $"{cost} Materials";
                if (slot.Desc != null)
                {
                    string goal = TerraformingGoalColors.GetSectorGoalForCard(card);
                    string goalLine = string.IsNullOrEmpty(goal) ? "Support" : TerraformingGoalColors.DisplayName(goal);
                    slot.Desc.text = string.IsNullOrEmpty(card.cardDescription)
                        ? goalLine
                        : $"{goalLine}\n{card.cardDescription}";
                }

                int captured = i;
                slot.Buy.onClick.RemoveAllListeners();
                slot.Buy.onClick.AddListener(() => TryBuy(captured));
                slots[i] = slot;
                RefreshBuyInteractable(i);
            }

            if (rerollButton != null)
            {
                int mats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                rerollButton.interactable = mats >= RerollCost;
            }
        }

        private void RefreshBuyInteractable(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            var slot = slots[index];
            if (slot.Buy == null || slot.Card == null) return;
            int mats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            slot.Buy.interactable = mats >= slot.Cost;
        }

        private void TryBuy(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            var slot = slots[index];
            if (slot.Card == null) return;

            int mats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            if (mats < slot.Cost)
            {
                ExplorationManager.NotifyExplorationFailed($"Need {slot.Cost} Materials (have {mats}).");
                return;
            }

            Supplies.UpdateMaterials(Owner.Player1, mats - slot.Cost);
            CardDeckController.Instance?.AddCardToHandFromShop(slot.Card);
            currentOffers[index] = null;
            slot.Card = null;
            slot.Root.SetActive(false);
            slots[index] = slot;
            UpdateHeader();
            for (int i = 0; i < slots.Count; i++) RefreshBuyInteractable(i);
            if (rerollButton != null)
            {
                int left = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m2) ? m2 : 0;
                rerollButton.interactable = left >= RerollCost;
            }
        }

        private void TryReroll()
        {
            int mats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
            if (mats < RerollCost)
            {
                ExplorationManager.NotifyExplorationFailed($"Reroll costs {RerollCost} Materials.");
                return;
            }
            Supplies.UpdateMaterials(Owner.Player1, mats - RerollCost);
            RefreshOffers(forceNew: true);
            UpdateHeader();
        }

        private void BuildOfferList()
        {
            currentOffers.Clear();
            var deck = CardDeckController.Instance;
            if (deck == null) return;

            var pool = new List<BlueprintCardSO>();
            foreach (var card in deck.MasterDeck)
            {
                if (card == null) continue;
                if (CardDeckController.IsExcludedFromColonyDeck(card)) continue;
                if (card is not UnlockBuildingCardSO unlock) continue;
                if (unlock.buildingToUnlock == null) continue;
                if (!DiscoverySystem.IsBuildingGeologicallyAvailable(unlock.buildingToUnlock)) continue;
                // Skip pure starters — those are granted on continue.
                string n = unlock.buildingToUnlock.Name ?? string.Empty;
                if (n.IndexOf("Command Post", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (BuildingSiteRegistry.IsSolarBuilding(unlock.buildingToUnlock)) continue;
                pool.Add(card);
            }

            // Shuffle
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < OfferCount && i < pool.Count; i++)
                currentOffers.Add(pool[i]);
        }

        private static int PriceFor(BlueprintCardSO card)
        {
            int baseCost = card != null ? Mathf.Max(20, card.GetMaterialsPlayCost()) : 40;
            return Mathf.Max(15, Mathf.RoundToInt(baseCost * ShopPriceScale));
        }

        private void EnsureUi()
        {
            if (root != null) return;

            root = new GameObject("BetweenActShopRoot");
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            var dimRt = dim.GetComponent<RectTransform>();
            StretchFull(dimRt);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.02f, 0.04f, 0.08f, 0.82f);
            dimImg.raycastTarget = true;

            // Anchored panel — stays on-screen at common resolutions (no fixed 980×620).
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.22f, 0.28f);
            panelRt.anchorMax = new Vector2(0.78f, 0.72f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.14f, 0.20f, 0.98f);

            titleText = CreateText(panel.transform, "Title", 22f, FontStyles.Bold,
                new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.98f));
            titleText.alignment = TextAlignmentOptions.Center;
            materialsText = CreateText(panel.transform, "Materials", 18f, FontStyles.Bold,
                new Vector2(0.04f, 0.76f), new Vector2(0.50f, 0.84f));
            materialsText.color = new Color(0.85f, 0.95f, 0.55f);
            hintText = CreateText(panel.transform, "Hint", 13f, FontStyles.Normal,
                new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.76f));
            hintText.color = new Color(0.75f, 0.82f, 0.90f);

            float slotW = 0.28f;
            float gap = 0.03f;
            float startX = 0.05f;
            for (int i = 0; i < OfferCount; i++)
            {
                float x0 = startX + i * (slotW + gap);
                float x1 = x0 + slotW;
                var slotGo = new GameObject($"Offer{i}", typeof(RectTransform));
                slotGo.transform.SetParent(panel.transform, false);
                var srt = slotGo.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(x0, 0.22f);
                srt.anchorMax = new Vector2(x1, 0.66f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var bg = slotGo.AddComponent<Image>();
                bg.color = new Color(0.16f, 0.22f, 0.30f, 1f);

                var title = CreateText(slotGo.transform, "CardTitle", 15f, FontStyles.Bold,
                    new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.96f));
                var price = CreateText(slotGo.transform, "Price", 14f, FontStyles.Bold,
                    new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.72f));
                price.color = new Color(1f, 0.85f, 0.35f);
                var desc = CreateText(slotGo.transform, "Desc", 12f, FontStyles.Normal,
                    new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.58f));
                desc.color = new Color(0.8f, 0.86f, 0.92f);

                var buyGo = new GameObject("Buy", typeof(RectTransform));
                buyGo.transform.SetParent(slotGo.transform, false);
                var brt = buyGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.12f, 0.06f);
                brt.anchorMax = new Vector2(0.88f, 0.24f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var buyImg = buyGo.AddComponent<Image>();
                buyImg.color = new Color(0.20f, 0.55f, 0.35f, 1f);
                var buyBtn = buyGo.AddComponent<Button>();
                buyBtn.targetGraphic = buyImg;
                var buyLabel = CreateText(buyGo.transform, "BuyLabel", 14f, FontStyles.Bold,
                    Vector2.zero, Vector2.one);
                buyLabel.text = "BUY";
                buyLabel.alignment = TextAlignmentOptions.Center;

                slots.Add(new OfferSlot
                {
                    Root = slotGo,
                    Title = title,
                    Price = price,
                    Desc = desc,
                    Buy = buyBtn
                });
            }

            rerollButton = CreateBottomButton(panel.transform, "Reroll", $"REROLL ({RerollCost})",
                new Vector2(0.06f, 0.05f), new Vector2(0.36f, 0.16f),
                new Color(0.35f, 0.40f, 0.55f, 1f), TryReroll);

            continueButton = CreateBottomButton(panel.transform, "Continue", "CONTINUE →",
                new Vector2(0.40f, 0.05f), new Vector2(0.94f, 0.16f),
                new Color(0.15f, 0.55f, 0.75f, 1f), ContinueToNextSector);
        }

        private static Button CreateBottomButton(
            Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var text = CreateText(go.transform, "Label", 15f, FontStyles.Bold, Vector2.zero, Vector2.one);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string name, float size, FontStyles style,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
