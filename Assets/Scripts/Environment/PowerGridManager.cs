using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class PowerGridManager : MonoBehaviour
    {
        public static PowerGridManager Instance { get; private set; }

        private static List<PowerNode> allNodes = new List<PowerNode>();
        private static List<List<PowerNode>> powerGrids = new List<List<PowerNode>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("PowerGridManager");
            DontDestroyOnLoad(go);
            go.AddComponent<PowerGridManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static void RegisterNode(PowerNode node)
        {
            if (node == null) return;
            if (!allNodes.Contains(node)) allNodes.Add(node);
            RecalculateGrids();
        }

        public static void UnregisterNode(PowerNode node)
        {
            allNodes.Remove(node);
            RecalculateGrids();
        }

        public static void RecalculateGrids()
        {
            // Drop destroyed nodes so stale entries cannot poison allocation.
            allNodes.RemoveAll(n => n == null);

            powerGrids.Clear();
            HashSet<PowerNode> visited = new HashSet<PowerNode>();

            Dictionary<Owner, float> ownerPower = new Dictionary<Owner, float>();
            foreach (Owner owner in System.Enum.GetValues(typeof(Owner)))
            {
                ownerPower[owner] = 0f;
            }

            foreach (var node in allNodes)
            {
                if (node == null || visited.Contains(node)) continue;

                List<PowerNode> currentGrid = new List<PowerNode>();
                Queue<PowerNode> queue = new Queue<PowerNode>();

                queue.Enqueue(node);
                visited.Add(node);

                float totalGeneration = 0f;

                while (queue.Count > 0)
                {
                    PowerNode current = queue.Dequeue();
                    currentGrid.Add(current);

                    if (current.Building != null &&
                        current.Building.ResolvedBuildingSO?.BuildingConfig != null &&
                        current.Building.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        float effectiveGen = current.Building.ResolvedBuildingSO.BuildingConfig.PowerGeneration
                            * BlueprintDraftManager.PowerGenMultiplier;
                        totalGeneration += effectiveGen;
                    }

                    foreach (var neighbor in current.ConnectedNodes)
                    {
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Allocate generation to consumers first (paired buildings / infrastructure),
                // then Command Posts last. CP starting backup cells must not starve a solar
                // cluster that was just wired through the auto-connect-to-CP path.
                var sortedNodes = new List<PowerNode>(currentGrid);
                sortedNodes.Sort((a, b) => AllocationPriority(a).CompareTo(AllocationPriority(b)));

                float remainingPower = totalGeneration;
                foreach (var gridNode in sortedNodes)
                {
                    float upkeep = 0f;
                    if (gridNode.Building != null &&
                        gridNode.Building.ResolvedBuildingSO?.BuildingConfig != null &&
                        gridNode.Building.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        upkeep = gridNode.Building.ResolvedBuildingSO.BuildingConfig.PowerUpkeep;
                    }

                    // Self-powered nodes (CP backup cells / battery) stay powered without
                    // draining shared generation — otherwise a 20-upkeep CP eats a 25-gen
                    // solar and leaves the Oxygen Processor dark despite being connected.
                    if (gridNode.IsSelfPowered)
                    {
                        gridNode.IsGridPowered = remainingPower >= upkeep;
                        continue;
                    }

                    if (upkeep <= remainingPower)
                    {
                        gridNode.IsGridPowered = true;
                        remainingPower -= upkeep;
                    }
                    else
                    {
                        gridNode.IsGridPowered = false;
                    }
                }

                if (currentGrid.Count > 0 && currentGrid[0].Building != null)
                {
                    Owner gridOwner = currentGrid[0].Building.Owner;
                    ownerPower[gridOwner] += remainingPower;
                }

                powerGrids.Add(currentGrid);
            }

            foreach (var kvp in ownerPower)
            {
                Supplies.UpdatePower(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Lower = earlier allocation. Generators first, then normal consumers, CPs last.
        /// </summary>
        private static int AllocationPriority(PowerNode node)
        {
            if (node?.Building?.ResolvedBuildingSO?.BuildingConfig == null) return 1;

            var config = node.Building.ResolvedBuildingSO.BuildingConfig;
            if (config.PowerGeneration > 0) return 0;

            bool isCp = node.Building.ResolvedBuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            return isCp ? 2 : 1;
        }

        /// <summary>Total PowerGeneration from completed Player buildings.</summary>
        public static float GetBoardPowerGeneration(Owner owner)
        {
            float gen = 0f;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                var cfg = b.ResolvedBuildingSO?.BuildingConfig;
                if (cfg == null) continue;
                if (cfg.PowerGeneration > 0f)
                    gen += cfg.PowerGeneration * BlueprintDraftManager.PowerGenMultiplier;
            }
            return gen;
        }

        /// <summary>Total PowerUpkeep from completed Player buildings.</summary>
        public static float GetBoardPowerUpkeep(Owner owner)
        {
            float upkeep = 0f;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                var cfg = b.ResolvedBuildingSO?.BuildingConfig;
                if (cfg != null && cfg.PowerUpkeep > 0f)
                    upkeep += cfg.PowerUpkeep;
            }
            return upkeep;
        }

        public static float GetBuildingPowerUpkeep(BuildingSO building)
        {
            if (building?.BuildingConfig == null) return 0f;
            return Mathf.Max(0f, building.BuildingConfig.PowerUpkeep);
        }

        public static float GetBuildingPowerGeneration(BuildingSO building)
        {
            if (building?.BuildingConfig == null) return 0f;
            float gen = building.BuildingConfig.PowerGeneration;
            return gen > 0f ? gen * BlueprintDraftManager.PowerGenMultiplier : 0f;
        }

        /// <summary>
        /// Hand may not hold consumer cards whose combined PowerUpkeep exceeds board generation.
        /// Generators (and zero-upkeep tiles) are always allowed.
        /// </summary>
        public static bool CanSeatCardInHandForPower(BlueprintCardSO card, System.Collections.Generic.IList<BlueprintCardSO> hand, Owner owner)
        {
            if (card == null) return false;
            float cardUpkeep = GetCardPowerUpkeep(card);
            if (cardUpkeep <= 0.0001f) return true;

            float gen = GetBoardPowerGeneration(owner);
            float handUpkeep = 0f;
            if (hand != null)
            {
                for (int i = 0; i < hand.Count; i++)
                    handUpkeep += GetCardPowerUpkeep(hand[i]);
            }

            return handUpkeep + cardUpkeep <= gen + 0.001f;
        }

        /// <summary>True if placing this consumer still fits under board generation.</summary>
        public static bool CanPlayBuildingForPower(BuildingSO building, Owner owner)
        {
            float upkeep = GetBuildingPowerUpkeep(building);
            if (upkeep <= 0.0001f) return true;
            float gen = GetBoardPowerGeneration(owner);
            float boardUpkeep = GetBoardPowerUpkeep(owner);
            return boardUpkeep + upkeep <= gen + 0.001f;
        }

        public static float GetCardPowerUpkeep(BlueprintCardSO card)
        {
            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                return GetBuildingPowerUpkeep(unlock.buildingToUnlock);
            return 0f;
        }
    }
}
