using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Manages the pool of possible exploration bonus rewards.
    /// Provides weighted random selection for generating discoveries when exploring sectors.
    /// Auto-spawns as a singleton.
    /// </summary>
    public class ExplorationNodeDatabase : MonoBehaviour
    {
        public static ExplorationNodeDatabase Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<ExplorationNodeDatabase>() != null) return;
            var go = new GameObject("ExplorationNodeDatabase (auto)");
            go.AddComponent<ExplorationNodeDatabase>();
            DontDestroyOnLoad(go);
        }

        private List<ExplorationNodeSO> allNodes;

        private void Awake()
        {
            Instance = this;
            InitializeNodePool();
        }

        private void InitializeNodePool()
        {
            allNodes = new List<ExplorationNodeSO>();

            // Climate bonuses (formerly direct-play cards)
            allNodes.Add(CreateNode("Thermal Surge", "Superheated gases vent into the atmosphere.", "A geothermal vent spews superheated gases...", ExplorationNodeSO.NodeRewardType.Temperature, 8f, 1, 1.0f));
            allNodes.Add(CreateNode("Atmospheric Compression", "Pressurized gas pockets burst, enriching the thin air.", "Pressurized gas pockets burst open...", ExplorationNodeSO.NodeRewardType.Atmosphere, 0.12f, 1, 1.0f));
            allNodes.Add(CreateNode("CO\u2082 Comet Trail", "Traces of a comet's tail settle into the upper atmosphere.", "Traces of a comet's tail drift down...", ExplorationNodeSO.NodeRewardType.Atmosphere, 0.15f, 1, 0.8f));
            allNodes.Add(CreateNode("Subsurface Water Surge", "Deep aquifers rupture, releasing ancient water reservoirs.", "Deep aquifers rupture, releasing water...", ExplorationNodeSO.NodeRewardType.Water, 6f, 1, 1.0f));
            allNodes.Add(CreateNode("Cometary Ice", "A small icy body's remnants scatter across the surface.", "Frozen comet fragments melt on the surface...", ExplorationNodeSO.NodeRewardType.Water, 8f, 1, 0.8f));

            // Resource bonuses
            allNodes.Add(CreateNode("Rich Mineral Vein", "A dense vein of rare minerals lies exposed.", "The scanner picks up a dense mineral signature...", ExplorationNodeSO.NodeRewardType.Materials, 400f, 1, 1.5f));
            allNodes.Add(CreateNode("Bio-Matter Cache", "Frozen organic compounds thaw in the sunlight.", "Frozen organic matter begins to thaw...", ExplorationNodeSO.NodeRewardType.Biomass, 100f, 1, 1.2f));
            allNodes.Add(CreateNode("Abandoned Drone", "A dormant mining drone, still functional, waits for a pilot.", "A dormant mining drone sits in the dust...", ExplorationNodeSO.NodeRewardType.SpawnMiningDrone, 0f, 1, 1.0f));
        }

        private ExplorationNodeSO CreateNode(string name, string desc, string flavor, ExplorationNodeSO.NodeRewardType type, float amount, int count, float weight)
        {
            var node = ScriptableObject.CreateInstance<ExplorationNodeSO>();
            node.nodeName = name;
            node.description = desc;
            node.flavorText = flavor;
            node.rewardType = type;
            node.rewardAmount = amount;
            node.rewardCount = count;
            node.weight = weight;
            return node;
        }

        /// <summary>
        /// Get a weighted random selection of exploration nodes.
        /// </summary>
        public ExplorationNodeSO[] GetRandomNodes(int count)
        {
            if (allNodes == null || allNodes.Count == 0)
            {
                InitializeNodePool();
                if (allNodes == null || allNodes.Count == 0) return new ExplorationNodeSO[0];
            }

            // Calculate total weight
            float totalWeight = 0f;
            foreach (var node in allNodes)
                totalWeight += node.weight;

            var selected = new List<ExplorationNodeSO>();
            var available = new List<ExplorationNodeSO>(allNodes);

            for (int i = 0; i < count && available.Count > 0; i++)
            {
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                int pickIndex = 0;

                for (int j = 0; j < available.Count; j++)
                {
                    cumulative += available[j].weight;
                    if (roll <= cumulative)
                    {
                        pickIndex = j;
                        break;
                    }
                }

                selected.Add(available[pickIndex]);
                totalWeight -= available[pickIndex].weight;
                available.RemoveAt(pickIndex);
            }

            return selected.ToArray();
        }
    }
}