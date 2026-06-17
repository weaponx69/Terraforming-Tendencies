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

                bool isPowered = totalGeneration >= totalUpkeep;
                
                foreach(var gridNode in currentGrid)
                {
                    gridNode.IsGridPowered = isPowered;
                }

                powerGrids.Add(currentGrid);
            }
        }
    }
}
