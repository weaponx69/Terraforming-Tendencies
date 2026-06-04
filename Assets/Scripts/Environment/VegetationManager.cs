using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class VegetationManager : MonoBehaviour
    {
        [Header("Plant Settings")]
        public GameObject[] plantPrefabs;
        public int maxPlantsPerZone = 300;
        public float plantMinSpacing = 0.8f;
        
        [Header("Grass Settings")]
        public GameObject[] grassPrefabs;
        public int maxGrassPerZone = 1500; 
        public float grassMinSpacing = 0.25f;
        public Color grassColor = new Color(0.2f, 0.8f, 0.1f);

        [Header("Optimization")]
        public int itemsPerChunk = 100;
        public float cullingDistance = 60f;
        public int maxSpawnsPerFrame = 50;

        [Header("Growth Loop")]
        public float spawnInterval = 1.5f;
public int spawnAttemptsPerInterval = 40;
        public LayerMask groundLayer;

        [Header("Growth Control")]
        [Range(0.1f, 10f)]
        public float globalGrowthMultiplier = 1.0f;
        
        [Tooltip("If enabled, all plants will follow the Manual Growth Progress slider below.")]
        public bool useManualGrowthControl = false;
        [Range(0f, 1f)]
        public float manualGrowthProgress = 0f;

        private Dictionary<LifeSupportNode, List<GameObject>> zonePlants = new Dictionary<LifeSupportNode, List<GameObject>>();
        private Dictionary<LifeSupportNode, List<GameObject>> zoneGrass = new Dictionary<LifeSupportNode, List<GameObject>>();
        private Dictionary<LifeSupportNode, List<VegetationChunk>> zoneChunks = new Dictionary<LifeSupportNode, List<VegetationChunk>>();

        private void Start()
        {
            if ((plantPrefabs == null || plantPrefabs.Length == 0) && (grassPrefabs == null || grassPrefabs.Length == 0))
            {
                Debug.LogWarning("[VegetationManager] No plant or grass prefabs assigned.");
                return;
            }

            StartCoroutine(GrowthLoop());
        }

        private IEnumerator GrowthLoop()
        {
            while (true)
            {
                Owner owner = GameOverManager.MonitoredOwner;
                float oxygen = 0f;
                if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(owner, out float val))
                {
                    oxygen = val;
                }

                if (oxygen <= 0)
                {
                    yield return new WaitForSeconds(spawnInterval);
                    continue;
                }

                float currentInterval = spawnInterval / Mathf.Clamp(oxygen, 0.1f, 10f);
                yield return new WaitForSeconds(Mathf.Clamp(currentInterval, 0.5f, spawnInterval));

                LifeSupportNode[] nodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
                foreach (var node in nodes)
                {
                    // Clean and ensure dictionaries
                    EnsureNodeData(node);

                    // Scale max counts by oxygen
                    float oxFactor = Mathf.Clamp01(oxygen / 100f);
                    int currentMaxPlants = Mathf.RoundToInt(maxPlantsPerZone * oxFactor);
                    int currentMaxGrass = Mathf.RoundToInt(maxGrassPerZone * oxFactor);

                    // Multiple attempts per interval
                    for (int i = 0; i < spawnAttemptsPerInterval; i++)
                    {
                        if (zonePlants[node].Count < currentMaxPlants)
                            TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]);
                        
                        if (zoneGrass[node].Count < currentMaxGrass)
                            TrySpawnItem(node, grassPrefabs, grassMinSpacing, zoneGrass[node]);
                    }
                }
            }
        }

        private void EnsureNodeData(LifeSupportNode node)
        {
            if (!zonePlants.ContainsKey(node)) zonePlants[node] = new List<GameObject>();
            if (!zoneGrass.ContainsKey(node)) zoneGrass[node] = new List<GameObject>();
            if (!zoneChunks.ContainsKey(node)) zoneChunks[node] = new List<VegetationChunk>();
            
            zonePlants[node].RemoveAll(p => p == null);
            zoneGrass[node].RemoveAll(p => p == null);
            zoneChunks[node].RemoveAll(c => c == null);
        }

        [ContextMenu("Fill All Zones (Zero Growth)")]
        public void FillAllZonesNow()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(FillAllZonesRoutine());
            }
            else
            {
                ExecuteFillNow();
            }
        }

        private IEnumerator FillAllZonesRoutine()
        {
            float oxygen = 100f; 
            Owner owner = GameOverManager.MonitoredOwner;
            if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(owner, out float val))
            {
                oxygen = val;
            }

            LifeSupportNode[] nodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
            int spawnCountThisFrame = 0;

            foreach (var node in nodes)
            {
                EnsureNodeData(node);
                
                int targetPlants = maxPlantsPerZone;
                int targetGrass = maxGrassPerZone;

                // Plants
                for (int i = 0; i < targetPlants * 2; i++)
                {
                    if (zonePlants[node].Count >= targetPlants) break;
                    if (TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]) != null)
                    {
                        spawnCountThisFrame++;
                        if (spawnCountThisFrame >= maxSpawnsPerFrame)
                        {
                            spawnCountThisFrame = 0;
                            yield return null;
                        }
                    }
                }

                // Grass
                for (int i = 0; i < targetGrass * 5; i++)
                {
                    if (zoneGrass[node].Count >= targetGrass) break;
                    if (TrySpawnItem(node, grassPrefabs, grassMinSpacing, zoneGrass[node]) != null)
                    {
                        spawnCountThisFrame++;
                        if (spawnCountThisFrame >= maxSpawnsPerFrame)
                        {
                            spawnCountThisFrame = 0;
                            yield return null;
                        }
                    }
                }
            }
            Debug.Log($"[VegetationManager] Finished filling zones staggered.");
        }

        private void ExecuteFillNow()
        {
            LifeSupportNode[] nodes = FindObjectsByType<LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                EnsureNodeData(node);
                int targetPlants = maxPlantsPerZone;
                int targetGrass = maxGrassPerZone;

                for (int i = 0; i < targetPlants * 2; i++)
                {
                    if (zonePlants[node].Count >= targetPlants) break;
                    TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]);
                }

                for (int i = 0; i < targetGrass * 5; i++)
                {
                    if (zoneGrass[node].Count >= targetGrass) break;
                    TrySpawnItem(node, grassPrefabs, grassMinSpacing, zoneGrass[node]);
                }
            }
            Debug.Log($"[VegetationManager] Filled zones immediately (Editor).");
        }

        [ContextMenu("Clear All Vegetation")]
        public void ClearAllVegetation()
        {
            var plants = GetComponentsInChildren<GrowingVegetation>();
            foreach (var p in plants)
            {
                if (Application.isPlaying) Destroy(p.gameObject);
                else DestroyImmediate(p.gameObject);
            }
            
            // Also destroy chunks
            var chunks = GetComponentsInChildren<VegetationChunk>();
            foreach (var c in chunks)
            {
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            zonePlants.Clear();
            zoneGrass.Clear();
            zoneChunks.Clear();
            Debug.Log("[VegetationManager] Cleared all vegetation.");
        }

        public void SimulateTurns(int turns)
        {
            // Simplified simulation
            for (int i = 0; i < turns; i++) FillAllZonesNow();
        }

        private GameObject TrySpawnItem(LifeSupportNode node, GameObject[] prefabs, float spacing, List<GameObject> collection)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            Vector2 randomCircle = Random.insideUnitCircle * node.Radius;
            Vector3 spawnPos = node.transform.position + new Vector3(randomCircle.x, 50f, randomCircle.y);

            int mask = groundLayer.value | (1 << LayerMask.NameToLayer("TransparentFX"));
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f, mask))
            {
                bool tooClose = false;
                if (spacing > 0.01f)
                {
                    int checkCount = Mathf.Min(collection.Count, 10);
                    for (int i = collection.Count - 1; i >= collection.Count - checkCount; i--)
                    {
                        if (collection[i] != null && Vector3.Distance(hit.point, collection[i].transform.position) < spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }

                if (!tooClose)
                {
                    // Get or create chunk
                    VegetationChunk chunk = GetAvailableChunk(node, hit.point);

                    GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                    GameObject item = Instantiate(prefab, hit.point, Quaternion.Euler(0, Random.Range(0, 360), 0));
                    item.transform.SetParent(chunk.transform);
                    item.layer = 2; // Ignore Raycast
                    
                    var gv = item.AddComponent<GrowingVegetation>();
                    
                    // If this is grass (based on spacing/collection), tint it green
                    if (spacing < 0.5f)
                    {
                        gv.SetDuration(20f); // Grass grows very fast
                        // Use much smaller scale for grass (prefabs are huge)
                        gv.SetTargetScale(new Vector3(0.04f, 0.03f, 0.04f)); 
                        gv.ApplyColorTint(grassColor);
                    }
                    else
                    {
                        gv.SetDuration(45f); // Plants grow at medium speed
                        // Use smaller scale for plants to look like bushes
                        gv.SetTargetScale(new Vector3(0.15f, 0.15f, 0.15f));
                        gv.ApplyColorTint(new Color(0.1f, 0.6f, 0.2f)); 
                    }

                    chunk.AddItem(item);
                    collection.Add(item);
                    return item;
                }
            }
            return null;
        }

        private VegetationChunk GetAvailableChunk(LifeSupportNode node, Vector3 position)
        {
            List<VegetationChunk> chunks = zoneChunks[node];
            
            // Find nearby chunk with space
            foreach (var c in chunks)
            {
                if (c.transform.childCount < itemsPerChunk && Vector3.Distance(c.transform.position, position) < 10f)
                {
                    return c;
                }
            }

            // Create new chunk
            GameObject chunkGO = new GameObject("VegetationChunk_" + node.name + "_" + chunks.Count);
            chunkGO.transform.SetParent(transform);
            chunkGO.transform.position = position;
            
            VegetationChunk chunk = chunkGO.AddComponent<VegetationChunk>();
            chunk.SetVisibleDistance(cullingDistance);
            chunks.Add(chunk);
            return chunk;
        }
    }
}
