using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-center action bar that shows the player's card hand.
    /// Building cards open site selection so the player picks which solar cluster to use.
    /// </summary>
    public class BottomBarActionsUI : MonoBehaviour
    {
        [Header("Button Wiring")]
        [Tooltip("Drag pre-placed UIActionButton children here (same pattern as ActionsUI).")]
        [SerializeField] private UIActionButton[] actionButtons;

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
        }

        private void Awake()
        {
            // Ensure this GameObject has a RectTransform (not plain Transform) for Canvas layout
            if (GetComponent<RectTransform>() == null)
            {
                Debug.LogError("[BottomBarActionsUI] This GameObject must have a RectTransform, not a plain Transform. UI elements under a Canvas require RectTransform.", this);
                return;
            }

            if (actionButtons == null || actionButtons.Length == 0)
            {
                Debug.LogError("[BottomBarActionsUI] No action buttons wired in Inspector! Drag UIActionButton children into the 'Action Buttons' array.", this);
                return;
            }

            isBuilt = true;
            gameObject.SetActive(true);
            RefreshBar();
            Debug.Log($"[BottomBarActionsUI] Initialized with {actionButtons.Length} wired action buttons. Showing up to 5 hand cards.");
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
        /// Refresh the bottom bar to show the player's current 10-card hand.
        /// Cards from the hand take priority. For any slot where the hand
        /// has no card, we fall through to show unlocked building commands
        /// from the GlobalCommander (e.g., Solar Panel after its card is played).
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

            // Show up to 10 cards (or however many buttons are wired)
            int maxButtons = Mathf.Min(actionButtons.Length, 10);

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < maxButtons && i < hand.Count && hand[i] != null)
                {
                    var card = hand[i];
                    int cardIndex = i; // Capture for closure

                    // Create a BuildBuildingCommand for building cards so placement works
                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        buildCmd.Name = "Build " + unlockCard.buildingToUnlock.Name;
                        buildCmd.Building = unlockCard.buildingToUnlock;
                        buildCmd.Icon = unlockCard.buildingToUnlock.Icon;
                        buildCmd.Slot = i;

                        actionButtons[i].EnableFor(buildCmd, null, () =>
                        {
                            PlayBuildingCard(cardIndex, unlockCard.buildingToUnlock);
                        });
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

                        actionButtons[i].EnableFor(playCmd, null, () =>
                        {
                            // The actual play happens in PlayCardCommand.Handle()
                            // The onClick just needs to route through PlayerInput
                            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(playCmd));
                        });
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

                // Find an empty button for each GlobalCommander building not already in hand
                foreach (var cmd in globalCommands)
                {
                    if (cmd is BuildBuildingCommand bbc && bbc.Building != null
                        && !alreadyShown.Contains(bbc.Building.Name))
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

                        actionButtons[slot].EnableFor(fbBbc, null, () =>
                        {
                            BeginBuildingSelection(bbc.Building, cardIndex: -1);
                        });

                        filledSlots.Add(slot);
                        alreadyShown.Add(bbc.Building.Name);
                    }
                }
            }
        }

        private void PlayBuildingCard(int cardIndex, BuildingSO building)
        {
            if (building == null) return;

            if (!ReservedSiteBuildUtility.CanBuildAtReservedSite(
                    building, owner, out string reason, requireUnlocked: false))
            {
                Debug.LogWarning($"[BottomBarActionsUI] {reason}");
                return;
            }

            if (BuildingSiteRegistry.IsCommandBuilding(building))
            {
                CardDeckController.Instance.PlayCard(cardIndex);

                if (!ReservedSiteBuildUtility.TryBuildAtReservedSite(building, owner, out reason))
                {
                    Debug.LogWarning($"[BottomBarActionsUI] Build failed after playing card: {reason}");
                }
                return;
            }

            BeginBuildingSelection(building, cardIndex);
        }

        private void BeginBuildingSelection(BuildingSO building, int cardIndex = -1)
        {
            if (building == null) return;

            if (!ReservedSiteBuildUtility.CanBuildAtReservedSite(building, owner, out string reason))
            {
                Debug.LogWarning($"[BottomBarActionsUI] {reason}");
                return;
            }

            BuildingSiteSelectionController.Begin(building, owner, cardIndex, (ok, selectReason) =>
            {
                if (!ok)
                {
                    Debug.LogWarning($"[BottomBarActionsUI] {selectReason}");
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
