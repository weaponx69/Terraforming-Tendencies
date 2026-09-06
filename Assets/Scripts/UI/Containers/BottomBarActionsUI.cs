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
        [Tooltip("Extra gap above the Bottom Bar HUD so cards do not cover selection info.")]
        [SerializeField] private float gapAboveBottomBar = 12f;
        [Tooltip("Fallback Bottom Bar height when the HUD rect cannot be found.")]
        [SerializeField] private float fallbackBottomBarHeight = 300f;
        [Tooltip("Distance from the left edge of the screen to the left edge of the hand.")]
        [SerializeField] private float leftMargin = 16f;
        [Tooltip("Visible hand capacity — keep in sync with CardDeckController.handSize.")]
        [SerializeField] private int visibleHandSlots = 5;

        private float bottomMargin = 16f;

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
        /// Resize hand slots to a taller playing-card aspect and dock them
        /// <b>above</b> the Bottom Bar so the middle selection-info panel stays visible.
        /// </summary>
        private void ApplyPlayingCardLayout()
        {
            float width = Mathf.Max(48f, cardSize.x);
            float height = Mathf.Max(width * 1.25f, cardSize.y);
            bottomMargin = ResolveBottomClearance();

            // Cards live on this object; chrome/container may be the parent.
            var cardsRt = transform as RectTransform;
            var containerRt = transform.parent as RectTransform;
            if (containerRt == null) containerRt = cardsRt;
            if (containerRt == null) return;

            // Bottom-left dock, raised above the Bottom Bar HUD band.
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
        /// Place the hand just above the classic Bottom Bar (minimap / selection info / actions).
        /// </summary>
        private float ResolveBottomClearance()
        {
            RectTransform bottomBar = FindBottomBarRect();
            if (bottomBar == null)
                return fallbackBottomBarHeight + gapAboveBottomBar;

            // Prefer layout height; fall back to rect height.
            float h = bottomBar.rect.height;
            if (h < 8f) h = bottomBar.sizeDelta.y;
            if (h < 8f) h = fallbackBottomBarHeight;
            return h + gapAboveBottomBar;
        }

        private RectTransform FindBottomBarRect()
        {
            Transform t = transform;
            for (int i = 0; i < 8 && t != null; i++)
            {
                if (t.name == "Bottom Bar")
                    return t as RectTransform;
                t = t.parent;
            }

            var go = GameObject.Find("Bottom Bar");
            return go != null ? go.transform as RectTransform : null;
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

            // Keep the hand parked above the Bottom Bar (selection info lives in that band).
            bottomMargin = ResolveBottomClearance();
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

            // Gather unlocked building commands from GlobalCommander as fallback
            var globalCmdr = Object.FindAnyObjectByType<GlobalCommander>();
            BaseCommand[] globalCommands = globalCmdr?.AvailableCommands;

            int maxButtons = Mathf.Min(actionButtons.Length, Mathf.Max(1, visibleHandSlots));

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < maxButtons && i < hand.Count && hand[i] != null)
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
                    // No card in this hand slot — populate with a GlobalCommander
                    // unlocked building command that isn't already shown by a hand card.
                    actionButtons[i].Disable();
                }
            }

            // ── Fallback: fill any empty slots with unlocked building commands ──
            if (globalCommands != null)
            {
                // Collect building names already shown by hand cards (UnlockBuildingCardSO)
                var alreadyShown = new HashSet<string>();
                // Track which button indices are already populated
                var filledSlots = new HashSet<int>();
                for (int h = 0; h < hand.Count && h < maxButtons; h++)
                {
                    filledSlots.Add(h);
                    if (hand[h] is UnlockBuildingCardSO uc && uc.buildingToUnlock != null)
                        alreadyShown.Add(uc.buildingToUnlock.Name);
                }

                // Find an empty button for each GlobalCommander building that can actually be built
                foreach (var cmd in globalCommands)
                {
                    if (cmd is BuildBuildingCommand bbc && bbc.Building != null
                        && !alreadyShown.Contains(bbc.Building.Name)
                        && ReservedSiteBuildUtility.CanBuildAtReservedSite(
                            bbc.Building, owner, out _, requireUnlocked: true))
                    {
                        // Find the first button slot that is NOT already filled
                        int slot = 0;
                        while (slot < actionButtons.Length && filledSlots.Contains(slot))
                        {
                            slot++;
                        }
                        if (slot >= actionButtons.Length) break;

                        var fbBbc = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        fbBbc.Name = cmd.Name;
                        fbBbc.Icon = cmd.Icon;
                        fbBbc.Slot = slot;
                        fbBbc.Building = bbc.Building;
                        fbBbc.GhostPrefab = bbc.GhostPrefab ?? bbc.Building?.Prefab;

                        string fallbackGoal = UnlockBuildingCardSO.ClassifyBuildingGoal(bbc.Building);
                        if (!TerraformingGoalColors.IsSectorCompletionGoal(fallbackGoal))
                        {
                            fallbackGoal = null;
                        }

                        actionButtons[slot].EnableFor(fbBbc, null, () =>
                        {
                            BeginBuildingSelection(bbc.Building, cardIndex: -1);
                        }, fallbackGoal);

                        filledSlots.Add(slot);
                        alreadyShown.Add(bbc.Building.Name);
                    }
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
