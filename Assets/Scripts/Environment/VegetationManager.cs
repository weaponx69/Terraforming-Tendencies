using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

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
        public int itemsPerChunk = 500; // Increased since they are now batches
        public float cullingDistance = 80f;
        public int maxSpawnsPerFrame = 100;

        [Header("Growth Loop")]
        public float spawnInterval = 5.0f;
        public int spawnAttemptsPerInterval = 20;
        public LayerMask groundLayer;

        [Header("Oxygen & Cost Balance")]
        public float oxygenPerGrass = 0.0005f;
        public float oxygenPerPlant = 0.001f;
        public float biomassCostPerGrass = 0.02f;
        public float biomassCostPerPlant = 0.1f;
        [Range(0f, 1f)]
        public float baseDecayChance = 0.002f;
        public float orphanedDecayMultiplier = 10f;
        public float balanceTickRate = 2f;

        [Header("Growth Control")]
        [Range(0.1f, 10f)]
        public float globalGrowthMultiplier = 1.0f;
        
        [Tooltip("If enabled, all plants will follow the Manual Growth Progress slider below.")]
        public bool useManualGrowthControl = false;
        [Range(0f, 1f)]
        public float manualGrowthProgress = 0f;

        private Dictionary<LifeSupportNode, List<GameObject>> zonePlants = new Dictionary<LifeSupportNode, List<GameObject>>();
        private Dictionary<LifeSupportNode, GrassBatch> zoneGrassBatches = new Dictionary<LifeSupportNode, GrassBatch>();
        
        private List<LifeSupportNode> cachedNodes = new List<LifeSupportNode>();
        private float nodeCacheTimer = 0f;
        private float balanceTickTimer = 0f;

        private class GrassInstance
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 TargetScale;
            public float GrowthProgress;
            public float GrowthDuration;
            public int PrefabIndex;
        }

        private class GrassBatch
        {
            public List<GrassInstance> Instances = new List<GrassInstance>();
            public Dictionary<int, List<Matrix4x4[]>> MatrixGroups = new Dictionary<int, List<Matrix4x4[]>>();
            public bool AnyGrowing = true;
            public bool NeedsUpdate = true;
        }

        private void Start()
        {
            if (Instance == null) Instance = this;
            
            if ((plantPrefabs == null || plantPrefabs.Length == 0) && (grassPrefabs == null || grassPrefabs.Length == 0))
            {
                Debug.LogWarning("[VegetationManager] No plant or grass prefabs assigned.");
                return;
            }

            UpdateNodeCache();
            StartCoroutine(GrowthLoop());
        }

        public static VegetationManager Instance { get; private set; }

        private void UpdateNodeCache()
        {
            cachedNodes.Clear();
            cachedNodes.AddRange(LifeSupportNode.ActiveNodes);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            nodeCacheTimer += dt;
            if (nodeCacheTimer >= 5f)
            {
                nodeCacheTimer = 0f;
                UpdateNodeCache();
            }

            balanceTickTimer += dt;
            if (balanceTickTimer >= balanceTickRate)
            {
                balanceTickTimer = 0f;
                ProcessBalanceTick();
            }

            RenderGrassBatches();
        }

        private void ProcessBalanceTick()
        {
            Owner owner = GameOverManager.MonitoredOwner;
            float totalOxygenBoost = 0f;
            float totalBiomassBoost = 0f;

            // 1. Calculate Oxygen, Biomass, and Process Decay for Plants
            var zonesToCleanup = new List<LifeSupportNode>();
            foreach (var kvp in zonePlants)
            {
                LifeSupportNode node = kvp.Key;
                List<GameObject> plants = kvp.Value;
                bool isOrphaned = node == null || (node.TryGetComponent<BaseBuilding>(out var b) && !b.IsOperating);

                totalOxygenBoost += plants.Count * oxygenPerPlant;
                totalBiomassBoost += plants.Count * biomassCostPerPlant; // biomassCostPerPlant now acts as generation

                // Decay plants
                float chance = isOrphaned ? baseDecayChance * orphanedDecayMultiplier : baseDecayChance;
                for (int i = plants.Count - 1; i >= 0; i--)
                {
                    if (plants[i] == null) { plants.RemoveAt(i); continue; }
                    if (Random.value < chance)
                    {
                        Destroy(plants[i]);
                        plants.RemoveAt(i);
                    }
                }
                if (isOrphaned && plants.Count == 0) zonesToCleanup.Add(node);
            }
            foreach (var node in zonesToCleanup) zonePlants.Remove(node);
            zonesToCleanup.Clear();

            // 2. Calculate Oxygen, Biomass, and Process Decay for Grass
            foreach (var kvp in zoneGrassBatches)
            {
                LifeSupportNode node = kvp.Key;
                GrassBatch batch = kvp.Value;
                bool isOrphaned = node == null || (node.TryGetComponent<BaseBuilding>(out var b) && !b.IsOperating);

                totalOxygenBoost += batch.Instances.Count * oxygenPerGrass;
                totalBiomassBoost += batch.Instances.Count * biomassCostPerGrass; // biomassCostPerGrass now acts as generation

                // Decay grass
                float chance = isOrphaned ? baseDecayChance * orphanedDecayMultiplier : baseDecayChance;
                bool changed = false;
                for (int i = batch.Instances.Count - 1; i >= 0; i--)
                {
                    if (Random.value < chance)
                    {
                        batch.Instances.RemoveAt(i);
                        changed = true;
                    }
                }
                if (changed) batch.NeedsUpdate = true;
                if (isOrphaned && batch.Instances.Count == 0) zonesToCleanup.Add(node);
            }
            foreach (var node in zonesToCleanup) zoneGrassBatches.Remove(node);

            // 3. Update Global Oxygen and Biomass
            if (totalOxygenBoost > 0)
            {
                float currentOxygen = Supplies.Oxygen.TryGetValue(owner, out float val) ? val : 0;
                Supplies.UpdateOxygen(owner, currentOxygen + totalOxygenBoost);
            }

            if (totalBiomassBoost > 0)
            {
                float currentBiomass = Supplies.Biomass.TryGetValue(owner, out float b) ? b : 0f;
                Supplies.UpdateBiomass(owner, currentBiomass + totalBiomassBoost);
            }
        }

        private void RenderGrassBatches()
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0) return;

            float dt = Time.deltaTime;
            float multiplier = globalGrowthMultiplier;

            foreach (var kvp in zoneGrassBatches)
            {
                LifeSupportNode node = kvp.Key;
                if (node == null) continue;

                GrassBatch batch = kvp.Value;

                if (batch.AnyGrowing)
                {
                    bool stillGrowing = false;
                    foreach (var inst in batch.Instances)
                    {
                        if (inst.GrowthProgress < 1f)
                        {
                            inst.GrowthProgress += (dt * multiplier) / inst.GrowthDuration;
                            if (inst.GrowthProgress >= 1f) inst.GrowthProgress = 1f;
                            else stillGrowing = true;
                            batch.NeedsUpdate = true;
                        }
                    }
                    batch.AnyGrowing = stillGrowing;
                }

                if (batch.NeedsUpdate)
                {
                    UpdateBatchMatrices(batch);
                    batch.NeedsUpdate = false;
                }

                for (int p = 0; p < grassPrefabs.Length; p++)
                {
                    Mesh mesh = GetMesh(grassPrefabs[p]);
                    Material mat = GetMaterial(grassPrefabs[p]);
                    if (mesh == null || mat == null) continue;

                    if (batch.MatrixGroups.TryGetValue(p, out var groups))
                    {
                        foreach (var group in groups)
                        {
                            if (group.Length > 0)
                                Graphics.DrawMeshInstanced(mesh, 0, mat, group, group.Length);
                        }
                    }
                }
            }
        }

        private void UpdateBatchMatrices(GrassBatch batch)
        {
            batch.MatrixGroups.Clear();
            Dictionary<int, List<Matrix4x4>> matricesByPrefab = new Dictionary<int, List<Matrix4x4>>();

            foreach (var inst in batch.Instances)
            {
                if (!matricesByPrefab.ContainsKey(inst.PrefabIndex))
                    matricesByPrefab[inst.PrefabIndex] = new List<Matrix4x4>();

                Vector3 scale = Vector3.Lerp(Vector3.zero, inst.TargetScale, inst.GrowthProgress);
                matricesByPrefab[inst.PrefabIndex].Add(Matrix4x4.TRS(inst.Position, inst.Rotation, scale));
            }

            foreach (var kvp in matricesByPrefab)
            {
                int prefabIdx = kvp.Key;
                List<Matrix4x4> matrices = kvp.Value;
                List<Matrix4x4[]> groups = new List<Matrix4x4[]>();

                for (int i = 0; i < matrices.Count; i += 1023)
                {
                    int count = Mathf.Min(1023, matrices.Count - i);
                    Matrix4x4[] group = new Matrix4x4[count];
                    matrices.CopyTo(i, group, 0, count);
                    groups.Add(group);
                }
                batch.MatrixGroups[prefabIdx] = groups;
            }
        }

        private Mesh GetMesh(GameObject prefab) => prefab.GetComponentInChildren<MeshFilter>()?.sharedMesh;
        private Material GetMaterial(GameObject prefab) => prefab.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;

        private IEnumerator GrowthLoop()
        {
            while (true)
            {
                // Flora density tracks Atmosphere toward the MVP +0.25 atm delta (not Oxygen).
                GetAtmosphereFloraGate(out bool allowSpawn, out float densityFactor, out float intervalScale);
                if (!allowSpawn)
                {
                    yield return new WaitForSeconds(spawnInterval);
                    continue;
                }

                float currentInterval = spawnInterval * intervalScale;
                yield return new WaitForSeconds(Mathf.Clamp(currentInterval, 0.5f, spawnInterval * 2f));

                foreach (var node in cachedNodes)
                {
                    if (node == null) continue;
                    if (node.TryGetComponent<BaseBuilding>(out var b) && !b.IsOperating) continue;
                    EnsureNodeData(node);

                    int currentMaxPlants = Mathf.RoundToInt(maxPlantsPerZone * densityFactor);
                    int currentMaxGrass = Mathf.RoundToInt(maxGrassPerZone * densityFactor);

                    for (int i = 0; i < spawnAttemptsPerInterval; i++)
                    {
                        if (zonePlants[node].Count < currentMaxPlants)
                            TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]);
                        
                        if (zoneGrassBatches[node].Instances.Count < currentMaxGrass)
                            TrySpawnGrassInstance(node);
                    }
                }
            }
        }

        /// <summary>
        /// Spawn rate/density scales with Atmosphere progress toward the MVP round delta.
        /// </summary>
        private static void GetAtmosphereFloraGate(out bool allowSpawn, out float densityFactor, out float intervalScale)
        {
            allowSpawn = false;
            densityFactor = 0f;
            intervalScale = 1f;

            Owner owner = GameOverManager.MonitoredOwner;
            float atmos = 0.01f;
            if (Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(owner, out float val))
                atmos = val;

            float baseline = 0.01f;
            float requiredDelta = GenerationManager.SectorAtmosphereDelta;
            if (GenerationManager.Instance != null)
                baseline = GenerationManager.Instance.BaselineAtmosphere;

            if (requiredDelta <= 0.0001f)
            {
                allowSpawn = true;
                densityFactor = 1f;
                intervalScale = 0.55f;
                return;
            }

            float progress = Mathf.Clamp01((atmos - baseline) / requiredDelta);
            if (progress <= 0.001f) return;

            allowSpawn = true;
            // Trickle early, full density at the Atmos win line.
            densityFactor = Mathf.Lerp(0.12f, 1f, progress);
            intervalScale = Mathf.Lerp(1.5f, 0.55f, progress);
        }

        private void EnsureNodeData(LifeSupportNode node)
        {
            if (!zonePlants.ContainsKey(node)) zonePlants[node] = new List<GameObject>();
            if (!zoneGrassBatches.ContainsKey(node)) zoneGrassBatches[node] = new GrassBatch();
            
            zonePlants[node].RemoveAll(p => p == null);
        }

        private void TrySpawnGrassInstance(LifeSupportNode node)
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0) return;

            Vector2 randomCircle = Random.insideUnitCircle * node.Radius;
            Vector3 spawnPos = node.transform.position + new Vector3(randomCircle.x, 50f, randomCircle.y);

            int mask = groundLayer.value;
            if (mask == 0)
            {
                mask = LayerMask.GetMask("Default", "Terrain");
            }
            int transparentLayer = LayerMask.NameToLayer("TransparentFX");
            if (transparentLayer != -1)
            {
                mask |= (1 << transparentLayer);
            }

            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f, mask))
            {
                int prefabIdx = Random.Range(0, grassPrefabs.Length);
                var inst = new GrassInstance
                {
                    Position = hit.point,
                    Rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
                    TargetScale = new Vector3(0.04f, 0.03f, 0.04f),
                    GrowthProgress = 0f,
                    GrowthDuration = 20f * Random.Range(0.8f, 1.2f),
                    PrefabIndex = prefabIdx
                };
                zoneGrassBatches[node].Instances.Add(inst);
                zoneGrassBatches[node].NeedsUpdate = true;
            }
        }

        [ContextMenu("Fill All Zones (Zero Growth)")]
        public void FillAllZonesNow()
        {
            if (Application.isPlaying) StartCoroutine(FillAllZonesRoutine());
            else ExecuteFillNow();
        }

        private IEnumerator FillAllZonesRoutine()
        {
            UpdateNodeCache();

            int spawnCountThisFrame = 0;
            foreach (var node in cachedNodes)
            {
                if (node == null) continue;
                EnsureNodeData(node);
                
                // Plants (GameObjects)
                for (int i = 0; i < maxPlantsPerZone; i++)
                {
                    if (zonePlants[node].Count >= maxPlantsPerZone) break;
                    if (TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]) != null)
                    {
                        spawnCountThisFrame++;
                        if (spawnCountThisFrame >= maxSpawnsPerFrame) { spawnCountThisFrame = 0; yield return null; }
                    }
                }

                // Grass (Instances)
                for (int i = 0; i < maxGrassPerZone; i++)
                {
                    if (zoneGrassBatches[node].Instances.Count >= maxGrassPerZone) break;
                    TrySpawnGrassInstance(node);
                    spawnCountThisFrame++;
                    if (spawnCountThisFrame >= maxSpawnsPerFrame) { spawnCountThisFrame = 0; yield return null; }
                }
            }
        }

        private void ExecuteFillNow()
        {
            UpdateNodeCache();
            foreach (var node in cachedNodes)
            {
                if (node == null) continue;
                EnsureNodeData(node);
                for (int i = 0; i < maxPlantsPerZone; i++)
                    TrySpawnItem(node, plantPrefabs, plantMinSpacing, zonePlants[node]);
                for (int i = 0; i < maxGrassPerZone; i++)
                    TrySpawnGrassInstance(node);
            }
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
            
            zonePlants.Clear();
            zoneGrassBatches.Clear();
            Debug.Log("[VegetationManager] Cleared all vegetation.");
        }

        private GameObject TrySpawnItem(LifeSupportNode node, GameObject[] prefabs, float spacing, List<GameObject> collection)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            Vector2 randomCircle = Random.insideUnitCircle * node.Radius;
            Vector3 spawnPos = node.transform.position + new Vector3(randomCircle.x, 50f, randomCircle.y);

            int mask = groundLayer.value;
            if (mask == 0)
            {
                mask = LayerMask.GetMask("Default", "Terrain");
            }
            int transparentLayer = LayerMask.NameToLayer("TransparentFX");
            if (transparentLayer != -1)
            {
                mask |= (1 << transparentLayer);
            }

            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f, mask))
            {
                // Simple distance check for plant spacing
                bool tooClose = false;
                int checkCount = Mathf.Min(collection.Count, 10);
                for (int i = collection.Count - 1; i >= collection.Count - checkCount; i--)
                {
                    if (collection[i] != null && Vector3.Distance(hit.point, collection[i].transform.position) < spacing)
                    {
                        tooClose = true; break;
                    }
                }

                if (!tooClose)
                {
                    GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                    GameObject item = Instantiate(prefab, hit.point, Quaternion.Euler(0, Random.Range(0, 360), 0));
                    item.transform.SetParent(transform);
                    item.layer = 2; 
                    
                    var gv = item.AddComponent<GrowingVegetation>();
                    gv.SetDuration(45f);
                    gv.SetTargetScale(new Vector3(0.15f, 0.15f, 0.15f));
                    gv.ApplyColorTint(new Color(0.1f, 0.6f, 0.2f)); 
                    collection.Add(item);
                    return item;
                }
            }
            return null;
        }
    }
}
