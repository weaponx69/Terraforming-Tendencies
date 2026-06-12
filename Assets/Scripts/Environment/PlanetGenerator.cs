using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Linq;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class PlanetGenerator : MonoBehaviour
    {
        public static PlanetGenerator Instance { get; private set; }
        public static event System.Action OnPlanetGenerated;

        public PlanetConfig Config;
        public float CellSize = 1f;
        public bool SpawnFloraOnStart = false; // Default to barren planet

        [Header("Air Unit Settings")]
        public float AirUnitFlightHeight = 4f;

        [Header("Resource Configurations (Now auto-loaded from Resources)")]
        public SupplySO MineralsSupplySO;
        public SupplySO GasSupplySO;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // // Debug.LogWarning($"[PlanetGenerator] Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (GameDevTV.RTS.Player.CampaignManager.Instance != null && GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet != null)
            {
                Config = GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet;
            }

            if (MineralsSupplySO == null) MineralsSupplySO = Resources.Load<SupplySO>("Gatherable Supplies/Iron");
            if (GasSupplySO == null) GasSupplySO = Resources.Load<SupplySO>("Gatherable Supplies/Regolith");
            
            if (MineralsSupplySO == null || GasSupplySO == null)
            {
                // // Debug.LogWarning("[PlanetGenerator] Could not load SupplySOs from Resources! Ensure they exist in Assets/Resources/Gatherable Supplies/");
            }
            
            FixPreplacedGatherables();

            if (Application.isPlaying)
            {
                ClearPlanet();
                GeneratePlanet();
            }
            else if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                GeneratePlanet();
            }
            else
            {
                BakeAllNavMeshes();
            }
            }

            private void BakeAllNavMeshes()
            {
                // Ensure physics system is aware of feature colliders before bake
                Physics.SyncTransforms(); 

                int agentTypeCount = NavMesh.GetSettingsCount();

                // Create or find FlyZone child for Air Units
                Transform flyZone = transform.Find("FlyZone");
                if (flyZone != null)
                {
                    // If it was marked for destruction, it might still be found. Destroy it immediately.
                    DestroyImmediate(flyZone.gameObject);
                    flyZone = null;
                }
            
                if (flyZone == null)
                {
                    flyZone = new GameObject("FlyZone").transform;
                    flyZone.parent = transform;
                    flyZone.gameObject.layer = LayerMask.NameToLayer("TransparentFX");
                }
            
                flyZone.localPosition = new Vector3(0, AirUnitFlightHeight, 0); 
                flyZone.localRotation = Quaternion.identity;
                flyZone.localScale = Vector3.one;

                // Ensure FlyZone has the terrain mesh child for baking
                GameObject bakeMeshObj = new GameObject("BakeMesh");
                bakeMeshObj.transform.parent = flyZone;
                bakeMeshObj.transform.localPosition = Vector3.zero;
                bakeMeshObj.transform.localRotation = Quaternion.identity;
                bakeMeshObj.transform.localScale = Vector3.one;
                bakeMeshObj.layer = LayerMask.NameToLayer("TransparentFX");

                var tempRenderers = new System.Collections.Generic.List<MeshRenderer>();

                Mesh terrainMesh = GetComponent<MeshFilter>().sharedMesh;
                Material terrainMat = GetComponent<MeshRenderer>().sharedMaterial;

                if (!bakeMeshObj.TryGetComponent<MeshFilter>(out var ff)) ff = bakeMeshObj.AddComponent<MeshFilter>();
                ff.sharedMesh = terrainMesh;

                if (!bakeMeshObj.TryGetComponent<MeshRenderer>(out var mr)) mr = bakeMeshObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = terrainMat;
                mr.enabled = true;
                tempRenderers.Add(mr);

                // Ghosting for Air NavMesh wrapping
                float mapWidthWorld = Config.MapWidth * CellSize;
                float mapHeightWorld = Config.MapHeight * CellSize;
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if (x == 0 && z == 0) continue;
                        GameObject ghost = new GameObject($"BakeMesh Ghost ({x},{z})");
                        ghost.transform.parent = flyZone;
                        ghost.transform.localPosition = new Vector3(x * mapWidthWorld, 0, z * mapHeightWorld);
                        ghost.transform.localRotation = Quaternion.identity;
                        ghost.transform.localScale = Vector3.one;
                        ghost.gameObject.layer = LayerMask.NameToLayer("TransparentFX");
                    
                        var gff = ghost.AddComponent<MeshFilter>();
                        gff.sharedMesh = terrainMesh;

                        var gmr = ghost.AddComponent<MeshRenderer>();
                        gmr.sharedMaterial = terrainMat;
                        gmr.enabled = true;
                        tempRenderers.Add(gmr);
                    }
                }

                // Sync transforms so bakes see the newly created/moved ghosts
                Physics.SyncTransforms(); 

                for (int i = 0; i < agentTypeCount; i++)
                {
                    NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
                    bool isAirAgent = settings.agentTypeID != 0; // Assuming 0 is Humanoid

                    // Find where this surface should live
                    GameObject targetObj = isAirAgent ? flyZone.gameObject : gameObject;
                
                    NavMeshSurface surface = targetObj.AddComponent<NavMeshSurface>();
                    surface.agentTypeID = settings.agentTypeID;

                    // Use Children for Air Units to only bake the FlyZone elevated mesh
                    // Use All for ground units to catch ghosts and expansion features
                    surface.collectObjects = isAirAgent ? CollectObjects.Children : CollectObjects.All;
                    surface.useGeometry = NavMeshCollectGeometry.RenderMeshes; 
                
                    int mask = ~0;
                    int buildingsLayer = LayerMask.NameToLayer("Buildings");
                    int suppliesLayer = LayerMask.NameToLayer("Supplies");
                    int transparentLayer = LayerMask.NameToLayer("TransparentFX");
                
                    // Exclude buildings from static bake (they use NavMeshObstacles)
                    if (buildingsLayer != -1) mask &= ~(1 << buildingsLayer);
                    // Air units ignore supplies on the ground
                    if (isAirAgent && suppliesLayer != -1) mask &= ~(1 << suppliesLayer);
                    // CRITICAL: The FlyZone elevated geometry (used to bake the Air NavMesh at Y=4)
                    // lives on the TransparentFX layer. It must NOT contaminate the GROUND bake,
                    // otherwise a phantom walkable surface is created at flight height and
                    // buildings/units snap upward into the air. The air surface uses
                    // collectObjects=Children so it still bakes the FlyZone correctly.
                    if (!isAirAgent && transparentLayer != -1) mask &= ~(1 << transparentLayer);
                
                    surface.layerMask = mask;
                    surface.BuildNavMesh();
                }

                // Disable the renderers immediately after baking so they are never drawn
                foreach (var renderer in tempRenderers)
                {
                    renderer.enabled = false;
                }
            }

                public void ClearPlanet()
                {
                    // Use DestroyImmediate to ensure hierarchy is clean for immediate reconstruction
                    for (int i = transform.childCount - 1; i >= 0; i--)
                    {
                        DestroyImmediate(transform.GetChild(i).gameObject);
                    }
                    if (TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = null;
                    if (TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = null;

                    // Also clear any existing NavMeshSurfaces to avoid stale data
                    foreach (var surface in GetComponents<NavMeshSurface>())
                    {
                        DestroyImmediate(surface);
                    }
                }

                [ContextMenu("Generate Planet (Editor)")]
                public void GeneratePlanetEditor()
                {
                ClearPlanet();
                GeneratePlanet();
                }

                public void GeneratePlanet()
                {
                if (Config == null) return;

                Mesh mesh = new Mesh();
                mesh.name = "Procedural Planet Surface";
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                int width = Config.MapWidth;
                int height = Config.MapHeight;

                int vertexCount = (width + 1) * (height + 1);
                Vector3[] vertices = new Vector3[vertexCount];
                Vector2[] uvs = new Vector2[vertexCount];
                int[] triangles = new int[width * height * 6];

                float minHeight = 0f;
                float maxHeight = 0.1f; // Avoid divide by zero in UV mapping

                for (int i = 0, y = 0; y <= height; y++)
                {
                for (int x = 0; x <= width; x++, i++)
                {
                    // Flat terrain
                    float yPos = 0f;

                    vertices[i] = new Vector3(x * CellSize, yPos, y * CellSize);
                }
                }

                for (int ti = 0, vi = 0, y = 0; y < height; y++, vi++)
                {
                for (int x = 0; x < width; x++, ti += 6, vi++)
                {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + width + 1;
                    triangles[ti + 5] = vi + width + 2;
                }
                }

                // Assign UVs strictly based on normalized height for the gradient mapping
                for (int i = 0; i < vertices.Length; i++)
                {
                float normHeight = Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y);
                uvs[i] = new Vector2(0, normHeight);
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.uv = uvs;
                mesh.RecalculateNormals();

                // Fix lighting seams by averaging the normals on the opposite edges
                Vector3[] normals = mesh.normals;
                for (int y = 0; y <= height; y++)
                {
                int idxLeft = y * (width + 1);
                int idxRight = idxLeft + width;
                Vector3 avgNormalX = (normals[idxLeft] + normals[idxRight]).normalized;
                normals[idxLeft] = avgNormalX;
                normals[idxRight] = avgNormalX;
                }
                for (int x = 0; x <= width; x++)
                {
                int idxBottom = x;
                int idxTop = height * (width + 1) + x;
                Vector3 avgNormalZ = (normals[idxBottom] + normals[idxTop]).normalized;
                normals[idxBottom] = avgNormalZ;
                normals[idxTop] = avgNormalZ;
                }
                // Fix the 4 corners
                int c00 = 0;
                int c10 = width;
                int c01 = height * (width + 1);
                int c11 = height * (width + 1) + width;
                Vector3 avgCorner = (normals[c00] + normals[c10] + normals[c01] + normals[c11]).normalized;
                normals[c00] = avgCorner;
                normals[c10] = avgCorner;
                normals[c01] = avgCorner;
                normals[c11] = avgCorner;
            
                mesh.normals = normals;

                GetComponent<MeshFilter>().mesh = mesh;
                GetComponent<MeshCollider>().sharedMesh = mesh;

                MeshRenderer renderer = GetComponent<MeshRenderer>();
                Shader shader = Shader.Find("Custom/URP_CurvedWorld");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                Material mat = new Material(shader);
                mat.mainTexture = GenerateHeightGradient();
                mat.SetFloat("_Smoothness", 0.1f); // Smooth but not shiny
                renderer.sharedMaterial = mat;
                }

                // Create 8 visual ghosts for seamless wrapping
                float mapWidthWorld = width * CellSize;
                float mapHeightWorld = height * CellSize;

                int transparentLayer = LayerMask.NameToLayer("TransparentFX");
                if (transparentLayer == -1) transparentLayer = 1; // Fallback to layer 1

                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if (x == 0 && z == 0) continue; 
                        
                        GameObject ghost = new GameObject($"Terrain Ghost ({x},{z})");
                        ghost.transform.parent = transform;
                        ghost.transform.localPosition = new Vector3(x * mapWidthWorld, 0, z * mapHeightWorld);
                        // Use Default layer for terrain ghosts so raycasts hit them
                        ghost.layer = 0; 
                        
                        MeshFilter mf = ghost.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;
                        
                        MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = renderer.sharedMaterial;

                        MeshCollider mc = ghost.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                    }
                }

                ScatterSurfaceFeatures();
                if (SpawnFloraOnStart)
                {
                    ScatterFlora();
                }

                ScatterResources();
                ScatterFuelResources();

                BakeAllNavMeshes();
                ApplyCurvedWorldShader(gameObject);

                GameObject updater = new GameObject("CurvedWorldUpdater");
                updater.transform.parent = transform;
                updater.AddComponent<CurvedWorldUpdater>();

                if (OnPlanetGenerated != null)
                {
                    OnPlanetGenerated?.Invoke();
                }
                }

                private void ApplyCurvedWorldShader(GameObject root)
                {
                    Shader curvedShader = Shader.Find("Custom/URP_CurvedWorld");
                    if (curvedShader == null) return;

                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        Material[] sharedMaterials = r.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < sharedMaterials.Length; i++)
                        {
                            if (sharedMaterials[i] != null && sharedMaterials[i].shader != null && sharedMaterials[i].shader.name != "Custom/URP_CurvedWorld")
                            {
                                // Save texture and color before swapping
                                Texture mainTex = sharedMaterials[i].mainTexture;
                                Color mainColor = Color.white;
                                if (sharedMaterials[i].HasProperty("_Color")) mainColor = sharedMaterials[i].GetColor("_Color");
                                if (sharedMaterials[i].HasProperty("_BaseColor")) mainColor = sharedMaterials[i].GetColor("_BaseColor");

                                sharedMaterials[i].shader = curvedShader;
                                
                                // Re-apply to the new shader's properties
                                if (mainTex != null) sharedMaterials[i].SetTexture("_BaseMap", mainTex);
                                sharedMaterials[i].SetColor("_BaseColor", mainColor);

                                changed = true;
                            }
                        }
                        if (changed) r.sharedMaterials = sharedMaterials;
                    }
                }

                private void ScatterFlora()
                {
                    if (Config == null || Config.EnvironmentPrefabs == null || Config.EnvironmentPrefabs.Length == 0) return;

                    int width = Config.MapWidth;
                    int height = Config.MapHeight;
                    float mapWidthWorld = width * CellSize;
                    float mapHeightWorld = height * CellSize;
                
                    float exclusionRadius = 15f; 
                    Vector3 center = new Vector3((width * CellSize) / 2f, 0, (height * CellSize) / 2f);

                    int count = Config.EnvironmentDensity;
                    int maxAttempts = count * 20;
                    int spawnedCount = 0;
                    float minSpacing = 5f;
                    System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

                    for (int i = 0; i < maxAttempts && spawnedCount < count; i++)
                    {
                        float randomX = Random.Range(0f, mapWidthWorld);
                        float randomZ = Random.Range(0f, mapHeightWorld);
                        Vector3 spawnPos = new Vector3(randomX, 0, randomZ);
                        
                        if (Vector3.Distance(spawnPos, center) < exclusionRadius) continue;

                        bool tooClose = false;
                        foreach (Vector3 pos in spawnedPositions)
                        {
                            if (Vector3.Distance(pos, spawnPos) < minSpacing)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;
                        spawnedPositions.Add(spawnPos);

                        GameObject prefab = Config.EnvironmentPrefabs[Random.Range(0, Config.EnvironmentPrefabs.Length)];
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, spawnPos, randomRot, transform);
                        
                        float scaleVar = Random.Range(0.8f, 1.3f);
                        instance.transform.localScale *= scaleVar;
                        
                        spawnedCount++;
                    }
                }

                private void FixPreplacedGatherables()
                {
                    foreach (GatherableSupply gs in GetComponentsInChildren<GatherableSupply>(true))
                    {
                        string nameLower = gs.name.ToLower();
                        bool isGas = nameLower.Contains("gas") || nameLower.Contains("regolith");
                        bool isMinerals = nameLower.Contains("crystal") || nameLower.Contains("mineral") || nameLower.Contains("rock") || nameLower.Contains("iron");

                        if (isGas && (gs.Supply == null || gs.Supply.name != "Regolith"))
                        {
                            gs.Supply = GasSupplySO;
                        }
                        else if (isMinerals && (gs.Supply == null || gs.Supply.name != "Iron"))
                        {
                            gs.Supply = MineralsSupplySO;
                        }
                    
                        if (gs.Supply != null && gs.Amount <= 0)
                        {
                            gs.Amount = gs.Supply.MaxAmount;
                        }
                    }
                }

                private void EnsureGatherableSupply(GameObject go, SupplySO so)
                {
                    if (!go.TryGetComponent<GatherableSupply>(out var gs))
                    {
                        gs = go.AddComponent<GatherableSupply>();
                    }

                    if (go.GetComponent<Collider>() == null)
                    {
                        var col = go.AddComponent<BoxCollider>();
                        col.size = new Vector3(2f, 2f, 2f);
                    }

                    if (so != null)
                    {
                        gs.Supply = so;
                        gs.Amount = so.MaxAmount;
                    }
                }

                private void ScatterResources()
                {
                    if (Config == null || Config.ResourcePrefabs == null || Config.ResourcePrefabs.Length == 0) return;

                    int width = Config.MapWidth;
                    int height = Config.MapHeight;
                    float mapWidthWorld = width * CellSize;
                    float mapHeightWorld = height * CellSize;
                
                    float exclusionRadius = 15f; 
                    Vector3 center = new Vector3((width * CellSize) / 2f, 0, (height * CellSize) / 2f);

                    int count = Config.ResourceCount;
                    int maxAttempts = count * 20;
                    int spawnedCount = 0;
                    float minSpacing = 5f;
                    System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

                    for (int i = 0; i < maxAttempts && spawnedCount < count; i++)
                    {
                        float randomX = Random.Range(0f, mapWidthWorld);
                        float randomZ = Random.Range(0f, mapHeightWorld);
                        Vector3 spawnPos = new Vector3(randomX, 0, randomZ);
                        
                        if (Vector3.Distance(spawnPos, center) < exclusionRadius) continue;

                        bool tooClose = false;
                        foreach (Vector3 pos in spawnedPositions)
                        {
                            if (Vector3.Distance(pos, spawnPos) < minSpacing)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;
                        spawnedPositions.Add(spawnPos);

                        GameObject prefab = Config.ResourcePrefabs[Random.Range(0, Config.ResourcePrefabs.Length)];
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, spawnPos, randomRot, transform);
                        
                        SupplySO so = instance.name.ToLower().Contains("gas") ? GasSupplySO : MineralsSupplySO;
                        EnsureGatherableSupply(instance, so);

                        if (instance.GetComponent<GatherableSupply>() != null && instance.GetComponent<HiddenResource>() == null)
                        {
                            instance.AddComponent<HiddenResource>();
                        }
                        
                        spawnedCount++;

                        float margin = 20f;
                        for (int gx = -1; gx <= 1; gx++)
                        {
                            for (int gz = -1; gz <= 1; gz++)
                            {
                                if (gx == 0 && gz == 0) continue; 

                                bool xNeeded = (gx == 0) || (gx == -1 && spawnPos.x > mapWidthWorld - margin) || (gx == 1 && spawnPos.x < margin);
                                bool zNeeded = (gz == 0) || (gz == -1 && spawnPos.z > mapHeightWorld - margin) || (gz == 1 && spawnPos.z < margin);
                                
                                if (!xNeeded || !zNeeded) continue;
                                
                                Vector3 ghostPos = spawnPos + new Vector3(gx * mapWidthWorld, 0, gz * mapHeightWorld);
                                GameObject ghost = Instantiate(prefab, ghostPos, randomRot, instance.transform);
                                ghost.name = "Ghost";
                                ghost.transform.localScale = Vector3.one;
                                SetLayerRecursive(ghost, LayerMask.NameToLayer("TransparentFX"));
                            }
                        }
                    }
                }

                private void ScatterFuelResources()
                {
                    int width = Config.MapWidth;
                    int height = Config.MapHeight;
                    float mapWidthWorld = width * CellSize;
                    float mapHeightWorld = height * CellSize;
                
                    float exclusionRadius = 15f; 
                    Vector3 center = new Vector3((width * CellSize) / 2f, 0, (height * CellSize) / 2f);

                    GameObject ironPrefab = Resources.Load<GameObject>("Gatherable Supplies 1/Iron");
                    GameObject regolithPrefab = Resources.Load<GameObject>("Gatherable Supplies 1/Regolith");
                    GameObject[] specificPrefabs = new GameObject[] { ironPrefab, regolithPrefab };

                    int count = 250;
                    int maxAttempts = count * 20;
                    int spawnedCount = 0;
                    float minSpacing = 5f;
                    System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

                    for (int i = 0; i < maxAttempts && spawnedCount < count; i++)
                    {
                        float randomX = Random.Range(0f, mapWidthWorld);
                        float randomZ = Random.Range(0f, mapHeightWorld);
                        Vector3 spawnPos = new Vector3(randomX, 0, randomZ);
                        
                        if (Vector3.Distance(spawnPos, center) < exclusionRadius) continue;

                        bool tooClose = false;
                        foreach (Vector3 pos in spawnedPositions)
                        {
                            if (Vector3.Distance(pos, spawnPos) < minSpacing)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) continue;
                        spawnedPositions.Add(spawnPos);

                        GameObject prefab = specificPrefabs[Random.Range(0, specificPrefabs.Length)];
                        if (prefab == null) continue;

                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, spawnPos, randomRot, transform);
                        
                        spawnedCount++;

                        if (instance.GetComponent<GatherableSupply>() != null && instance.GetComponent<HiddenResource>() == null)
                        {
                            instance.AddComponent<HiddenResource>();
                        }
                        
                        // Handle wraparound ghosts
                        float margin = 20f;
                        for (int gx = -1; gx <= 1; gx++)
                        {
                            for (int gz = -1; gz <= 1; gz++)
                            {
                                if (gx == 0 && gz == 0) continue; 

                                bool xNeeded = (gx == 0) || (gx == -1 && spawnPos.x > mapWidthWorld - margin) || (gx == 1 && spawnPos.x < margin);
                                bool zNeeded = (gz == 0) || (gz == -1 && spawnPos.z > mapHeightWorld - margin) || (gz == 1 && spawnPos.z < margin);
                                
                                if (!xNeeded || !zNeeded) continue;
                                
                                Vector3 ghostPos = spawnPos + new Vector3(gx * mapWidthWorld, 0, gz * mapHeightWorld);
                                GameObject ghost = Instantiate(prefab, ghostPos, randomRot, instance.transform);
                                ghost.name = "Ghost";
                                ghost.transform.localScale = Vector3.one;
                                SetLayerRecursive(ghost, LayerMask.NameToLayer("TransparentFX"));
                            }
                        }
                    }
                }

                private void SetLayerRecursive(GameObject obj, int layer)
                {
                obj.layer = layer;
                foreach (Transform child in obj.transform)
                {
                    SetLayerRecursive(child.gameObject, layer);
                }
                }

                private Texture2D GenerateHeightGradient()
                {
                Texture2D tex = new Texture2D(1, 256);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear; // Smooth blending
            
                // Muted Mars Palette: Subtle contrast limited to a few specific tones
                Color colorLow = new Color(0.55f, 0.25f, 0.15f);   // Deep plains
                Color colorMid = new Color(0.65f, 0.35f, 0.20f);   // Slopes
                Color colorHigh = new Color(0.75f, 0.45f, 0.25f);  // Peaks

                for (int i = 0; i < 256; i++)
                {
                float t = i / 255f;
                Color c;
                
                // Smoothly blend across the three subtle tones
                if (t < 0.5f) c = Color.Lerp(colorLow, colorMid, t * 2f);
                else c = Color.Lerp(colorMid, colorHigh, (t - 0.5f) * 2f);
                
                tex.SetPixel(0, i, c);
                }
                tex.Apply();
                return tex;
                }

                private void ScatterSurfaceFeatures()
                {
                if (Config.SurfaceFeaturePrefabs == null || Config.SurfaceFeaturePrefabs.Length == 0) return;

                int width = Config.MapWidth;
                int height = Config.MapHeight;
            
                float exclusionRadius = 15f; 
                Vector3 center = new Vector3((width * CellSize) / 2f, 0, (height * CellSize) / 2f);

                int density = Config.SurfaceFeatureDensity;
                int maxAttempts = density * 10;
                int spawnedCount = 0;
                float minSpacing = 4f;
                System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

                int transparentLayer = LayerMask.NameToLayer("TransparentFX");

                for (int i = 0; i < maxAttempts && spawnedCount < density; i++)
                {
                float randomX = Random.Range(0f, width * CellSize);
                float randomZ = Random.Range(0f, height * CellSize);
                Vector3 spawnPos = new Vector3(randomX, 0, randomZ);
                
                if (Vector3.Distance(spawnPos, center) < exclusionRadius) continue;

                bool tooClose = false;
                foreach (Vector3 pos in spawnedPositions)
                {
                    if (Vector3.Distance(pos, spawnPos) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                spawnedPositions.Add(spawnPos);

                GameObject prefab = Config.SurfaceFeaturePrefabs[Random.Range(0, Config.SurfaceFeaturePrefabs.Length)];
                
                Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                GameObject instance = Instantiate(prefab, spawnPos, randomRot, transform);
                
                float scaleVar = Random.Range(0.8f, 1.3f);
                instance.transform.localScale *= scaleVar;

                // Ensure GatherableSupply is correctly configured if it's a mineral/crystal
                bool isMineral = instance.name.ToLower().Contains("crystal") || instance.name.ToLower().Contains("mineral");
                if (isMineral)
                {
                    EnsureGatherableSupply(instance, MineralsSupplySO);
                }
                
                Color groundColor = new Color(0.65f, 0.35f, 0.20f);

                if (!isMineral)
                {
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        Material[] sharedMaterials = r.sharedMaterials;
                        foreach (var m in sharedMaterials)
                        {
                            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", groundColor);
                            else if (m.HasProperty("_Color")) m.SetColor("_Color", groundColor);
                        }
                    }
                }

                if (instance.GetComponent<GatherableSupply>() != null && instance.GetComponent<HiddenResource>() == null)
                {
                    instance.AddComponent<HiddenResource>();
                }

                float mapWidthWorld = width * CellSize;
                float mapHeightWorld = height * CellSize;
                float margin = 20f;

                for (int gx = -1; gx <= 1; gx++)
                {
                    for (int gz = -1; gz <= 1; gz++)
                    {
                        if (gx == 0 && gz == 0) continue; 

                        // Only spawn ghost if original is close enough to the opposite edge to be seen when wrapping
                        bool xNeeded = (gx == 0) || (gx == -1 && spawnPos.x > mapWidthWorld - margin) || (gx == 1 && spawnPos.x < margin);
                        bool zNeeded = (gz == 0) || (gz == -1 && spawnPos.z > mapHeightWorld - margin) || (gz == 1 && spawnPos.z < margin);
                        
                        if (!xNeeded || !zNeeded) continue;
                        
                        Vector3 ghostPos = spawnPos + new Vector3(gx * mapWidthWorld, 0, gz * mapHeightWorld);
                        GameObject ghost = Instantiate(prefab, ghostPos, randomRot, instance.transform);
                        ghost.name = "Ghost";
                        ghost.transform.localScale = Vector3.one;
                        SetLayerRecursive(ghost, transparentLayer);
                        
                        Renderer[] ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
                        foreach (var r in ghostRenderers)
                        {
                            Material[] sharedMaterials = r.sharedMaterials;
                            foreach (var m in sharedMaterials)
                            {
                                if (!isMineral)
                                {
                                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", groundColor);
                                    else if (m.HasProperty("_Color")) m.SetColor("_Color", groundColor);
                                }
                            }
                        }

                        foreach (var c in ghost.GetComponentsInChildren<Collider>())
                        {
                            if (Application.isPlaying) c.enabled = false;
                            else DestroyImmediate(c);
                        }
                    }
                }

                spawnedCount++;
                }
                }


                }
                }
