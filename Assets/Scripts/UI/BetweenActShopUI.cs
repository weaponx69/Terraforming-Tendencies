using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Between-Act roguelike upgrade shop. Spend Terra-Coins (carry across Acts).
    /// Colony Score clears Acts; coins are shop-only.
    /// </summary>
    public class BetweenActShopUI : MonoBehaviour
    {
        public static BetweenActShopUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int OfferCount = 3;
        private const int RerollCost = 10;

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI coinsText;
        private TextMeshProUGUI hintText;
        private readonly List<OfferSlot> slots = new();
        private readonly List<UpgradeOffer> currentOffers = new();
        private Button continueButton;
        private Button rerollButton;
        private float savedTimeScale = 1f;

        private struct UpgradeOffer
        {
            public ColonyActManager.ShopUpgradeId Id;
            public string Title;
            public string Description;
            public int Cost;
        }

        private struct OfferSlot
        {
            public GameObject Root;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI Desc;
            public Button Buy;
            public UpgradeOffer Offer;
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
            RebuildUi();
            IsOpen = true;
            savedTimeScale = Time.timeScale;
            if (Time.timeScale > 0.01f)
                Time.timeScale = 0f;

            RefreshOffers(forceNew: true);
            UpdateHeader();
            root.SetActive(true);
            Debug.Log("[BetweenActShop] Opened Terra-Coin upgrade depot.");
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
            coinsText = null;
            hintText = null;
            continueButton = null;
            rerollButton = null;
            EnsureUi();
        }

        private void ContinueToNextAct()
        {
            Hide();
            Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
            ColonyActManager.Instance?.CompleteBetweenActShopAndAdvance();
        }

        private void UpdateHeader()
        {
            int coins = ColonyActManager.Instance != null ? ColonyActManager.Instance.TerraCoins : 0;
            int cleared = ColonyActManager.Instance != null ? ColonyActManager.Instance.CurrentAct : 0;
            int next = cleared + 1;
            if (titleText != null)
                titleText.text = $"UPGRADE DEPOT\nAct {cleared} cleared → preparing Act {next}";
            if (coinsText != null)
                coinsText.text = $"Terra-Coins: {coins}";
            if (hintText != null)
                hintText.text = "Spend Terra-Coins on run upgrades. Coins carry between Acts. Solar seats when you continue.";
        }

        private void RefreshOffers(bool forceNew)
        {
            if (forceNew || currentOffers.Count == 0)
                BuildOfferList();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (i >= currentOffers.Count)
                {
                    slot.Root.SetActive(false);
                    slots[i] = slot;
                    continue;
                }

                UpgradeOffer offer = currentOffers[i];
                slot.Offer = offer;
                slot.Root.SetActive(true);
                if (slot.Title != null) slot.Title.text = offer.Title;
                if (slot.Price != null) slot.Price.text = $"{offer.Cost} Terra-Coins";
                if (slot.Desc != null) slot.Desc.text = offer.Description;

                int captured = i;
                slot.Buy.onClick.RemoveAllListeners();
                slot.Buy.onClick.AddListener(() => TryBuy(captured));
                slots[i] = slot;
                RefreshBuyInteractable(i);
            }

            if (rerollButton != null)
            {
                int coins = ColonyActManager.Instance != null ? ColonyActManager.Instance.TerraCoins : 0;
                rerollButton.interactable = coins >= RerollCost;
            }
        }

        private void RefreshBuyInteractable(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            var slot = slots[index];
            if (slot.Buy == null) return;
            int coins = ColonyActManager.Instance != null ? ColonyActManager.Instance.TerraCoins : 0;
            slot.Buy.interactable = coins >= slot.Offer.Cost;
        }

        private void TryBuy(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            var slot = slots[index];
            var acts = ColonyActManager.Instance;
            if (acts == null) return;

            if (!acts.TrySpendTerraCoins(slot.Offer.Cost))
            {
                ExplorationManager.NotifyExplorationFailed(
                    $"Need {slot.Offer.Cost} Terra-Coins (have {acts.TerraCoins}).");
                return;
            }

            acts.PurchaseUpgrade(slot.Offer.Id);
            currentOffers.RemoveAt(index);
            // Keep three slots visually — refill one offer if catalog remains.
            if (currentOffers.Count < OfferCount)
            {
                var refill = PickRandomUpgrade(ExcludeCurrentIds());
                if (refill.HasValue) currentOffers.Add(refill.Value);
            }

            RefreshOffers(forceNew: false);
            UpdateHeader();
        }

        private void TryReroll()
        {
            var acts = ColonyActManager.Instance;
            if (acts == null) return;
            if (!acts.TrySpendTerraCoins(RerollCost))
            {
                ExplorationManager.NotifyExplorationFailed($"Reroll costs {RerollCost} Terra-Coins.");
                return;
            }
            RefreshOffers(forceNew: true);
            UpdateHeader();
        }

        private HashSet<ColonyActManager.ShopUpgradeId> ExcludeCurrentIds()
        {
            var set = new HashSet<ColonyActManager.ShopUpgradeId>();
            foreach (var o in currentOffers) set.Add(o.Id);
            return set;
        }

        private void BuildOfferList()
        {
            currentOffers.Clear();
            var exclude = new HashSet<ColonyActManager.ShopUpgradeId>();
            for (int i = 0; i < OfferCount; i++)
            {
                var pick = PickRandomUpgrade(exclude);
                if (!pick.HasValue) break;
                currentOffers.Add(pick.Value);
                exclude.Add(pick.Value.Id);
            }
        }

        private static UpgradeOffer? PickRandomUpgrade(HashSet<ColonyActManager.ShopUpgradeId> exclude)
        {
            var catalog = BuildCatalog();
            var pool = new List<UpgradeOffer>();
            foreach (var u in catalog)
            {
                if (exclude != null && exclude.Contains(u.Id)) continue;
                pool.Add(u);
            }
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        private static List<UpgradeOffer> BuildCatalog()
        {
            return new List<UpgradeOffer>
            {
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.ExtraWeeks,
                    Title = "+2 Weeks",
                    Description = "Next Act starts with 2 extra weeks.",
                    Cost = 20
                },
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.ScorePercent,
                    Title = "+10% Score",
                    Description = "All placement scores this run gain +10% (stacks).",
                    Cost = 25
                },
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.GeologyBonus,
                    Title = "Geology Expert",
                    Description = "+5 score when placing on matching deposits / features.",
                    Cost = 20
                },
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.AdjacencyBump,
                    Title = "Tight Colony",
                    Description = "+1 adjacency score per neighboring tile (stacks).",
                    Cost = 25
                },
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.PowerScoreBoost,
                    Title = "Power Prestige",
                    Description = "Power tiles grant +3 extra placement score (stacks).",
                    Cost = 18
                },
                new UpgradeOffer
                {
                    Id = ColonyActManager.ShopUpgradeId.SeatClimateCard,
                    Title = "Climate Pack",
                    Description = "Queue climate tile offers into your hand path.",
                    Cost = 22
                }
            };
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
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.02f, 0.04f, 0.08f, 0.82f);
            dimImg.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.22f, 0.28f);
            panelRt.anchorMax = new Vector2(0.78f, 0.72f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.98f);

            titleText = CreateText(panel.transform, "Title", 22f, FontStyles.Bold,
                new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.98f));
            titleText.alignment = TextAlignmentOptions.Center;
            coinsText = CreateText(panel.transform, "Coins", 18f, FontStyles.Bold,
                new Vector2(0.04f, 0.76f), new Vector2(0.50f, 0.84f));
            coinsText.color = new Color(1f, 0.88f, 0.35f);
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
                slotGo.AddComponent<Image>().color = new Color(0.16f, 0.22f, 0.30f, 1f);

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

            continueButton = CreateBottomButton(panel.transform, "Continue", "CONTINUE →",
                new Vector2(0.55f, 0.04f), new Vector2(0.96f, 0.16f),
                new Color(0.15f, 0.55f, 0.75f, 1f), ContinueToNextAct);
            rerollButton = CreateBottomButton(panel.transform, "Reroll", $"REROLL ({RerollCost})",
                new Vector2(0.04f, 0.04f), new Vector2(0.45f, 0.16f),
                new Color(0.45f, 0.35f, 0.15f, 1f), TryReroll);
        }

        private static Button CreateBottomButton(Transform parent, string name, string label,
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, float size, FontStyles style,
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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
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
