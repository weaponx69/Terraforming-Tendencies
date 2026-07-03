using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-center action bar that shows the player's 5-card hand
    /// from the CardDeckController. Each card is a consumable action:
    ///   - Building card → unlock + enter placement mode
    ///   - Unit card → spawn the unit
    ///   - Resource card → grant resources, draw replacement
    ///   - Buff card → apply buff, draw replacement
    ///
    /// When a card is played, it's removed from the hand and a new card is
    /// drawn from the deck. Refreshes on various game events.
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
        /// Refresh the bottom bar to show the player's current 5-card hand.
        /// Each button corresponds to one card in the hand.
        /// </summary>
        public void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;

            var hand = CardDeckController.Instance?.Hand;
            if (hand == null) return;

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
                        // Create a BuildBuildingCommand for this building
                        var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        buildCmd.Name = "Build " + unlockCard.buildingToUnlock.Name;
                        buildCmd.Building = unlockCard.buildingToUnlock;
                        buildCmd.Icon = unlockCard.buildingToUnlock.Icon;
                        buildCmd.GhostPrefab = FindGhostPrefabForBuilding(unlockCard.buildingToUnlock)
                            ?? unlockCard.buildingToUnlock.Prefab;
                        buildCmd.Slot = i;

                        actionButtons[i].EnableFor(buildCmd, null, () =>
                        {
                            // Play the card (unlock the building) then start placement
                            CardDeckController.Instance.PlayCard(cardIndex);
                            // CommandSelectedEvent lets PlayerInput enter placement mode
                            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(buildCmd));
                        });
                    }
                    else
                    {
                        // Non-building cards: use PlayCardCommand which applies the card immediately
                        var playCmd = ScriptableObject.CreateInstance<PlayCardCommand>();
                        playCmd.Name = card.cardName;
                        playCmd.Icon = card.icon;
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
                    actionButtons[i].Disable();
                }
            }
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
