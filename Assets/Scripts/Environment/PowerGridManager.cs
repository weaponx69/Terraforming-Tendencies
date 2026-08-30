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
                        current.Building.BuildingSO?.BuildingConfig != null &&
                        current.Building.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        float effectiveGen = current.Building.BuildingSO.BuildingConfig.PowerGeneration
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
                        gridNode.Building.BuildingSO?.BuildingConfig != null &&
                        gridNode.Building.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        upkeep = gridNode.Building.BuildingSO.BuildingConfig.PowerUpkeep;
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
            if (node?.Building?.BuildingSO?.BuildingConfig == null) return 1;

            var config = node.Building.BuildingSO.BuildingConfig;
            if (config.PowerGeneration > 0) return 0;

            bool isCp = node.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
            return isCp ? 2 : 1;
        }
    }
}
