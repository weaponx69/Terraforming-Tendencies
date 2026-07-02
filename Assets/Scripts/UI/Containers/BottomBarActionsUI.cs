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
    /// Persistent bottom-center action bar that shows ALL unlockable actions from
    /// the BlueprintDraftManager — all buildings that have been unlocked via draft cards,
    /// plus any commands from existing units/buildings.
    ///
    /// Works like ActionsUI: drag pre-placed UIActionButton children into the
    /// "Action Buttons" array in the Inspector. No dynamic button generation.
    ///
    /// Refreshes whenever units/buildings are selected, die, or spawn, and also
    /// on a periodic tick to catch draft completions and construction completions.
    /// </summary>
    public class BottomBarActionsUI : MonoBehaviour
    {
        [Header("Button Wiring")]
        [Tooltip("Drag pre-placed UIActionButton children here (same pattern as ActionsUI).")]
        [SerializeField] private UIActionButton[] actionButtons;

        private bool isBuilt = false;
        private Owner owner = Owner.Player1;

        // Cached template restrictions to avoid searching every time
        private static BuildingRestrictionSO[] cachedTemplateRestrictions;

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
                // Can't directly convert Transform to RectTransform, so we log an error
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
            Debug.Log($"[BottomBarActionsUI] Initialized with {actionButtons.Length} wired action buttons.");
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
        /// Collect ALL unlockable actions and populate the wired buttons.
        /// Same pattern as ActionPanelBase.RefreshButtons but collects from all sources.
        /// </summary>
        public void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;

            var allCommands = CollectAllCommands();

            // Deduplicate by command name (same command from multiple buildings = one button)
            var seen = new HashSet<string>();
            var uniqueCommands = new List<BaseCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && !seen.Contains(cmd.Name))
                {
                    seen.Add(cmd.Name);
                    uniqueCommands.Add(cmd);
                }
            }

            // Populate buttons — same pattern as ActionPanelBase.RefreshButtons
            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < uniqueCommands.Count)
                {
                    var cmd = uniqueCommands[i];
                    actionButtons[i].EnableFor(cmd, null, () =>
                    {
                        Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(cmd));
                    });
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }
        }

        /// <summary>
        /// Collect all unlockable actions:
        /// 1. All buildings unlocked via BlueprintDraftManager (from draft cards)
        /// 2. All commands from existing units/buildings (production, research, etc.)
        /// </summary>
        private List<BaseCommand> CollectAllCommands()
        {
            var commands = new List<BaseCommand>();
            var addedBuildingNames = new HashSet<string>();

            // 1. Add BuildBuildingCommand for ALL buildings unlocked via draft
            var unlockedBuildingNames = BlueprintDraftManager.GetUnlockedBuildingNames();
            foreach (var buildingName in unlockedBuildingNames)
            {
                if (string.IsNullOrEmpty(buildingName)) continue;
                if (addedBuildingNames.Contains(buildingName)) continue;

                var buildingSO = BlueprintDraftManager.GetBuildingSOByName(buildingName);
                if (buildingSO == null) continue;

                // Create a BuildBuildingCommand for this unlocked building
                var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                buildCmd.Name = "Build " + buildingSO.Name;
                buildCmd.Building = buildingSO;
                // Try FindIconForBuilding first (searches assets by name),
                // then fall back to buildingSO.Icon (may be null on cloned instances)
                buildCmd.Icon = FindIconForBuilding(buildingSO) ?? buildingSO.Icon;
                buildCmd.Slot = FindFreeSlot(commands);

                // Assign the ghost prefab for placement preview from the template BuildBuildingCommand
                // asset in Resources. Each command asset already has GhostPrefab correctly set to its
                // own ghost variant prefab. Falls back to the solid Prefab only if no template is found.
                buildCmd.GhostPrefab = FindGhostPrefabForBuilding(buildingSO) ?? buildingSO.Prefab;

                // Copy Restrictions from an existing template command so dynamically-created
                // commands still enforce placement rules (e.g. flat ground, no overlap).
                CopyRestrictionsFromTemplate(buildCmd);

                commands.Add(buildCmd);
                addedBuildingNames.Add(buildingName);
            }

            // 2. Gather commands from all owned units (workers, etc.)
            var allUnits = FindObjectsByType<AbstractCommandable>();
            foreach (var unit in allUnits)
            {
                if (unit == null) continue;
                if (unit.Owner != owner) continue;

                if (unit.AvailableCommands != null)
                {
                    foreach (var cmd in unit.AvailableCommands)
                    {
                        if (cmd == null) continue;
                        if (cmd is BuildBuildingCommand) continue;
                        commands.Add(cmd);
                    }
                }
            }

            // 3. Gather non-building commands from owned buildings (production, research, etc.)
            if (BaseBuilding.ActiveBuildings != null)
            {
                foreach (var building in BaseBuilding.ActiveBuildings)
                {
                    if (building == null) continue;
                    if (building.Owner != owner) continue;

                    if (building.AvailableCommands != null)
                    {
                        foreach (var cmd in building.AvailableCommands)
                        {
                            if (cmd == null) continue;
                            if (cmd is BuildBuildingCommand) continue;
                            commands.Add(cmd);
                        }
                    }
                }
            }

            return commands;
        }

        /// <summary>
        /// Find an icon for a building by looking for existing BuildBuildingCommand assets.
        /// </summary>
        private Sprite FindIconForBuilding(BuildingSO buildingSO)
        {
            if (buildingSO == null || string.IsNullOrEmpty(buildingSO.Name)) return null;

            var allCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && cmd.Building != null && cmd.Building.Name == buildingSO.Name && cmd.Icon != null)
                {
                    return cmd.Icon;
                }
            }

            // Fallback: check runtime building instances' AvailableCommands
            var allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var building in allBuildings)
            {
                if (building == null || building.BuildingSO == null) continue;
                if (building.BuildingSO.Name != buildingSO.Name) continue;
                if (building.AvailableCommands == null) continue;

                foreach (var cmd in building.AvailableCommands)
                {
                    if (cmd is BuildBuildingCommand bbc && bbc.Icon != null)
                    {
                        return bbc.Icon;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find the GhostPrefab from the template BuildBuildingCommand asset for this building.
        /// Each command asset in Resources/Commands already has GhostPrefab correctly assigned
        /// to its own ghost variant prefab. This avoids adding new fields to BuildingSO or
        /// copying ghosts from unrelated templates.
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

        /// <summary>
        /// Copy Restrictions from any pre-existing BuildBuildingCommand asset or scene-building
        /// command that has them set. GhostPrefab is NOT copied here — it's assigned in
        /// CollectAllCommands from the building's own template command asset via FindGhostPrefabForBuilding.
        /// </summary>
        private static void CopyRestrictionsFromTemplate(BuildBuildingCommand target)
        {
            // Look for a template with Restrictions in scene building commands first
            var allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var building in allBuildings)
            {
                if (building == null || building.AvailableCommands == null) continue;
                foreach (var cmd in building.AvailableCommands)
                {
                    if (cmd is BuildBuildingCommand bbc && bbc.Restrictions != null && bbc.Restrictions.Length > 0)
                    {
                        CopyRestrictions(target, bbc);
                        return;
                    }
                }
            }

            // Fallback: find any BuildBuildingCommand asset in Resources with Restrictions
            var allCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd.Restrictions != null && cmd.Restrictions.Length > 0)
                {
                    CopyRestrictions(target, cmd);
                    return;
                }
            }
        }

        /// <summary>
        /// Copy Restrictions array from a template command to a target command via reflection.
        /// </summary>
        private static void CopyRestrictions(BuildBuildingCommand target, BuildBuildingCommand template)
        {
            if (template.Restrictions != null && template.Restrictions.Length > 0)
            {
                var restrictionsField = typeof(BaseCommand).GetField("<Restrictions>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                restrictionsField?.SetValue(target, template.Restrictions);
            }
        }

        /// <summary>
        /// Set RequiresClickToActivate on a command via reflection.
        /// </summary>
        private static void SetRequiresClickToActivate(BaseCommand cmd, bool value)
        {
            var field = typeof(BaseCommand).GetField("<RequiresClickToActivate>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(cmd, value);
        }

        /// <summary>
        /// Find the next available slot index for a command.
        /// </summary>
        private int FindFreeSlot(List<BaseCommand> commands)
        {
            var usedSlots = new HashSet<int>();
            foreach (var cmd in commands)
            {
                if (cmd != null) usedSlots.Add(cmd.Slot);
            }
            for (int i = 0; i < 8; i++)
            {
                if (!usedSlots.Contains(i)) return i;
            }
            return -1;
        }
    }
}
