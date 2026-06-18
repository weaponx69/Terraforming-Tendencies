using System;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public static class BlueprintDraftManager
    {
        private static HashSet<string> unlockedBuildings = new() { "Command Post", "Supply Hut" };

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

            GatherSpeedMultiplier = 1.0f;
            PowerGenMultiplier = 1.0f;
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
        }

        public static void CompleteDraft(BlueprintCardSO chosenCard)
        {
            if (chosenCard != null)
            {
                chosenCard.Apply();
            }

            // Unpause game
            Time.timeScale = 1f;

            OnDraftCompleted?.Invoke();
        }
    }
}