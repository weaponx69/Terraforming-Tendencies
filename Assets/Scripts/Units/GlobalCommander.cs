using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class GlobalCommander : AbstractCommandable
    {
        private BaseCommand[] augmentedCommands;

        protected override void Start()
        {
            base.Start();
            // The global commander is always owned by Player 1
            Owner = Owner.Player1;
            gameObject.name = "Universal Command Center";
            
            // Set stats to make it invulnerable and fully operational under any condition
            MaxHealth = 99999;
            CurrentHealth = 99999;

            EnsureSelectionCollider();
            
            BuildAugmentedCommands();
        }

        private void EnsureSelectionCollider()
        {
            // The UCC is an invisible stand-in selected via empty-ground clicks in PlayerInput.
            // A solid collider here sits under air drones near the starting Command Post and
            // steals selection raycasts, making trained mining drones appear "unselectable".
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }

        public override BaseCommand[] AvailableCommands
        {
            get { return augmentedCommands ?? base.AvailableCommands; }
        }

        /// <summary>
        /// Build the same augmented commands that a Command Post would have,
        /// showing all unlocked buildings in the bottom action bar.
        /// </summary>
        public void BuildAugmentedCommands()
        {
            var unlockedBuildingNames = BlueprintDraftManager.GetUnlockedBuildingNames();
            if (unlockedBuildingNames.Count == 0)
            {
                augmentedCommands = System.Array.Empty<BaseCommand>();
                return;
            }

            var list = new List<BaseCommand>();
            foreach (var buildingName in unlockedBuildingNames)
            {
                var buildingSO = BlueprintDraftManager.GetBuildingSOByName(buildingName);
                if (buildingSO == null) continue;

                var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                buildCmd.Name = "Build " + buildingSO.Name;
                buildCmd.Building = buildingSO;
                buildCmd.Icon = buildingSO.Icon ?? FindIconForBuilding(buildingSO);
                buildCmd.Slot = FindFreeSlot(list);
                list.Add(buildCmd);
            }

            // Copy restrictions from an existing BuildBuildingCommand template
            var templateCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var template in templateCommands)
            {
                if (template != null && template.Restrictions != null && template.Restrictions.Length > 0)
                {
                    var restrictionsField = typeof(BaseCommand).GetField("<Restrictions>k__BackingField",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (var cmd in list)
                    {
                        restrictionsField?.SetValue(cmd, template.Restrictions);
                    }
                    break;
                }
            }

            augmentedCommands = list.ToArray();
        }

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

        private Sprite FindIconForBuilding(BuildingSO buildingSO)
        {
            var allCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && cmd.Building == buildingSO && cmd.Icon != null)
                {
                    return cmd.Icon;
                }
            }
            return null;
        }
    }
}
