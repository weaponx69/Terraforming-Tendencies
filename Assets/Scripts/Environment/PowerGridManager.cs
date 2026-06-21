using System.Collections.Generic;
using UnityEngine;

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
                            totalGeneration += current.Building.BuildingSO.BuildingConfig.PowerGeneration;
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

                powerGrids.Add(currentGrid);
            }
        }
    }
}
