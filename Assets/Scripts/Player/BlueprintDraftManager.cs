using System;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public static class BlueprintDraftManager
    {
        private static HashSet<string> unlockedBuildings = new() { "Command Post", "Supply Hut" };
        private static Dictionary<string, BuildingSO> knownBuildings = new();

        // Passive buff multipliers
        public static float GatherSpeedMultiplier { get; set; } = 1.0f;
        public static float PowerGenMultiplier { get; set; } = 1.0f;

        public static event Action OnDraftCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Reset()
        {
            unlockedBuildings.Clear();
            unlockedBuildings.Add("Command Post");
            unlockedBuildings.Add("Supply Hut");

            knownBuildings.Clear();

            GatherSpeedMultiplier = 1.0f;
            PowerGenMultiplier = 1.0f;
        }

        public static void RegisterBuildingSO(BuildingSO building)
        {
            if (building == null) return;
            knownBuildings[building.Name] = building;

            if (unlockedBuildings.Contains(building.Name) && building.BuildingConfig != null && building.BuildingConfig.PowerUpkeep > 0)
            {
                if (!unlockedBuildings.Contains("Solar Panel"))
                {
                    unlockedBuildings.Add("Solar Panel");
                }
            }
        }

        public static BuildingSO GetBuildingSOByName(string name)
        {
            if (knownBuildings.TryGetValue(name, out var b)) return b;
            return null;
        }

        public static HashSet<string> GetUnlockedBuildingNames()
        {
            return new HashSet<string>(unlockedBuildings);
        }

        public static bool IsBuildingUnlocked(BuildingSO building)
        {
            if (building == null) return true;
            
            // Allow default essential starting buildings
            if (building.Name.Contains("Command") || building.Name.Contains("Supply Hut"))
            {
                return true;
            }

            return unlockedBuildings.Contains(building.Name);
        }

        public static void UnlockBuilding(string name)
        {
            unlockedBuildings.Add(name);

            BuildingSO building = GetBuildingSOByName(name);
            if (building != null && building.BuildingConfig != null && building.BuildingConfig.PowerUpkeep > 0)
            {
                bool hasPowerGenerator = false;
                foreach (var bldName in unlockedBuildings)
                {
                    var bld = GetBuildingSOByName(bldName);
                    if (bld != null && bld.BuildingConfig != null && bld.BuildingConfig.PowerGeneration > 0)
                    {
                        hasPowerGenerator = true;
                        break;
                    }
                }

                if (!hasPowerGenerator)
                {
                    unlockedBuildings.Add("Solar Panel");
                }
            }
        }

        public static BlueprintCardSO LastDraftedCard { get; private set; }

        public static void CompleteDraft(BlueprintCardSO chosenCard)
        {
            if (chosenCard != null)
            {
                LastDraftedCard = chosenCard;
                chosenCard.Apply();
            }

            // Unpause game
            Time.timeScale = 1f;

            OnDraftCompleted?.Invoke();
        }
    }
}