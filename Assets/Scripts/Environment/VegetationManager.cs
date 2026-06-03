using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class VegetationManager : MonoBehaviour
    {
        [Header("Settings")]
        public GameObject[] plantPrefabs;
        public float spawnInterval = 10f;
        public int maxPlantsPerZone = 15;
        public LayerMask groundLayer;

        private Dictionary<LifeSupportNode, List<GameObject>> zonePlants = new Dictionary<LifeSupportNode, List<GameObject>>();

        private void Start()
        {
            if (plantPrefabs == null || plantPrefabs.Length == 0)
            {
                Debug.LogWarning("[VegetationManager] No plant prefabs assigned.");
                return;
            }

            StartCoroutine(GrowthLoop());
        }

        private IEnumerator GrowthLoop()
        {
            while (true)
            {
                // Get the displayed owner (usually Player1)
                Owner owner = GameOverManager.MonitoredOwner;
                float oxygen = 0f;
                if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(owner, out float val))
                {
                    oxygen = val;
                }

                // If oxygen is 0, don't grow anything yet
                if (oxygen <= 0)
                {
                    yield return new WaitForSeconds(spawnInterval);
                    continue;
                }

                // Scale interval by oxygen (faster spawning as oxygen increases)
                // At 1% oxygen, interval is spawnInterval. At 100%, it is spawnInterval / 10.
                float currentInterval = spawnInterval / Mathf.Clamp(oxygen, 0.1f, 10f);
                yield return new WaitForSeconds(Mathf.Clamp(currentInterval, 1f, spawnInterval));

                LifeSupportNode[] nodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
                foreach (var node in nodes)
                {
                    if (!zonePlants.ContainsKey(node))
                    {
                        zonePlants[node] = new List<GameObject>();
                    }

                    // Clean up nulls
                    zonePlants[node].RemoveAll(p => p == null);

                    // Max plants per zone also scales with oxygen
                    int currentMax = Mathf.RoundToInt(maxPlantsPerZone * (oxygen / 100f));
                    currentMax = Mathf.Clamp(currentMax, 1, maxPlantsPerZone);

                    if (zonePlants[node].Count < currentMax)
                    {
                        TrySpawnPlant(node);
                    }
                }
            }
        }

        private void TrySpawnPlant(LifeSupportNode node)
        {
            // Pick a random point in the radius
            Vector2 randomCircle = Random.insideUnitCircle * node.Radius;
            Vector3 spawnPos = node.transform.position + new Vector3(randomCircle.x, 10f, randomCircle.y);

            // Raycast down to find ground
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                // Ensure it's not too close to other plants in this zone
                bool tooClose = false;
                foreach (var p in zonePlants[node])
                {
                    if (Vector3.Distance(hit.point, p.transform.position) < 2f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    GameObject prefab = plantPrefabs[Random.Range(0, plantPrefabs.Length)];
                    GameObject plant = Instantiate(prefab, hit.point, Quaternion.Euler(0, Random.Range(0, 360), 0));
                    plant.transform.SetParent(transform);
                    
                    // Add the growth component
                    plant.AddComponent<GrowingVegetation>();
                    
                    zonePlants[node].Add(plant);
                }
            }
        }
    }
}
