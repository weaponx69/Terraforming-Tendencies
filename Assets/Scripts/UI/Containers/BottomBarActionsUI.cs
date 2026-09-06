using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-left action bar that shows the player's card hand.
    /// Building cards open site selection so the player picks which solar cluster to use.
    /// </summary>
    public class BottomBarActionsUI : MonoBehaviour
    {
        [Header("Button Wiring")]
        [Tooltip("Drag pre-placed UIActionButton children here (same pattern as ActionsUI).")]
        [SerializeField] private UIActionButton[] actionButtons;

        [Header("Card Layout")]
        [Tooltip("Playing-card style size for a 5-card hand (width x height).")]
        [SerializeField] private Vector2 cardSize = new Vector2(158f, 220f);
        [SerializeField] private float cardSpacing = 14f;
        [Tooltip("Distance from the bottom edge of the screen to the bottom of the hand.")]
        [SerializeField] private float bottomMargin = 16f;
        [Tooltip("Distance from the left edge of the screen to the left edge of the hand.")]
        [SerializeField] private float leftMargin = 16f;
        [SerializeField] private int visibleHandSlots = 5;

        private bool isBuilt = false;
        private Owner owner = Owner.Player1;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleRefresh;
            CardDeckController.OnHandChanged += RefreshBar;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleRefresh;
            CardDeckController.OnHandChanged -= RefreshBar;
        }

        private void Awake()
        {
            // Ensure this GameObject has a RectTransform (not plain Transform) for Canvas layout
            if (GetComponent<RectTransform>() == null)
            {
                Debug.LogError("[BottomBarActionsUI] This GameObject must have a RectTransform, not a plain Transform. UI elements under a Canvas require RectTransform.", this);
                return;
            }

            HideChromeBackground();
            visibleHandSlots = 5;
            ApplyPlayingCardLayout();

            if (actionButtons == null || actionButtons.Length == 0)
            {
                Debug.LogError("[BottomBarActionsUI] No action buttons wired in Inspector! Drag UIActionButton children into the 'Action Buttons' array.", this);
                return;
            }

            isBuilt = true;
            gameObject.SetActive(true);
            RefreshBar();
            Debug.Log($"[BottomBarActionsUI] Initialized with {actionButtons.Length} wired action buttons. Showing up to {visibleHandSlots} hand cards.");
        }

        /// <summary>
        /// Resize hand slots to a taller playing-card aspect and dock them in the
        /// lower-left corner (selection info is shifted right by RuntimeUI).
        /// </summary>
        private void ApplyPlayingCardLayout()
        {
            float width = Mathf.Max(48f, cardSize.x);
            float height = Mathf.Max(width * 1.25f, cardSize.y);

            // Cards live on this object; chrome/container may be the parent.
            var cardsRt = transform as RectTransform;
            var containerRt = transform.parent as RectTransform;
            if (containerRt == null) containerRt = cardsRt;
            if (containerRt == null) return;

            // True bottom-left corner — do not float mid-screen above the old Bottom Bar band.
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, bottomMargin);
            containerRt.SetAsLastSibling();

            if (cardsRt != null && cardsRt != containerRt)
            {
                cardsRt.anchorMin = Vector2.zero;
                cardsRt.anchorMax = Vector2.one;
                cardsRt.offsetMin = Vector2.zero;
                cardsRt.offsetMax = Vector2.zero;
            }

            // Layout group must be on the parent of the card buttons.
            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = cardSpacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(8, 8, 6, 6);

            // Disable a parent HLG that only wraps this bar — it fights sizing.
            if (containerRt != cardsRt)
            {
                var parentHlg = containerRt.GetComponent<HorizontalLayoutGroup>();
                if (parentHlg != null) parentHlg.enabled = false;
            }

            if (actionButtons == null) return;

            foreach (var slot in actionButtons)
            {
                if (slot == null) continue;
                var rt = slot.transform as RectTransform;
                if (rt == null) continue;
                rt.sizeDelta = new Vector2(width, height);

                var le = slot.GetComponent<LayoutElement>();
                if (le == null) le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;
                le.minWidth = width;
                le.minHeight = height;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
                // Empty slots are collapsed in FitLayoutToActiveCards after RefreshBar.

                Transform icon = slot.transform.Find("Icon");
                if (icon is RectTransform iconRt)
                {
                    // Leave room for cost chip (top) and title (bottom).
                    iconRt.anchorMin = new Vector2(0.08f, 0.28f);
                    iconRt.anchorMax = new Vector2(0.92f, 0.70f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                }
            }

            FitLayoutToActiveCards();
        }

        /// <summary>
        /// Collapse disabled slots out of the horizontal layout and size the dock
        /// to the active hand only (left-aligned).
        /// </summary>
        private void FitLayoutToActiveCards()
        {
            var cardsRt = transform as RectTransform;
            var containerRt = transform.parent as RectTransform;
            if (containerRt == null) containerRt = cardsRt;
            if (containerRt == null || actionButtons == null) return;

            float width = Mathf.Max(48f, cardSize.x);
            float height = Mathf.Max(width * 1.25f, cardSize.y);
            var hlg = GetComponent<HorizontalLayoutGroup>();
            int leftPad = hlg != null ? hlg.padding.left : 8;
            int rightPad = hlg != null ? hlg.padding.right : 8;
            int topPad = hlg != null ? hlg.padding.top : 6;
            int bottomPad = hlg != null ? hlg.padding.bottom : 6;

            int activeSlots = 0;
            foreach (var slot in actionButtons)
            {
                if (slot == null) continue;
                bool active = slot.IsActive;
                var le = slot.GetComponent<LayoutElement>();
                if (le == null) le = slot.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = !active;
                if (!active) continue;

                activeSlots++;
                le.preferredWidth = width;
                le.preferredHeight = height;
                le.minWidth = width;
                le.minHeight = height;
            }

            float contentWidth = activeSlots <= 0
                ? leftPad + rightPad
                : activeSlots * width
                  + Mathf.Max(0, activeSlots - 1) * cardSpacing
                  + leftPad + rightPad;
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + topPad + bottomPad);

            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, bottomMargin);

            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRt);
        }

        /// <summary>
        /// Hide panel backgrounds so only card / action buttons remain visible.
        /// </summary>
        private void HideChromeBackground()
        {
            ClearBackgroundImage(GetComponent<Image>());
            Transform t = transform;
            for (int i = 0; i < 4 && t != null; i++)
            {
                ClearBackgroundImage(t.GetComponent<Image>());
                if (t.name == "Bottom Action Bar Container" || t.name == "Bottom Bar")
                {
                    ClearBackgroundImage(t.GetComponent<Image>());
                }
                t = t.parent;
            }
        }

        private static void ClearBackgroundImage(Image image)
        {
            if (image == null) return;
            Color c = image.color;
            c.a = 0f;
            image.color = c;
            image.raycastTarget = false;
            image.enabled = false;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (BuildingSiteSelectionController.IsSelecting) return;
            // Periodic refresh every ~0.5s to catch newly completed buildings
            if (Time.frameCount % 30 == 0)
            {
                RefreshBar();
            }
        }

        private void HandleRefresh(UnitSelectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeselectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingSpawnEvent evt) { RefreshBar(); }
        private void HandleRefresh(UpgradeResearchedEvent evt) { RefreshBar(); }

        /// <summary>
        /// Refresh the bottom bar to show the player's current hand (up to 5 cards).
        /// </summary>
        public void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;
            if (BuildingSiteSelectionController.IsSelecting) return;

            var hand = CardDeckController.Instance?.Hand;
            if (hand == null) return;

            // Hard cap: only the 5-card hand is shown — never GlobalCommander fallback extras.
            int maxButtons = Mathf.Min(actionButtons.Length, Mathf.Max(1, visibleHandSlots), 5);
            int cardsToShow = Mathf.Min(hand.Count, maxButtons);

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < cardsToShow && hand[i] != null)
                {
                    var card = hand[i];
                    int cardIndex = i; // Capture for closure

                    // Create a BuildBuildingCommand for building cards so placement works
                    string sectorGoal = TerraformingGoalColors.GetSectorGoalForCard(card);

                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        buildCmd.Name = unlockCard.buildingToUnlock.Name;
                        buildCmd.Building = unlockCard.buildingToUnlock;
                        buildCmd.Icon = unlockCard.buildingToUnlock.Icon;
                        buildCmd.Slot = i;

                        int playCost = unlockCard.GetMaterialsPlayCost();
                        actionButtons[i].EnableFor(buildCmd, null, () =>
                        {
                            PlayBuildingCard(cardIndex, unlockCard.buildingToUnlock);
                        }, sectorGoal, playCost);
                    }
                    else
                    {
                        // Non-building cards: use PlayCardCommand which applies the card immediately
                        var playCmd = ScriptableObject.CreateInstance<PlayCardCommand>();
                        playCmd.Name = card.cardName;
                        
                        Sprite cardIcon = card.icon;
                        if (cardIcon == null && card is SpawnUnitCardSO spawnCard && spawnCard.unitPrefab != null)
                        {
                            var unit = spawnCard.unitPrefab.GetComponent<AbstractUnit>();
                            if (unit != null)
                            {
                                cardIcon = unit.Icon;
                            }
                        }
                        playCmd.Icon = cardIcon;
                        
                        playCmd.Slot = i;
                        playCmd.HandIndex = cardIndex;
                        playCmd.MaterialsCost = card.GetMaterialsPlayCost();

                        actionButtons[i].EnableFor(playCmd, null, () =>
                        {
                            // The actual play happens in PlayCardCommand.Handle()
                            // The onClick just needs to route through PlayerInput
                            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(playCmd));
                        }, sectorGoal, playCmd.MaterialsCost);
                    }
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }

            FitLayoutToActiveCards();
        }

        private void PlayBuildingCard(int cardIndex, BuildingSO building)
        {
            if (building == null) return;

            if (!ReservedSiteBuildUtility.CanBuildAtReservedSite(
                    building, owner, out string reason, requireUnlocked: false))
            {
                ExplorationManager.NotifyExplorationFailed(reason);
                CardDeckController.Instance?.DiscardUnplayableFromHand();
                return;
            }

            if (BuildingSiteRegistry.IsCommandBuilding(building))
            {
                if (!ReservedSiteBuildUtility.TryBuildAtReservedSite(building, owner, out reason))
                {
                    ExplorationManager.NotifyExplorationFailed(reason);
                    return;
                }

                CardDeckController.Instance.ConsumeCardAfterBuild(cardIndex);
                return;
            }

            BeginBuildingSelection(building, cardIndex);
        }

        private void BeginBuildingSelection(BuildingSO building, int cardIndex = -1)
        {
            if (building == null) return;

            // Cards defer PlayCard until a site is chosen, so the building is not unlocked yet.
            bool requireUnlocked = cardIndex < 0;

            if (!ReservedSiteBuildUtility.CanBuildAtReservedSite(building, owner, out string reason, requireUnlocked))
            {
                ExplorationManager.NotifyExplorationFailed(reason);
                CardDeckController.Instance?.DiscardUnplayableFromHand();
                return;
            }

            BuildingSiteSelectionController.Begin(building, owner, cardIndex, (ok, selectReason) =>
            {
                if (ok)
                {
                    FindAnyObjectByType<RuntimeUI>(FindObjectsInactive.Include)?.HideWarningBanner();
                    return;
                }

                if (!string.IsNullOrEmpty(selectReason))
                {
                    ExplorationManager.NotifyExplorationFailed(selectReason);
                }
            });
        }

        /// <summary>
        /// Find the GhostPrefab from the template BuildBuildingCommand asset for this building.
        /// </summary>
        private GameObject FindGhostPrefabForBuilding(BuildingSO buildingSO)
        {
            if (buildingSO == null || string.IsNullOrEmpty(buildingSO.Name)) return null;

            var allCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && cmd.Building != null && cmd.Building.Name == buildingSO.Name && cmd.GhostPrefab != null)
                {
                    return cmd.GhostPrefab;
                }
            }
            return null;
        }
    }
}
