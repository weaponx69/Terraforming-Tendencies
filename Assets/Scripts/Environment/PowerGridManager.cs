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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void RegisterNode(PowerNode node)
        {
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
            powerGrids.Clear();
            HashSet<PowerNode> visited = new HashSet<PowerNode>();

            // Track total net power for each owner
            Dictionary<Owner, float> ownerPower = new Dictionary<Owner, float>();
            foreach (Owner owner in System.Enum.GetValues(typeof(Owner)))
            {
                ownerPower[owner] = 0f;
            }

            foreach(var node in allNodes)
            {
                if (node == null || visited.Contains(node)) continue;

                List<PowerNode> currentGrid = new List<PowerNode>();
                Queue<PowerNode> queue = new Queue<PowerNode>();
                
                queue.Enqueue(node);
                visited.Add(node);

                float totalGeneration = 0f;
                float totalUpkeep = 0f;

                while(queue.Count > 0)
                {
                    PowerNode current = queue.Dequeue();
                    currentGrid.Add(current);

                    if (current.Building != null && current.Building.BuildingSO != null && current.Building.BuildingSO.BuildingConfig != null)
                    {
                        if (current.Building.Progress.State == GameDevTV.RTS.Units.BuildingProgress.BuildingState.Completed)
                        {
                            float effectiveGen = current.Building.BuildingSO.BuildingConfig.PowerGeneration * Player.BlueprintDraftManager.PowerGenMultiplier;
                            totalGeneration += effectiveGen;
                            totalUpkeep += current.Building.BuildingSO.BuildingConfig.PowerUpkeep;
                        }
                    }

                    foreach(var neighbor in current.ConnectedNodes)
                    {
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Sort grid nodes: Command Posts first
                var sortedNodes = new List<PowerNode>(currentGrid);
                sortedNodes.Sort((a, b) =>
                {
                    bool aIsCP = a.Building != null && a.Building.BuildingSO != null && a.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    bool bIsCP = b.Building != null && b.Building.BuildingSO != null && b.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    if (aIsCP && !bIsCP) return -1;
                    if (!aIsCP && bIsCP) return 1;
                    return 0;
                });

                float remainingPower = totalGeneration;
                foreach (var gridNode in sortedNodes)
                {
                    float upkeep = 0f;
                    if (gridNode.Building != null && gridNode.Building.BuildingSO != null && gridNode.Building.BuildingSO.BuildingConfig != null)
                    {
                        if (gridNode.Building.Progress.State == GameDevTV.RTS.Units.BuildingProgress.BuildingState.Completed)
                        {
                            upkeep = gridNode.Building.BuildingSO.BuildingConfig.PowerUpkeep;
                        }
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

                // Accumulate the net power to the owner of the first node in the grid
                if (currentGrid.Count > 0 && currentGrid[0].Building != null)
                {
                    Owner gridOwner = currentGrid[0].Building.Owner;
                    ownerPower[gridOwner] += remainingPower;
                }

                powerGrids.Add(currentGrid);
            }

            // Update Supplies with the exact static net power level
            foreach (var kvp in ownerPower)
            {
                Supplies.UpdatePower(kvp.Key, kvp.Value);
            }
        }
    }
}
