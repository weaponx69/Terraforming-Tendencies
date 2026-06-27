using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent ability hand displayed at the bottom-center of the screen.
    /// Collects ActiveAbilityCommand instances from all owned, operational, completed buildings
    /// and renders them as clickable cards. Respects cooldown and operational lockout.
    ///
    /// This panel is ALWAYS visible (not selection-driven) — it sits alongside the existing
    /// ActionsUI and does not replace it.
    ///
    /// Wire in Inspector:
    ///   - handPanel        : root GameObject (anchored bottom-center)
    ///   - cardSlotPrefab   : prefab with AbilityCardSlotUI component
    ///   - cardContainer    : HorizontalLayoutGroup that holds the card slots
    ///   - owner            : which owner to collect abilities from (default Player1)
    /// </summary>
    public class AbilityHandUI : MonoBehaviour
    {
        [Header("Hand Layout")]
        [SerializeField] private GameObject handPanel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardSlotPrefab;

        [Header("Settings")]
        [SerializeField] private Owner owner = Owner.Player1;

        private List<ActiveAbilityCommand> currentAbilities = new();
        private List<GameObject> spawnedSlots = new();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleBuildingChanged;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleBuildingChanged;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleBuildingChanged;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleBuildingChanged;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleBuildingChanged;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleBuildingChanged;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (handPanel != null)
                handPanel.SetActive(true);
            RefreshHand();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            // Periodic refresh to catch newly-completed buildings
            // (events handle most cases, but this is a safety net for buildings
            //  that complete construction between frames)
            if (Time.frameCount % 30 == 0)
            {
                RefreshHand();
            }
        }

        private void HandleBuildingChanged(BuildingSpawnEvent evt)
        {
            RefreshHand();
        }

        private void HandleBuildingChanged(BuildingDeathEvent evt)
        {
            RefreshHand();
        }

        private void HandleBuildingChanged(UpgradeResearchedEvent evt)
        {
            RefreshHand();
        }

        /// <summary>
        /// Collect all ActiveAbilityCommands from owned, completed, operational buildings
        /// and rebuild the hand.
        /// </summary>
        public void RefreshHand()
        {
            currentAbilities = CollectAbilities();
            RebuildSlots();
        }

        private List<ActiveAbilityCommand> CollectAbilities()
        {
            var abilities = new List<ActiveAbilityCommand>();

            if (BaseBuilding.ActiveBuildings == null) return abilities;

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null) continue;
                if (building.Owner != owner) continue;
                if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (!building.IsOperating) continue;

                // Gather ActiveAbilityCommand instances from the building's available commands
                if (building.AvailableCommands == null) continue;

                foreach (var cmd in building.AvailableCommands)
                {
                    if (cmd is ActiveAbilityCommand ability)
                    {
                        abilities.Add(ability);
                    }
                }
            }

            return abilities;
        }

        private void RebuildSlots()
        {
            if (cardContainer == null || cardSlotPrefab == null) return;

            // Clear old slots
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot);
            }
            spawnedSlots.Clear();

            // Spawn new slots
            foreach (var ability in currentAbilities)
            {
                GameObject slotGO = Instantiate(cardSlotPrefab, cardContainer);
                spawnedSlots.Add(slotGO);

                if (slotGO.TryGetComponent(out AbilityCardSlotUI slot))
                {
                    slot.Initialize(ability, OnAbilitySelected);
                }
            }
        }

        private void OnAbilitySelected(ActiveAbilityCommand ability)
        {
            if (ability == null || !ability.IsReady) return;

            // Execute the ability via the event bus (same path as clicking in ActionsUI)
            var context = new CommandContext(owner, null, new RaycastHit());
            if (ability.CanHandle(context))
            {
                ability.Handle(context);
            }

            // Refresh to update cooldown display
            RefreshHand();
        }
    }
}
