using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Linq;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class PlanetGenerator : MonoBehaviour
    {
        public static PlanetGenerator Instance { get; private set; }
        public static event System.Action OnPlanetGenerated;

        /// <summary>Prologue-friendly deposit placement (play mode or Terraforming scene bake).</summary>
        public static bool IsProloguePlacementRun()
        {
            if (Object.FindAnyObjectByType<GameDevTV.RTS.Prologue.PrologueController>() != null) return true;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return scene.IndexOf("Terraforming", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public PlanetConfig Config;
        public float CellSize = 1f;
        public bool SpawnFloraOnStart = false; // Default to barren planet
        public bool HasGenerated { get; private set; }
        Vector3[] drawnVerts;
        int drawnWidth;
        int drawnHeight;

        public Vector3 StartingAreaCenter
        {
            get
            {
                if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
                {
                    return SectorManager.Instance.Sectors[0].Center;
                }

                return Config == null
                    ? Vector3.zero
                    : new Vector3(Config.MapWidth * CellSize / 2f, 0f, Config.MapHeight * CellSize / 2f);
            }
        }

        [Header("Air Unit Settings")]
        public float AirUnitFlightHeight = 4f;

        [Header("Resource Configurations (Now auto-loaded from Resources)")]
        public SupplySO MineralsSupplySO;
        public SupplySO GasSupplySO;

        private void Awake()
        {
            Instance = this;
            // Drop the detailed mesh collider as soon as the scene loads, in the
            // editor and in play, so physics is not cooking the drawn surface.
            if (HasBuiltRelief())
            {
                EnsureDrawnHeightCache();
                EnsureCoarseCollider();
                EnsureWrapStreaming();
            }
        }

        bool HasBuiltRelief()
        {
            if (Config == null)
                Config = Resources.Load<PlanetConfig>("Planet 1 - Easy");
            if (Config == null) return false;
            if (!TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                return false;
            int expected = (Config.MapWidth + 1) * (Config.MapHeight + 1);
            return filter.sharedMesh.vertexCount == expected && filter.sharedMesh.bounds.size.y > 0.5f;
        }

        private void Start()
        {
            if (GameDevTV.RTS.Player.CampaignManager.Instance != null && GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet != null)
            {
                Config = GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet;
            }

            // Fallback: If Config is still null, load the default Planet 1 - Easy config from Resources
            if (Config == null)
            {
                Config = Resources.Load<PlanetConfig>("Planet 1 - Easy");
            }

            if (MineralsSupplySO == null) MineralsSupplySO = Resources.Load<SupplySO>("Gatherable Supplies/Minerals");
            if (GasSupplySO == null) GasSupplySO = Resources.Load<SupplySO>("Gatherable Supplies/Gas");
            
            // For Iron and Regolith we use the same fallback SO structure
            SupplySO ironSO = Resources.Load<SupplySO>("Gatherable Supplies/Iron");
            SupplySO regolithSO = Resources.Load<SupplySO>("Gatherable Supplies/Regolith");
            
            if (MineralsSupplySO == null || GasSupplySO == null)
            {
                // // Debug.LogWarning("[PlanetGenerator] Could not load SupplySOs from Resources! Ensure they exist in Assets/Resources/Gatherable Supplies/");
            }

            if (SectorManager.Instance != null)
            {
                SectorManager.Instance.ReinitializeSectors();
            }
            
            FixPreplacedGatherables();

            ApplyPatchScaleFromConfig();
            bool meshReady = HasBuiltRelief();
            if (meshReady && NeedsPlanetRegenForPatchScale())
            {
                Debug.Log($"[PlanetGenerator] Regenerating terrain for PlanetPatchDiameter={Config.PlanetPatchDiameter:0} (was ~{CurrentMeshSpanX():0}m wide).");
                ClearPlanet();
                meshReady = false;
            }

            if (!meshReady)
            {
                ClearPlanet();
                GeneratePlanet();
            }
            else
            {
                // Mesh is already in the scene. Do not rebuild it.
                // The walk mesh is baked once, in play, and only if it is missing.
                HasGenerated = true;
                EnsureDrawnHeightCache();
                EnsureCoarseCollider();
                EnsureWrapStreaming();
                RefreshDepositMarkHeights();
                ApplyHorizonPresentationFromConfig();
                GroundExistingWorldObjects();
                if (Application.isPlaying)
                    BakeAllNavMeshes();
            }
            }

            void ApplyPatchScaleFromConfig()
            {
                if (Config == null || Config.PlanetPatchDiameter < 1f) return;
                int span = Mathf.Max(Config.MapWidth, Config.MapHeight);
                CellSize = Config.PlanetPatchDiameter / span;
            }

            bool NeedsPlanetRegenForPatchScale()
            {
                if (Config == null || Config.PlanetPatchDiameter < 1f) return false;
                float want = Config.MapWidth * CellSize;
                float have = CurrentMeshSpanX();
                if (have < 1f) return false;
                return Mathf.Abs(have - want) > want * 0.12f;
            }

            float CurrentMeshSpanX()
            {
                if (!TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null)
                    return 0f;
                return mf.sharedMesh.bounds.size.x;
            }

            public void ApplyHorizonPresentationFromConfig()
            {
                if (Config != null && Config.EnableHorizonCurvature)
                    EnableHorizonPresentation();
                else
                    DisableHorizonPresentation();
            }

            /// <summary>Curved terrain shader in Play Mode (off by default — keep geometry flat).</summary>
            public void EnableHorizonPresentation()
            {
                if (Config == null || !Config.EnableHorizonCurvature) return;
                float bodyR = Mathf.Max(Config.HorizonBodyRadiusMeters, 100f);
                float patch = Config.PlanetPatchDiameter > 1f
                    ? Config.PlanetPatchDiameter
                    : Mathf.Max(Config.MapWidth, Config.MapHeight) * CellSize;
                float half = patch * 0.5f;
                float strength = (half * half) / (2f * bodyR);
                strength = Mathf.Clamp(strength, 0.00004f, 0.004f);

                ApplyCurvedWorldShader(gameObject);
                CurvedWorldUpdater updater = GetComponentInChildren<CurvedWorldUpdater>(true);
                if (updater == null)
                {
                    var go = new GameObject("CurvedWorldUpdater");
                    go.transform.SetParent(transform, false);
                    updater = go.AddComponent<CurvedWorldUpdater>();
                }
                updater.CurveStrength = strength;
                updater.CurveOrigin = null;
            }

            public void DisableHorizonPresentation()
            {
                Shader.SetGlobalFloat("_CurveStrength", 0f);
                var updater = GetComponentInChildren<CurvedWorldUpdater>(true);
                if (updater != null)
                {
                    if (Application.isPlaying)
                        Destroy(updater.gameObject);
                    else
                        DestroyImmediate(updater.gameObject);
                }
            }

            private void BakeAllNavMeshes()
            {
                if (HasGroundNavMesh())
                {
                    Debug.Log("[PlanetGenerator] Ground navmesh already baked — skipping.");
                    return;
                }

                // Ensure physics system is aware of feature colliders before bake
                Physics.SyncTransforms();

                DisableNavMeshBakeInterferenceSources(out var disabledBehaviours, out var disabledRenderers);

                try
                {
                // Air units are retired. Drop any leftover FlyZone so its bake meshes
                // are not left in the scene drawing extra planet copies.
                Transform flyZone = transform.Find("FlyZone");
                if (flyZone != null)
                    DestroyImmediate(flyZone.gameObject);

                for (int c = 0; c < transform.childCount; c++)
                {
                    Transform wrap = transform.GetChild(c);
                    if (wrap == null || !wrap.name.StartsWith("Terrain Ghost")) continue;
                    if (wrap.TryGetComponent<MeshCollider>(out var wrapCol))
                        wrapCol.enabled = false;
                }

                int agentTypeCount = NavMesh.GetSettingsCount();

                for (int i = 0; i < agentTypeCount; i++)
                {
                    NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
                    if (settings.agentTypeID != 0)
                        continue;

                    NavMeshSurface surface = null;
                    foreach (var existing in GetComponents<NavMeshSurface>())
                    {
                        if (existing != null && existing.agentTypeID == 0)
                        {
                            surface = existing;
                            break;
                        }
                    }
                    if (surface == null)
                        surface = gameObject.AddComponent<NavMeshSurface>();
                    surface.agentTypeID = settings.agentTypeID;

                    // Ground units: terrain collider plus walkable colliders.
                    surface.collectObjects = CollectObjects.All;
                    surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                
                    int mask = ~0;
                    int buildingsLayer = LayerMask.NameToLayer("Buildings");
                    int transparentLayer = LayerMask.NameToLayer("TransparentFX");
                
                    // Exclude buildings from static bake (they use NavMeshObstacles)
                    if (buildingsLayer != -1) mask &= ~(1 << buildingsLayer);
                    // Keep TransparentFX out of the ground bake so leftover helper meshes
                    // cannot create a walkable surface above the terrain.
                    if (transparentLayer != -1) mask &= ~(1 << transparentLayer);

                    // Exclude Text meshes from NavMesh baking to prevent invalid vertex data errors
                    int textMeshProLayer = LayerMask.NameToLayer("TextMeshPro");
                    if (textMeshProLayer != -1) mask &= ~(1 << textMeshProLayer);
                    // Exclude UI layer from NavMesh baking
                    int uiLayer = LayerMask.NameToLayer("UI");
                    if (uiLayer != -1) mask &= ~(1 << uiLayer);
                
                    surface.layerMask = mask;
                    surface.BuildNavMesh();
                }
                }
                finally
                {
                    RestoreNavMeshBakeInterferenceSources(disabledBehaviours, disabledRenderers);
                    EnsureWrapStreaming();
                }
            }

            /// <summary>
            /// Wrap copies stay in the scene, but only the ones near the camera
            /// (and a short way along the current grab) are drawn.
            /// </summary>
            public void EnsureWrapStreaming()
            {
                var streamer = GetComponent<TerrainWrapStreamer>();
                if (streamer == null)
                    streamer = gameObject.AddComponent<TerrainWrapStreamer>();
                streamer.Bind(this);
            }

            bool HasGroundNavMesh()
            {
                foreach (var surface in GetComponents<NavMeshSurface>())
                {
                    if (surface != null && surface.agentTypeID == 0 && surface.navMeshData != null)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Clicks and the walk bake use a quarter-resolution copy of the visible mesh.
            /// The detailed mesh stays on the renderer only.
            /// </summary>
            public void EnsureCoarseCollider()
            {
                if (!TryGetComponent<MeshFilter>(out var visualFilter) || visualFilter.sharedMesh == null)
                    return;
                Vector3[] visualVerts = visualFilter.sharedMesh.vertices;
                CacheDrawnVerts(visualVerts);
                if (TryGetComponent<MeshCollider>(out var heavy))
                {
                    heavy.sharedMesh = null;
                    heavy.enabled = false;
                }

                Transform child = transform.Find("PlanetCollision");
                if (child == null)
                {
                    var go = new GameObject("PlanetCollision");
                    go.transform.SetParent(transform, false);
                    go.layer = gameObject.layer;
                    child = go.transform;
                }
                if (!child.TryGetComponent<MeshCollider>(out var coarseCol))
                    coarseCol = child.gameObject.AddComponent<MeshCollider>();
                Mesh coarse = BuildCoarseColliderMesh(visualVerts);
                if (coarse == null)
                    return;
                coarseCol.sharedMesh = coarse;
                coarseCol.enabled = true;

                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform wrap = transform.GetChild(i);
                    if (wrap == null || !wrap.name.StartsWith("Terrain Ghost")) continue;
                    if (!wrap.TryGetComponent<MeshCollider>(out var wrapCol)) continue;
                    wrapCol.sharedMesh = coarse;
                }
            }

            void CacheDrawnVerts(Vector3[] verts)
            {
                drawnVerts = verts;
                drawnWidth = Config != null ? Config.MapWidth : 0;
                drawnHeight = Config != null ? Config.MapHeight : 0;
            }

            /// <summary>Keep a copy of drawn mesh heights for placement (independent of physics colliders).</summary>
            public void EnsureDrawnHeightCache()
            {
                if (Config == null)
                    Config = Resources.Load<PlanetConfig>("Planet 1 - Easy");
                if (!TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    return;
                CacheDrawnVerts(filter.sharedMesh.vertices);
            }

            void RefreshDepositMarkHeights()
            {
                float floatHeight = GameDevTV.RTS.Prologue.PrologueIceMarkers.DepositFloatHeight;
                foreach (var mark in Object.FindObjectsByType<GameDevTV.RTS.Prologue.PrologueDepositMark>(FindObjectsInactive.Exclude))
                {
                    if (mark == null) continue;
                    Vector3 ground = mark.GroundPoint;
                    ground.y = SampleGroundY(ground);
                    mark.GroundPoint = ground;
                    var root = mark.transform;
                    root.position = ground + Vector3.up * floatHeight;
                    GameDevTV.RTS.Prologue.PrologueDepositMark.EnsureClickVolume(root, floatHeight);
                }
            }

            Mesh BuildCoarseColliderMesh(Vector3[] src)
            {
                const int stride = 4;
                int width = Config != null ? Config.MapWidth : 0;
                int height = Config != null ? Config.MapHeight : 0;
                int srcStride = width + 1;
                if (src == null || width < stride || height < stride || src.Length != srcStride * (height + 1))
                    return null;

                int cellsX = width / stride;
                int cellsZ = height / stride;
                int vx = cellsX + 1;
                int vz = cellsZ + 1;
                var verts = new Vector3[vx * vz];
                for (int z = 0; z <= cellsZ; z++)
                {
                    int sz = Mathf.Min(z * stride, height);
                    for (int x = 0; x <= cellsX; x++)
                    {
                        int sx = Mathf.Min(x * stride, width);
                        verts[z * vx + x] = src[sz * srcStride + sx];
                    }
                }

                var tris = new int[cellsX * cellsZ * 6];
                int t = 0;
                for (int z = 0; z < cellsZ; z++)
                {
                    for (int x = 0; x < cellsX; x++)
                    {
                        int i = z * vx + x;
                        tris[t++] = i;
                        tris[t++] = i + vx;
                        tris[t++] = i + 1;
                        tris[t++] = i + 1;
                        tris[t++] = i + vx;
                        tris[t++] = i + vx + 1;
                    }
                }

                var mesh = new Mesh { name = "PlanetCollision" };
                if (verts.Length > 65000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>
            /// Screen-space / world TMP meshes are invalid NavMesh sources. Disabling
            /// Canvas alone is not enough — CollectObjects.All still gathers enabled
            /// MeshRenderers on TMP objects, so disable those renderers too.
            /// </summary>
            private static void DisableNavMeshBakeInterferenceSources(
                out System.Collections.Generic.List<Behaviour> disabledBehaviours,
                out System.Collections.Generic.List<Renderer> disabledRenderers)
            {
                disabledBehaviours = new System.Collections.Generic.List<Behaviour>();
                disabledRenderers = new System.Collections.Generic.List<Renderer>();

                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                {
                    if (canvas == null || !canvas.enabled) continue;
                    canvas.enabled = false;
                    disabledBehaviours.Add(canvas);
                }

                foreach (var tmp in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include))
                {
                    if (tmp == null) continue;
                    var renderers = tmp.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        if (renderer == null || !renderer.enabled) continue;
                        renderer.enabled = false;
                        disabledRenderers.Add(renderer);
                    }
                }
            }

            private static void RestoreNavMeshBakeInterferenceSources(
                System.Collections.Generic.List<Behaviour> disabledBehaviours,
                System.Collections.Generic.List<Renderer> disabledRenderers)
            {
                if (disabledBehaviours != null)
                {
                    foreach (var behaviour in disabledBehaviours)
                    {
                        if (behaviour != null)
                            behaviour.enabled = true;
                    }
                }

                if (disabledRenderers != null)
                {
                    foreach (var renderer in disabledRenderers)
                    {
                        if (renderer != null)
                            renderer.enabled = true;
                    }
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
                    drawnVerts = null;

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
                ApplyPatchScaleFromConfig();

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

                // Seeded hills on the visible mesh (same function as PlanetTerrainBuilder collider).
                Vector3 landingLocal = transform.InverseTransformPoint(StartingAreaCenter);
                var heightParams = PlanetTerrainBuilder.MakeParams(Config, CellSize, landingLocal);

                for (int i = 0, y = 0; y <= height; y++)
                {
                for (int x = 0; x <= width; x++, i++)
                {
                    float wx = x * CellSize;
                    float wz = y * CellSize;
                    float yPos = PlanetTerrainBuilder.SampleWorldY(wx, wz, in heightParams);

                    vertices[i] = new Vector3(wx, yPos, wz);
                    float u = width > 0 ? x / (float)width : 0f;
                    float v = height > 0 ? y / (float)height : 0f;
                    uvs[i] = new Vector2(u, v);
                    if (yPos < minHeight) minHeight = yPos;
                    if (yPos > maxHeight) maxHeight = yPos;
                }
                }

                // Ease height into the wrap so ghost copies don't meet on a crease.
                int stride = width + 1;
                int blend = Mathf.Clamp(width / 24, 4, 12);
                for (int y = 0; y <= height; y++)
                {
                    int row = y * stride;
                    float avgY = (vertices[row].y + vertices[row + width].y) * 0.5f;
                    for (int k = 0; k <= blend; k++)
                    {
                        float t = k / (float)blend;
                        t = t * t * (3f - 2f * t);
                        vertices[row + k].y = Mathf.Lerp(avgY, vertices[row + k].y, t);
                        vertices[row + width - k].y = Mathf.Lerp(avgY, vertices[row + width - k].y, t);
                    }
                }
                for (int x = 0; x <= width; x++)
                {
                    int bottom = x;
                    int top = height * stride + x;
                    float avgY = (vertices[bottom].y + vertices[top].y) * 0.5f;
                    for (int k = 0; k <= blend; k++)
                    {
                        float t = k / (float)blend;
                        t = t * t * (3f - 2f * t);
                        vertices[k * stride + x].y = Mathf.Lerp(avgY, vertices[k * stride + x].y, t);
                        vertices[(height - k) * stride + x].y = Mathf.Lerp(avgY, vertices[(height - k) * stride + x].y, t);
                    }
                }
                float cornerY = (vertices[0].y + vertices[width].y
                    + vertices[height * stride].y + vertices[height * stride + width].y) * 0.25f;
                vertices[0].y = cornerY;
                vertices[width].y = cornerY;
                vertices[height * stride].y = cornerY;
                vertices[height * stride + width].y = cornerY;

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
                if (TryGetComponent<MeshCollider>(out var heavyCol))
                {
                    heavyCol.sharedMesh = null;
                    heavyCol.enabled = false;
                }

                float generatedMapWidth = width * CellSize;
                float generatedMapHeight = height * CellSize;
                Vector3 mapMin = transform.TransformPoint(Vector3.zero);
                Vector3 mapMax = transform.TransformPoint(new Vector3(generatedMapWidth, 0f, generatedMapHeight));
                Vector3 mapCenter = transform.TransformPoint(new Vector3(generatedMapWidth / 2f, 0f, generatedMapHeight / 2f));
                Vector3 sectorCenter = SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0
                    ? SectorManager.Instance.Sectors[0].Center
                    : Vector3.zero;
                Debug.Log($"[PlanetGenerator] World bounds min={mapMin}, max={mapMax}, center={mapCenter}; Sector 0 center={sectorCenter}; offset={sectorCenter - mapCenter}");

                ApplyAuthoredOrGradientSurface();

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
                        if (TryGetComponent<MeshRenderer>(out var mainMr))
                            mr.sharedMaterial = mainMr.sharedMaterial;
                        mr.enabled = false;

                        MeshCollider mc = ghost.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.enabled = false;
                    }
                }

                // Remove old scattered resources — only node-based resources should exist
                ClearOldScatteredResources();

                // Ensure sectors are initialized before placing nodes
                if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count == 0)
                {
                    SectorManager.Instance.InitializeSectors();
                }

                // Terrain collider / SampleHeight (visual hills are already on the mesh above).
                Vector3 landing = StartingAreaCenter;
                try
                {
                    var built = PlanetTerrainBuilder.BuildOrUpdate(transform, Config, CellSize, landing);
                    if (built == null)
                        Debug.LogWarning("[PlanetGenerator] PlanetTerrainBuilder returned null — using mesh heights only.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[PlanetGenerator] Terrain build failed: {ex.Message}\n{ex.StackTrace}");
                }
                // Keep MeshRenderer on — mesh carries visible relief. Wrap copies stream in
                // near the camera so a grab still has ground ahead of it.
                Debug.Log($"[PlanetGenerator] Mesh relief span Y=[{minHeight:0.#}..{maxHeight:0.#}] wavelength={heightParams.Wavelength:0.#} amp={heightParams.HeightAmp:0.#}");

                // Place resource nodes per sector (replaces old random scatter)
                PlaceSectorResourceNodes();

                EnsureDrawnHeightCache();
                EnsureCoarseCollider();
                EnsureWrapStreaming();
                RefreshDepositMarkHeights();
                BakeAllNavMeshes();
                ApplyHorizonPresentationFromConfig();

                GroundExistingWorldObjects();

                HasGenerated = true;
                OnPlanetGenerated?.Invoke();
                // ClimateVisualStages may swap gradients on the same frame — reassert photo albedo.
                ApplyAuthoredOrGradientSurface();
                if (Application.isPlaying && Config != null && Config.AlbedoMap != null)
                    StartCoroutine(CoReassertPhotoSurface());
                }

        System.Collections.IEnumerator CoReassertPhotoSurface()
        {
            yield return null;
            ApplyAuthoredOrGradientSurface();
        }

        /// <summary>URP Lit with AlbedoMap when authored; otherwise height-band gradient.</summary>
        public void ApplyAuthoredOrGradientSurface()
        {
            if (Config == null) return;
            if (!TryGetComponent<MeshRenderer>(out var renderer)) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Custom/URP_CurvedWorld");
            if (shader == null) return;

            Material mat = new Material(shader);
            if (Config.AlbedoMap != null)
            {
                // The photo and its normal carry the hills and valleys.
                // Detail maps only add the close dust.
                var repeat = Vector2.one;
                Config.AlbedoMap.wrapMode = TextureWrapMode.Repeat;
                mat.mainTexture = Config.AlbedoMap;
                mat.mainTextureScale = repeat;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", Config.AlbedoMap);
                    mat.SetTextureScale("_BaseMap", repeat);
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1.12f, 1.04f, 0.96f, 1f));
                if (Config.NormalMap != null && mat.HasProperty("_BumpMap"))
                {
                    Config.NormalMap.wrapMode = TextureWrapMode.Repeat;
                    mat.SetTexture("_BumpMap", Config.NormalMap);
                    mat.SetTextureScale("_BumpMap", repeat);
                    mat.EnableKeyword("_NORMALMAP");
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.65f);
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                ApplyCloseSand(mat);
            }
            else
            {
                var grad = GenerateHeightGradient();
                grad.name = "ClimateGroundGrad";
                mat.mainTexture = grad;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", grad);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            }

            renderer.sharedMaterial = mat;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Terrain Ghost", System.StringComparison.Ordinal))
                    continue;
                if (child.TryGetComponent<MeshRenderer>(out var gmr))
                    gmr.sharedMaterial = mat;
            }

            if (Application.isPlaying && GameDevTV.RTS.Prologue.PrologueController.IsActive)
                GameDevTV.RTS.Prologue.PrologueGroundVisualTuning.SharpenPlanetTextures();

            Debug.Log(Config.AlbedoMap != null
                ? $"[PlanetGenerator] Surface material = photo albedo '{Config.AlbedoMap.name}'"
                : "[PlanetGenerator] Surface material = ClimateGroundGrad fallback");
        }

        /// <summary>
        /// Dust on the photo. Albedo adds grit. The normal stays quiet so it
        /// does not replace the hills and valleys.
        /// </summary>
        void ApplyCloseSand(Material mat)
        {
            if (Config.DetailAlbedoMap == null || !mat.HasProperty("_DetailAlbedoMap")) return;
            float tiling = Config.DetailTiling > 1f ? Config.DetailTiling : 32f;
            var repeat = new Vector2(tiling, tiling);
            Config.DetailAlbedoMap.wrapMode = TextureWrapMode.Repeat;
            mat.SetTexture("_DetailAlbedoMap", Config.DetailAlbedoMap);
            mat.SetTextureScale("_DetailAlbedoMap", repeat);
            if (mat.HasProperty("_DetailAlbedoMapScale"))
                mat.SetFloat("_DetailAlbedoMapScale", 1f);
            if (Config.DetailNormalMap != null && mat.HasProperty("_DetailNormalMap"))
            {
                Config.DetailNormalMap.wrapMode = TextureWrapMode.Repeat;
                mat.SetTexture("_DetailNormalMap", Config.DetailNormalMap);
                mat.SetTextureScale("_DetailNormalMap", repeat);
                if (mat.HasProperty("_DetailNormalMapScale"))
                    mat.SetFloat("_DetailNormalMapScale", 0.35f);
            }
            mat.EnableKeyword("_DETAIL_MULX2");
        }

        /// <summary>Pin buildings / deposits / gatherables to the mesh height after relief is baked.</summary>
        void GroundExistingWorldObjects()
        {
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null) continue;
                Vector3 p = b.transform.position;
                p.y = SampleGroundY(p);
                b.transform.position = p;
            }

            foreach (var gs in Object.FindObjectsByType<GatherableSupply>(FindObjectsInactive.Exclude))
            {
                if (gs == null) continue;
                Vector3 p = gs.transform.position;
                p.y = SampleGroundY(p);
                gs.transform.position = p;
            }

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null) continue;
                Vector3 p = hr.transform.position;
                p.y = SampleGroundY(p);
                hr.transform.position = p;
            }
        }

        /// <summary>World Y of the visible planet triangles, so placed meshes meet the ground you see.</summary>
        public float SampleGroundY(Vector3 worldPos)
        {
            if (TrySampleVisibleMesh(worldPos, out float meshY))
                return meshY;
            if (TrySampleCoarseCollider(worldPos, out meshY))
                return meshY;

            if (Config != null)
            {
                Vector3 local = transform.InverseTransformPoint(worldPos);
                Vector3 landingLocal = transform.InverseTransformPoint(StartingAreaCenter);
                var p = PlanetTerrainBuilder.MakeParams(Config, CellSize, landingLocal);
                float yLocal = PlanetTerrainBuilder.SampleWorldY(local.x, local.z, in p);
                return transform.TransformPoint(new Vector3(local.x, yLocal, local.z)).y;
            }

            int mask = LayerMask.GetMask("Default", "Terrain");
            if (mask == 0) mask = Physics.DefaultRaycastLayers;
            if (Physics.Raycast(worldPos + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, mask,
                    QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        bool TrySampleCoarseCollider(Vector3 worldPos, out float y)
        {
            y = 0f;
            Transform child = transform.Find("PlanetCollision");
            if (child == null || !child.TryGetComponent<MeshCollider>(out var col)
                || col.sharedMesh == null || !col.enabled)
                return false;
            Bounds bounds = col.bounds;
            var origin = new Vector3(worldPos.x, bounds.max.y + 25f, worldPos.z);
            var ray = new Ray(origin, Vector3.down);
            if (!col.Raycast(ray, out RaycastHit hit, bounds.size.y + 50f))
                return false;
            y = hit.point.y;
            return true;
        }

        bool TrySampleVisibleMesh(Vector3 worldPos, out float y)
        {
            y = 0f;
            if (drawnVerts == null)
                EnsureDrawnHeightCache();
            int width = drawnWidth;
            int height = drawnHeight;
            int stride = width + 1;
            if (drawnVerts == null || width < 1 || height < 1 || drawnVerts.Length != stride * (height + 1))
            {
                EnsureDrawnHeightCache();
                stride = drawnWidth + 1;
                if (drawnVerts == null || drawnWidth < 1 || drawnHeight < 1
                    || drawnVerts.Length != stride * (drawnHeight + 1))
                    return false;
                width = drawnWidth;
                height = drawnHeight;
            }

            Vector3 local = transform.InverseTransformPoint(worldPos);
            float mapW = width * CellSize;
            float mapH = height * CellSize;
            float lx = Mathf.Repeat(local.x, mapW);
            float lz = Mathf.Repeat(local.z, mapH);
            float fx = lx / CellSize;
            float fz = lz / CellSize;
            int x = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(fz), 0, height - 1);
            float u = Mathf.Clamp01(fx - x);
            float v = Mathf.Clamp01(fz - z);
            int i = z * stride + x;
            float y00 = drawnVerts[i].y;
            float y10 = drawnVerts[i + 1].y;
            float y01 = drawnVerts[i + stride].y;
            float y11 = drawnVerts[i + stride + 1].y;
            float localY = (u + v) <= 1f
                ? y00 * (1f - u - v) + y10 * u + y01 * v
                : y10 * (1f - v) + y01 * (1f - u) + y11 * (u + v - 1f);
            y = transform.TransformPoint(new Vector3(local.x, localY, local.z)).y;
            return true;
        }

                /// <summary>
                /// Place resource, feature, and nexus nodes in each sector.
                /// These are recorded as SectorNode entries and spawned as hidden GatherableSupply objects.
                /// </summary>
                private void PlaceSectorResourceNodes()
                {
                    if (SectorManager.Instance == null || SectorManager.Instance.Sectors.Count == 0)
                    {
                        Debug.LogError("[PlanetGenerator] Cannot place nodes — no sectors available!");
                        return;
                    }
                    Debug.Log($"[PlanetGenerator] Placing nodes in {SectorManager.Instance.Sectors.Count} sectors...");

                    bool prologueRun = IsProloguePlacementRun();
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        int sectorIndex = SectorManager.Instance.Sectors.IndexOf(sector);
                        float secW = (Config.MapWidth * CellSize) / Config.SectorsX;
                        float secH = (Config.MapHeight * CellSize) / Config.SectorsY;
                        Vector3 sectorMin = sector.Center - new Vector3(secW * 0.45f, 0, secH * 0.45f);
                        Vector3 sectorMax = sector.Center + new Vector3(secW * 0.45f, 0, secH * 0.45f);
                        // Keep deposits off the Command Post colony tile; sparse grid so non-mines have room.
                        Vector2Int cpCell = ColonyTileGrid.WorldToCell(sector.Center);
                        var usedDepositCells = new System.Collections.Generic.HashSet<Vector2Int> { cpCell };

                        bool isStartingSector = sectorIndex == 0;
                        int minDepositHexGap = prologueRun && isStartingSector ? 1 : 2;
                        float revealRadius = HexGridManager.Instance != null
                            ? HexGridManager.Instance.StartingAreaRevealRadius
                            : 15f;
                        float bootstrapRadius = Mathf.Max(ColonyTileGrid.TileSize * 2f, revealRadius * 0.7f);

                        System.Func<Vector3, bool> tryClaimDepositCell = candidate =>
                        {
                            Vector2Int cell = ColonyTileGrid.WorldToCell(candidate);
                            if (usedDepositCells.Contains(cell)) return false;
                            foreach (var used in usedDepositCells)
                            {
                                if (ColonyTileGrid.HexDistance(used, cell) < minDepositHexGap)
                                    return false;
                            }
                            usedDepositCells.Add(cell);
                            return true;
                        };

                        System.Func<Vector3> randomPos = () =>
                        {
                            for (int attempt = 0; attempt < 40; attempt++)
                            {
                                float rx = Random.Range(sectorMin.x, sectorMax.x);
                                float rz = Random.Range(sectorMin.z, sectorMax.z);
                                var candidate = ColonyTileGrid.SnapToTile(new Vector3(rx, 0, rz));
                                if (tryClaimDepositCell(candidate))
                                    return candidate;
                            }
                            // Fallback: walk hex rings from CP onto a free cell.
                            for (int ring = 2; ring <= 8; ring++)
                            {
                                for (int dq = -ring; dq <= ring; dq++)
                                {
                                    for (int dr = -ring; dr <= ring; dr++)
                                    {
                                        var cell = new Vector2Int(cpCell.x + dq, cpCell.y + dr);
                                        if (ColonyTileGrid.HexDistance(cpCell, cell) != ring) continue;
                                        Vector3 candidate = ColonyTileGrid.CellToWorldOnGround(cell);
                                        if (tryClaimDepositCell(candidate))
                                            return candidate;
                                    }
                                }
                            }
                            Vector2 dir = Random.insideUnitCircle.normalized;
                            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
                            return ColonyTileGrid.SnapToTile(
                                sector.Center + new Vector3(dir.x, 0f, dir.y) * (ColonyTileGrid.TileSize * 3f));
                        };

                        System.Func<Vector3> randomPosNearCamp = () =>
                        {
                            for (int attempt = 0; attempt < 24; attempt++)
                            {
                                int ring = Random.Range(3, 8);
                                int dq = Random.Range(-ring, ring + 1);
                                int dr = Random.Range(-ring, ring + 1);
                                var cell = new Vector2Int(cpCell.x + dq, cpCell.y + dr);
                                if (ColonyTileGrid.HexDistance(cpCell, cell) != ring) continue;
                                Vector3 candidate = ColonyTileGrid.CellToWorldOnGround(cell);
                                if (tryClaimDepositCell(candidate))
                                    return candidate;
                            }
                            for (int ring = 3; ring <= 8; ring++)
                            {
                                for (int dq = -ring; dq <= ring; dq++)
                                {
                                    for (int dr = -ring; dr <= ring; dr++)
                                    {
                                        var cell = new Vector2Int(cpCell.x + dq, cpCell.y + dr);
                                        if (ColonyTileGrid.HexDistance(cpCell, cell) != ring) continue;
                                        Vector3 candidate = ColonyTileGrid.CellToWorldOnGround(cell);
                                        if (tryClaimDepositCell(candidate))
                                            return candidate;
                                    }
                                }
                            }
                            return randomPos();
                        };
                        var pickDepositPos = prologueRun && isStartingSector
                            ? (System.Func<Vector3>)randomPosNearCamp
                            : randomPos;

                        // Sparse deposits (richer yield applied at spawn) — board stays placeable.
                        // PlanetConfig allowlist gates which gatherable types spawn (planet mix); select UI is future.
                        bool allowMinerals = Config == null || Config.AllowMinerals;
                        bool allowGas = Config == null || Config.AllowGas;
                        bool allowIron = Config == null || Config.AllowIron;
                        bool allowRegolith = Config == null || Config.AllowRegolith;

                        if (allowMinerals)
                        {
                            int mineralPads = prologueRun && isStartingSector ? 3 : 2;
                            for (int i = 0; i < mineralPads; i++)
                            {
                                Vector3 pos;
                                if (isStartingSector && i == 0)
                                {
                                    pos = ColonyTileGrid.SnapToTile(
                                        sector.Center + new Vector3(-bootstrapRadius * 0.9f, 0f, bootstrapRadius * 0.75f));
                                    if (!tryClaimDepositCell(pos))
                                        pos = randomPos();
                                }
                                else if (isStartingSector && i == 1)
                                {
                                    pos = ColonyTileGrid.SnapToTile(
                                        sector.Center + new Vector3(bootstrapRadius * 0.85f, 0f, -bootstrapRadius * 0.7f));
                                    if (!tryClaimDepositCell(pos))
                                        pos = randomPos();
                                }
                                else
                                {
                                    pos = pickDepositPos();
                                }
                                sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Minerals, pos, "A crystalline mineral deposit glistens in the light.", "Minerals"));
                            }
                        }

                        if (allowGas)
                            sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Gas, pickDepositPos(), "Vaporous gases seep from fissures in the ground.", "Gas"));
                        if (allowIron)
                            sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Iron, pickDepositPos(), "A rich iron ore deposit, suitable for smelting.", "Iron"));
                        if (allowRegolith)
                            sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Regolith, pickDepositPos(), "Loose regolith, useful for construction.", "Regolith"));

                        // Feature node (based on sector's assigned feature)
                        string featureLabel = "";
                        string featureFlavor = "";
                        switch (sector.Feature)
                        {
                            case SectorManager.SectorFeature.LavaTube:
                                featureLabel = "Lava Tube";
                                featureFlavor = "A vast lava tube network — place Lava Tube Outposts and Subterranean housing here.";
                                break;
                            case SectorManager.SectorFeature.FaultLine:
                                featureLabel = "Fault Line";
                                featureFlavor = "A deep geological fault — place Magnetic Shields and Sector Command Centers here.";
                                break;
                            case SectorManager.SectorFeature.WaterDeposit:
                                featureLabel = "Water Deposit";
                                featureFlavor = "Subsurface aquifer — place Aquifers and biosphere water works here.";
                                break;
                            case SectorManager.SectorFeature.Glacier:
                                featureLabel = "Glacier";
                                featureFlavor = "Polar ice sheet — place Subglacial Water Extractors here.";
                                break;
                            case SectorManager.SectorFeature.Volcano:
                                featureLabel = "Volcanic Vent";
                                featureFlavor = "An active volcanic vent — place Geothermal Generators here.";
                                break;
                        }
                        if (!string.IsNullOrEmpty(featureLabel))
                        {
                            Vector3 featurePos = prologueRun && isStartingSector
                                ? pickDepositPos()
                                : sector.Center + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                            var featureNode = new SectorNode(SectorNode.NodeType.Feature, featurePos, featureFlavor, featureLabel);
                            sector.Nodes.Add(featureNode);
                        }

                        // Nexus node (connects to next sector)
                        int nextSector = sectorIndex + 1;
                        if (nextSector < SectorManager.Instance.Sectors.Count)
                        {
                            Vector3 nexusPos = sector.Center + new Vector3(secW * 0.3f, 0, 0);
                            var nexusNode = new SectorNode(SectorNode.NodeType.Nexus, nexusPos, "A strange signal emanates from this point... leading to the next sector.", "Nexus Signal");
                            nexusNode.connectedSectorIndex = nextSector;
                            sector.Nodes.Add(nexusNode);
                        }

                        EnsureMinimumSectorResources(sector, sectorIndex);
                    }

                    // Build connection graph between nodes
                    BuildNodeConnections();

                    // Spawn visual markers (colored discs + labels)
                    SpawnNodeVisuals();

                    if (prologueRun)
                    {
                        // Prologue: leave every deposit as "?" — PrologueResourceFog owns reveal.
                        Debug.Log("[PlanetGenerator] Prologue active — skipping auto-discover / first-node explore.");
                    }
                    else
                    {
                        // Resource types for Sector 0 are discovered before node visuals exist.
                        // Re-apply so Minerals/Gas discs light up planet-wide (FoW retired).
                        DiscoverySystem.RefreshDiscoveredResourceNodes();
                        SectorManager.Instance.DiscoverResourcesInUnlockedSectors();

                        // Set Sector 0's first node as explored (entry point from UCC)
                        if (SectorManager.Instance.Sectors.Count > 0 && SectorManager.Instance.Sectors[0].Nodes.Count > 0)
                        {
                            var firstNode = SectorManager.Instance.Sectors[0].Nodes[0];
                            Debug.Log($"[PlanetGenerator] First node has {firstNode.connections.Count} connections before OnExplored()");
                            string connInfo = firstNode.connections.Count > 0
                                ? $"explored={firstNode.connections[0].isExplored} discovered={firstNode.connections[0].isDiscovered}"
                                : "NONE";
                            firstNode.OnExplored();
                            Debug.Log($"[PlanetGenerator] After OnExplored: {firstNode.connections.Count} connections. First connection: {connInfo}");
                        }
                    }

                    // Update visibility based on node states
                    UpdateAllNodeVisibility();

                    PlaceSectorBuildingSites();

                    Debug.Log($"[PlanetGenerator] Placed sector resource nodes across {SectorManager.Instance.Sectors.Count} sectors.");
                }

                /// <summary>
                /// Log nominal SO yield. Actual materials budget is met by richer Amount on the
                /// sparse deposit set at spawn — do not flood the sector with extra mine tiles.
                /// </summary>
                private void EnsureMinimumSectorResources(SectorManager.Sector sector, int sectorIndex)
                {
                    SupplySO ironSO = Resources.Load<SupplySO>("Gatherable Supplies/Iron");
                    SupplySO regolithSO = Resources.Load<SupplySO>("Gatherable Supplies/Regolith");
                    int nominal = SectorResourceBudget.CalculateGatherableYield(
                        sector, MineralsSupplySO, GasSupplySO, ironSO, regolithSO);
                    int nodes = 0;
                    if (sector?.Nodes != null)
                    {
                        foreach (var n in sector.Nodes)
                        {
                            if (n != null && SectorResourceBudget.IsGatherableNodeType(n.type))
                                nodes++;
                        }
                    }
                    Debug.Log(
                        $"[PlanetGenerator] Sector {sectorIndex}: {nodes} deposits " +
                        $"(nominal SO yield {nominal}; spawn Amount tops to {SectorResourceBudget.MinGatherableMaterialsPerSector}+).");
                }

                /// <summary>
                /// Pre-place reserved building pads per sector. Each sector gets one command post pad,
                /// three solar/building clusters, and mine pads on resource geography.
                /// Sector 0 guarantees Command Post + one Solar/Oxygen cluster inside the starting
                /// reveal radius so the bootstrap loop is always playable in the visible fog area.
                /// </summary>
                private void PlaceSectorBuildingSites()
                {
                    var markerRoot = new GameObject("BuildingSiteMarkers");
                    markerRoot.transform.parent = transform;
                    float revealRadius = HexGridManager.Instance != null
                        ? HexGridManager.Instance.StartingAreaRevealRadius
                        : 15f;
                    // Keep pads comfortably inside the reveal circle (hex edges / snap variance).
                    float bootstrapRadius = Mathf.Max(6f, revealRadius * 0.7f);

                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        sector.BuildingSites.Clear();
                        sector.BuildingClusters.Clear();

                        float secW = (Config.MapWidth * CellSize) / Config.SectorsX;
                        float secH = (Config.MapHeight * CellSize) / Config.SectorsY;
                        Vector3 sectorMin = sector.Center - new Vector3(secW * 0.4f, 0, secH * 0.4f);
                        Vector3 sectorMax = sector.Center + new Vector3(secW * 0.4f, 0, secH * 0.4f);
                        bool isStartingSector = SectorManager.Instance.Sectors.IndexOf(sector) == 0;

                        Vector3 commandPos = isStartingSector
                            ? SnapSitePosition(sector.Center + new Vector3(-bootstrapRadius * 0.35f, 0f, -bootstrapRadius * 0.2f))
                            : SnapSitePosition(sector.Center + new Vector3(-secW * 0.15f, 0, -secH * 0.15f));
                        // Starting pad sits on the hex center. The lit prologue disc is drawn there;
                        // a navmesh snap leaves the building beside that disc.
                        if (isStartingSector)
                            commandPos = ColonyTileGrid.CellToWorldOnGround(ColonyTileGrid.WorldToCell(commandPos));
                        var commandSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, commandPos, sector);
                        sector.BuildingSites.Add(commandSite);
                        commandSite.MarkerGO = CreateSiteMarker(markerRoot.transform, commandPos, BuildingSiteKind.CommandPost, commandSite);

                        int clustersPlaced = 0;
                        // Enough solar/paired clusters for climate goals (temp/atmos/water/oxygen/biomass)
                        // without exhausting pads before the sector can be finished.
                        const int clustersPerSector = 6;

                        if (isStartingSector)
                        {
                            // Guaranteed bootstrap cluster: Solar Pad + Paired Building (Oxygen Processor)
                            // inside the starting reveal so both sites are visible from turn 1.
                            Vector3 solarPos = SnapSitePosition(sector.Center + new Vector3(bootstrapRadius * 0.45f, 0f, bootstrapRadius * 0.15f));
                            Vector3 buildingPos = SnapSitePosition(solarPos + new Vector3(5.5f, 0f, 2f));
                            if (Vector3.Distance(buildingPos, sector.Center) > bootstrapRadius)
                            {
                                buildingPos = SnapSitePosition(solarPos + new Vector3(-5.5f, 0f, 2f));
                            }

                            PlaceCluster(sector, markerRoot.transform, solarPos, buildingPos);
                            clustersPlaced++;
                        }

                        for (int attempt = 0; attempt < 24 && clustersPlaced < clustersPerSector; attempt++)
                        {
                            Vector3 clusterCenter = SnapSitePosition(new Vector3(
                                Random.Range(sectorMin.x, sectorMax.x),
                                0f,
                                Random.Range(sectorMin.z, sectorMax.z)));
                            if (Vector3.Distance(clusterCenter, sector.Center) < 8f) continue;
                            if (isStartingSector && Vector3.Distance(clusterCenter, sector.Center) < bootstrapRadius + 4f)
                            {
                                continue; // don't crowd the guaranteed starter cluster
                            }

                            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                            Vector3 buildingOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f;
                            Vector3 solarPos = clusterCenter;
                            Vector3 buildingPos = SnapSitePosition(clusterCenter + buildingOffset);
                            if (Vector3.Distance(solarPos, buildingPos) < 4f) continue;

                            PlaceCluster(sector, markerRoot.transform, solarPos, buildingPos);
                            clustersPlaced++;
                        }
                    }

                    // Mine pads after clusters so resource nodes already exist.
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        foreach (var node in sector.Nodes)
                        {
                            if (node.type != SectorNode.NodeType.Minerals &&
                                node.type != SectorNode.NodeType.Gas &&
                                node.type != SectorNode.NodeType.Iron &&
                                node.type != SectorNode.NodeType.Regolith)
                            {
                                continue;
                            }

                            Vector3 minePos = node.visualGO != null
                                ? node.visualGO.transform.position
                                : SnapSitePosition(node.position);
                            var mineSite = new BuildingSiteSlot(BuildingSiteKind.Mine, minePos, sector, node);
                            sector.BuildingSites.Add(mineSite);
                            mineSite.MarkerGO = CreateSiteMarker(markerRoot.transform, minePos, BuildingSiteKind.Mine, mineSite);
                        }
                    }

                    int totalSites = 0;
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        totalSites += sector.BuildingSites.Count;
                    }

                    Debug.Log($"[PlanetGenerator] Placed {totalSites} reserved building sites.");
                }

                private void PlaceCluster(SectorManager.Sector sector, Transform markerRoot, Vector3 solarPos, Vector3 buildingPos)
                {
                    var cluster = new BuildingSiteCluster
                    {
                        Id = sector.BuildingClusters.Count,
                        Sector = sector
                    };

                    var solarSite = new BuildingSiteSlot(BuildingSiteKind.Solar, solarPos, sector);
                    solarSite.Cluster = cluster;
                    cluster.SolarSlot = solarSite;

                    var buildingSite = new BuildingSiteSlot(BuildingSiteKind.PairedBuilding, buildingPos, sector);
                    buildingSite.Cluster = cluster;
                    cluster.BuildingSlot = buildingSite;

                    sector.BuildingClusters.Add(cluster);
                    sector.BuildingSites.Add(solarSite);
                    sector.BuildingSites.Add(buildingSite);

                    solarSite.MarkerGO = CreateSiteMarker(markerRoot, solarPos, BuildingSiteKind.Solar, solarSite);
                    buildingSite.MarkerGO = CreateSiteMarker(markerRoot, buildingPos, BuildingSiteKind.PairedBuilding, buildingSite);
                }

                private Vector3 SnapSitePosition(Vector3 approximate)
                {
                    Vector3 pos = ColonyTileGrid.ApplyGroundY(approximate);

                    UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
                    {
                        agentTypeID = 0,
                        areaMask = UnityEngine.AI.NavMesh.AllAreas
                    };
                    if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit navHit, 8f, filter))
                    {
                        // Keep XZ from NavMesh if available, but height always from the mesh field.
                        pos = ColonyTileGrid.ApplyGroundY(new Vector3(navHit.position.x, 0f, navHit.position.z));
                    }

                    return pos;
                }

                private GameObject CreateSiteMarker(Transform parent, Vector3 position, BuildingSiteKind kind, BuildingSiteSlot site)
                {
                    var marker = new GameObject($"Site_{kind}");
                    marker.transform.SetParent(parent, false);
                    marker.transform.position = position;
                    marker.transform.rotation = Quaternion.identity;

                    var markerComponent = marker.AddComponent<BuildingSiteMarker>();
                    markerComponent.Initialize(site);

                    marker.layer = 0;
                    return marker;
                }

                /// <summary>
                /// Build a connection graph between nodes.
                /// Each node connects to its 2-3 nearest neighbors within the same sector.
                /// Cross-sector connections link the last node of sector N to the first of sector N+1.
                /// </summary>
                private void BuildNodeConnections()
                {
                    for (int s = 0; s < SectorManager.Instance.Sectors.Count; s++)
                    {
                        var nodes = SectorManager.Instance.Sectors[s].Nodes;
                        if (nodes.Count < 2) continue;

                        // Connect each node to its nearest neighbors
                        foreach (var node in nodes)
                        {
                            // Find 2-3 nearest nodes
                            var sorted = new System.Collections.Generic.List<SectorNode>(nodes);
                            sorted.Sort((a, b) =>
                                Vector3.Distance(node.position, a.position)
                                    .CompareTo(Vector3.Distance(node.position, b.position)));

                            int connectionsToAdd = UnityEngine.Random.Range(2, 4);
                            int added = 0;
                            foreach (var neighbor in sorted)
                            {
                                if (neighbor == node) continue;
                                if (node.connections.Contains(neighbor)) continue;
                                if (added >= connectionsToAdd) break;

                                node.connections.Add(neighbor);
                                neighbor.connections.Add(node);
                                added++;
                            }
                        }

                        // Cross-sector connection: link last node of sector s to first node of sector s+1
                        if (s + 1 < SectorManager.Instance.Sectors.Count)
                        {
                            var nextNodes = SectorManager.Instance.Sectors[s + 1].Nodes;
                            if (nextNodes.Count > 0 && nodes.Count > 0)
                            {
                                SectorNode lastNode = nodes[nodes.Count - 1];
                                SectorNode firstNext = nextNodes[0];
                                if (!lastNode.connections.Contains(firstNext))
                                {
                                    lastNode.connections.Add(firstNext);
                                    firstNext.connections.Add(lastNode);
                                }
                            }
                        }
                    }
                }

                /// <summary>
                /// Spawn small dot markers for each node + "?" labels for discovered-but-unexplored nodes.
                /// Also spawns gatherable supplies at resource nodes.
                /// </summary>
                private void SpawnNodeVisuals()
                {
                    var markerRoot = new GameObject("SectorNodeMarkers");
                    markerRoot.transform.parent = transform;
                    var questionMarkRoot = new GameObject("QuestionMarks");
                    questionMarkRoot.transform.parent = transform;

                    int totalDots = 0;
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        foreach (var node in sector.Nodes)
                        {
                            totalDots++;
                        }
                    }
                    Debug.Log($"[PlanetGenerator] Spawning {totalDots} node visuals across {SectorManager.Instance.Sectors.Count} sectors...");

                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        int gatherableCount = 0;
                        if (sector.Nodes != null)
                        {
                            foreach (var n in sector.Nodes)
                            {
                                if (n != null && SectorResourceBudget.IsGatherableNodeType(n.type))
                                    gatherableCount++;
                            }
                        }
                        int amountPerDeposit = Mathf.Max(
                            250,
                            Mathf.CeilToInt(
                                (float)SectorResourceBudget.MinGatherableMaterialsPerSector
                                / Mathf.Max(1, gatherableCount)));

                        foreach (var node in sector.Nodes)
                        {
                            if (node.visualGO != null) continue;

                            // Snap deposit pad to the visible mesh height field.
                            Vector2Int depositCell = ColonyTileGrid.WorldToCell(node.position);
                            Vector3 spawnPos = ColonyTileGrid.CellToWorldOnGround(depositCell);
                            node.position = spawnPos;

                            // --- Dot marker (small flat circle) ---
                            Color dotColor;
                            float dotSize;
                            switch (node.type)
                            {
                                case SectorNode.NodeType.Minerals:   dotColor = new Color(0.25f, 0.65f, 1f); dotSize = 0.55f; break;
                                case SectorNode.NodeType.Gas:        dotColor = new Color(0.2f, 1f, 0.35f); dotSize = 0.55f; break;
                                case SectorNode.NodeType.Iron:       dotColor = new Color(0.85f, 0.55f, 0.35f); dotSize = 0.55f; break;
                                case SectorNode.NodeType.Regolith:   dotColor = new Color(0.75f, 0.55f, 0.25f); dotSize = 0.55f; break;
                                case SectorNode.NodeType.Feature:
                                    // WaterDeposit cyan; Glacier white-blue; other geology orange.
                                    string fl = node.labelOverride ?? string.Empty;
                                    if (fl.IndexOf("Glacier", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                        dotColor = new Color(0.85f, 0.95f, 1f);
                                    else if (fl.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                        dotColor = new Color(0.35f, 0.85f, 1f);
                                    else
                                        dotColor = new Color(1f, 0.55f, 0.15f);
                                    dotSize = 0.85f;
                                    break;
                                case SectorNode.NodeType.Nexus:      dotColor = new Color(1f, 0f, 1f); dotSize = 0.55f; break;
                                default: dotColor = Color.white; dotSize = 0.4f; break;
                            }

                            // Use a flat cylinder (disc) for the dot — large enough to read as a deposit pad.
                            var dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            dot.name = $"Node_{node.type}";
                            dot.transform.position = spawnPos;
                            float visSize = Mathf.Max(dotSize, 0.7f);
                            dot.transform.localScale = new Vector3(visSize, 0.08f, visSize);
                            dot.transform.parent = transform; // Parent to PlanetGenerator so AI can find it
                            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.SeatVisualsOnGround(dot, 0.02f);
                            dot.layer = LayerMask.NameToLayer("Supplies"); // Set to Supplies layer
                            var dotRenderer = dot.GetComponent<MeshRenderer>();
                            var dotMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            dotMat.color = dotColor;
                            if (dotMat.HasProperty("_EmissionColor"))
                            {
                                dotMat.EnableKeyword("_EMISSION");
                                dotMat.SetColor("_EmissionColor", dotColor * 1.4f);
                            }
                            dotRenderer.material = dotMat;

                            // Make the collider larger and wider for easier clicking
                            var capCol = dot.GetComponent<CapsuleCollider>();
                            if (capCol != null)
                            {
                                capCol.radius = Mathf.Max(visSize * 0.8f, 0.5f); // Wide click radius
                                capCol.height = 0.5f; // Tall enough to click
                                capCol.center = new Vector3(0, 0.25f, 0); // Center it above ground
                            }

                            // For resource nodes, make the dot itself gatherable
                            if (node.type == SectorNode.NodeType.Minerals ||
                                node.type == SectorNode.NodeType.Gas ||
                                node.type == SectorNode.NodeType.Iron ||
                                node.type == SectorNode.NodeType.Regolith)
                            {
                                SupplySO supplySO = null;
                                string resourceType = "";
                                switch (node.type)
                                {
                                    case SectorNode.NodeType.Minerals: supplySO = MineralsSupplySO; resourceType = "Minerals"; break;
                                    case SectorNode.NodeType.Gas: supplySO = GasSupplySO; resourceType = "Gas"; break;
                                    case SectorNode.NodeType.Iron: supplySO = Resources.Load<SupplySO>("Gatherable Supplies/Iron"); resourceType = "Iron"; break;
                                    case SectorNode.NodeType.Regolith: supplySO = Resources.Load<SupplySO>("Gatherable Supplies/Regolith"); resourceType = "Regolith"; break;
                                }
                                if (supplySO != null)
                                {
                                    var gs = dot.AddComponent<GatherableSupply>();
                                    gs.Supply = supplySO;
                                    // Sparse deposits: each node carries a share of the sector materials budget.
                                    gs.Amount = Mathf.Max(supplySO.MaxAmount > 0 ? supplySO.MaxAmount : 250, amountPerDeposit);
                                    gs.SetVisible(false);
                                    gs.ToggleColliders(false);

                                    var hr = dot.AddComponent<HiddenResource>();
                                    hr.ResourceTypeName = resourceType;
                                }
                            }

                                                         node.visualGO = dot;
                                                         var explorable = dot.AddComponent<ExplorableNode>();
                                                         explorable.NodeData = node;
                                                         
                                                         // --- Floating label (feature name or "?") ---
                                                         var qmGo = new GameObject($"QuestionMark_{node.type}");
                                                         Vector3 ground = spawnPos;
                                                         ground.y = SampleGroundY(ground);
                                                         float qmFloat = GameDevTV.RTS.Prologue.PrologueIceMarkers.DepositFloatHeight;
                                                         qmGo.transform.position = ground + Vector3.up * qmFloat;
                                                         qmGo.transform.parent = questionMarkRoot.transform;
                                                         var qmText = qmGo.AddComponent<TMPro.TextMeshPro>();
                                                         bool prologueRun = IsProloguePlacementRun();
                                                         // Prologue: every deposit/feature is a mystery "?" until Survey Team uncovers it.
                                                         string label = prologueRun
                                                             ? "?"
                                                             : (!string.IsNullOrEmpty(node.labelOverride)
                                                                 ? node.labelOverride
                                                                 : "?");
                                                         qmText.text = label;
                                                         // Match Prologue ice "?" size (half of the old ice scale).
                                                         float qmScale = GameDevTV.RTS.Prologue.PrologueIceMarkers.MarkerScale;
                                                         float qmFont = GameDevTV.RTS.Prologue.PrologueIceMarkers.MarkerFontSize;
                                                         qmText.fontSize = qmFont;
                                                         qmText.alignment = TMPro.TextAlignmentOptions.Center;
                                                         qmText.color = prologueRun
                                                             ? new Color(1f, 0.92f, 0.2f, 1f)
                                                             : (node.type == SectorNode.NodeType.Feature
                                                                 ? new Color(0.55f, 0.95f, 1f)
                                                                 : Color.yellow);
                                                         qmText.fontStyle = TMPro.FontStyles.Bold;
                                                         qmText.transform.localScale = Vector3.one * qmScale;
                                                         var depositMark = qmGo.AddComponent<GameDevTV.RTS.Prologue.PrologueDepositMark>();
                                                         depositMark.GroundPoint = ground;
                                                         GameDevTV.RTS.Prologue.PrologueDepositMark.EnsureClickVolume(qmGo.transform, qmFloat);
                                                         node.questionMarkGO = qmGo;
                                                     }
                                                 }
                                             }

                /// <summary>
                /// Destroy all old scattered GatherableSupply objects left from previous generation.
                /// </summary>
                private void ClearOldScatteredResources()
                {
                    var oldSupplies = GetComponentsInChildren<GatherableSupply>(true);
                    int count = 0;
                    foreach (var gs in oldSupplies)
                    {
                        if (gs != null && gs.gameObject != null)
                        {
                            DestroyImmediate(gs.gameObject);
                            count++;
                        }
                    }
                    if (count > 0)
                        Debug.Log($"[PlanetGenerator] Cleaned up {count} old scattered resources.");
                }

                public void ReplenishResources()
                {
                    // Resources are node-based now — they persist across generations.
                    // Only reset node visibility and refresh sector discoveries.
                    if (SectorManager.Instance != null)
                    {
                        SectorManager.Instance.DiscoverResourcesInUnlockedSectors();
                    }

                    Debug.Log("[PlanetGenerator] Resources are node-based — not scattering new ones.");
                }

                /// <summary>
                /// No-op — resources are node-based and pre-placed at planet generation.
                /// </summary>
                public void ReplenishResourcesInSector(SectorManager.Sector sector)
                {
                    // No-op
                }

                /// <summary>
                /// Update visibility of dots and labels. FoW / node shroud retired — show all markers.
                /// </summary>
                private void UpdateAllNodeVisibility()
                {
                    bool prologueRun = IsProloguePlacementRun();
                    foreach (var sector in SectorManager.Instance.Sectors)
                    {
                        foreach (var node in sector.Nodes)
                        {
                            if (prologueRun)
                            {
                                // Keep GO alive for fog/reveal, but hide colored pucks — only "?" until survey.
                                node.SetVisualVisible(true);
                                if (node.visualGO != null)
                                {
                                    foreach (var r in node.visualGO.GetComponentsInChildren<Renderer>(true))
                                        if (r != null) r.enabled = false;
                                    if (node.visualGO.TryGetComponent(out GatherableSupply gs))
                                    {
                                        gs.SetVisible(false);
                                        gs.ToggleColliders(false);
                                    }
                                }
                                if (node.questionMarkGO != null)
                                {
                                    node.questionMarkGO.SetActive(true);
                                    var tmp = node.questionMarkGO.GetComponent<TMPro.TextMeshPro>();
                                    if (tmp != null)
                                    {
                                        tmp.text = "?";
                                        tmp.color = new Color(1f, 0.92f, 0.2f, 1f);
                                    }
                                }
                                node.isExplored = false;
                                node.isDiscovered = true;
                                continue;
                            }

                            node.SetVisualVisible(true);
                            // Feature / nexus keep name labels; hide legacy "?" on resources
                            // (HiddenResource beacons carry the deposit name instead).
                            bool showLabel = node.type == SectorNode.NodeType.Feature
                                || node.type == SectorNode.NodeType.Nexus;
                            node.SetQuestionMarkVisible(showLabel);
                        }
                    }
                }

                public void ApplyCurvedWorldShader(GameObject root)
                {
                    if (Config != null && !Config.EnableHorizonCurvature) return;
                    Shader curvedShader = Shader.Find("Custom/URP_CurvedWorld");
                    Shader curvedShaderTransparent = Shader.Find("Custom/URP_CurvedWorld_Transparent");
                    if (curvedShader == null || curvedShaderTransparent == null) return;

                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.gameObject.name == "Selection Indicator")
                        {
                            continue;
                        }

                        Material[] sharedMaterials = r.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < sharedMaterials.Length; i++)
                        {
                            if (sharedMaterials[i] != null && sharedMaterials[i].shader != null && sharedMaterials[i].shader.name != "Custom/URP_CurvedWorld")
                            {
                                string shaderName = sharedMaterials[i].shader.name;
                                if (shaderName.Contains("TextMeshPro") || shaderName.Contains("UI") || shaderName.Contains("Particles")) continue;

                                // Save texture and color before swapping
                                Texture mainTex = null;
                                if (sharedMaterials[i].HasProperty("_BaseMap")) mainTex = sharedMaterials[i].GetTexture("_BaseMap");
                                if (mainTex == null && sharedMaterials[i].HasProperty("_MainTex")) mainTex = sharedMaterials[i].GetTexture("_MainTex");
                                if (mainTex == null) mainTex = sharedMaterials[i].mainTexture;

                                Color mainColor = Color.white;
                                if (sharedMaterials[i].HasProperty("_BaseColor")) mainColor = sharedMaterials[i].GetColor("_BaseColor");
                                else if (sharedMaterials[i].HasProperty("_Color")) mainColor = sharedMaterials[i].GetColor("_Color");

                                Color emissionColor = Color.black;
                                bool hasEmission = false;
                                if (sharedMaterials[i].HasProperty("_EmissionColor"))
                                {
                                    emissionColor = sharedMaterials[i].GetColor("_EmissionColor");
                                    hasEmission = sharedMaterials[i].IsKeywordEnabled("_EMISSION") || (emissionColor.r > 0f || emissionColor.g > 0f || emissionColor.b > 0f);
                                }

                                bool isTransparent = sharedMaterials[i].renderQueue >= 3000 || shaderName.Contains("Transparent") || shaderName.Contains("Unlit");
                                sharedMaterials[i].shader = isTransparent ? curvedShaderTransparent : curvedShader;
                                
                                // Re-apply to the new shader's properties
                                if (mainTex != null) sharedMaterials[i].SetTexture("_BaseMap", mainTex);
                                sharedMaterials[i].SetColor("_BaseColor", mainColor);
                                if (hasEmission)
                                {
                                    sharedMaterials[i].SetColor("_EmissionColor", emissionColor);
                                    sharedMaterials[i].EnableKeyword("_EMISSION");
                                }

                                changed = true;
                            }
                        }
                        if (changed) r.sharedMaterials = sharedMaterials;
                    }
                }

                private Vector3 GetStartingAreaCenter()
                {
                    if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
                    {
                        return SectorManager.Instance.Sectors[0].Center;
                    }

                    return new Vector3(
                        (Config.MapWidth * CellSize) / 2f,
                        0,
                        (Config.MapHeight * CellSize) / 2f);
                }

                private void ScatterFlora()
                {
                    if (Config == null || Config.EnvironmentPrefabs == null || Config.EnvironmentPrefabs.Length == 0) return;

                    int width = Config.MapWidth;
                    int height = Config.MapHeight;
                    float mapWidthWorld = width * CellSize;
                    float mapHeightWorld = height * CellSize;
                
                    float exclusionRadius = 15f; 
                    Vector3 center = GetStartingAreaCenter();

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
                    Vector3 center = GetStartingAreaCenter();

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
                        
                        string resourceType;
                        SupplySO so;
                        if (instance.name.ToLower().Contains("gas"))
                        {
                            so = GasSupplySO;
                            resourceType = "Gas";
                        }
                        else
                        {
                            so = MineralsSupplySO;
                            resourceType = "Minerals";
                        }
                        EnsureGatherableSupply(instance, so);

                        if (instance.GetComponent<GatherableSupply>() != null && instance.GetComponent<HiddenResource>() == null)
                        {
                            var hr = instance.AddComponent<HiddenResource>();
                            hr.ResourceTypeName = resourceType;
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
                    if (Config == null || Config.SurfaceFeaturePrefabs == null || Config.SurfaceFeaturePrefabs.Length == 0) return;

                    int width = Config.MapWidth;
                    int height = Config.MapHeight;
                    float mapWidthWorld = width * CellSize;
                    float mapHeightWorld = height * CellSize;
                
                    float exclusionRadius = 15f; 
                    Vector3 center = GetStartingAreaCenter();

                    SupplySO ironSO = Resources.Load<SupplySO>("Gatherable Supplies/Iron");
                    SupplySO regolithSO = Resources.Load<SupplySO>("Gatherable Supplies/Regolith");
                    SupplySO[] specificSOs = new SupplySO[] { ironSO, regolithSO };

                    // Filter out crystals from SurfaceFeaturePrefabs so Iron/Regolith just look like normal rocks
                    System.Collections.Generic.List<GameObject> rockPrefabs = new System.Collections.Generic.List<GameObject>();
                    foreach (var p in Config.SurfaceFeaturePrefabs)
                    {
                        if (p != null && !p.name.ToLower().Contains("crystal") && !p.name.ToLower().Contains("mineral"))
                        {
                            rockPrefabs.Add(p);
                        }
                    }
                    if (rockPrefabs.Count == 0) return;

                    int count = 250;
                    int maxAttempts = count * 20;
                    int spawnedCount = 0;
                    float minSpacing = 5f;
                    System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

                    Color groundColor = new Color(0.65f, 0.35f, 0.20f);

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

                        GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, spawnPos, randomRot, transform);
                        
                        float scaleVar = Random.Range(0.8f, 1.3f);
                        instance.transform.localScale *= scaleVar;

                        spawnedCount++;

                        SupplySO so = specificSOs[Random.Range(0, specificSOs.Length)];
                        string resourceType = "Iron";
                        if (so != null)
                        {
                            EnsureGatherableSupply(instance, so);
                            resourceType = so.name.ToLower().Contains("regolith") ? "Regolith" : "Iron";
                        }

                        if (instance.GetComponent<GatherableSupply>() != null && instance.GetComponent<HiddenResource>() == null)
                        {
                            var hr = instance.AddComponent<HiddenResource>();
                            hr.ResourceTypeName = resourceType;
                        }

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
                                ghost.transform.localScale = Vector3.one * scaleVar;
                                SetLayerRecursive(ghost, LayerMask.NameToLayer("TransparentFX"));
                                
                                // Color the ghost too
                                Renderer[] ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
                                foreach (var gr in ghostRenderers)
                                {
                                    Material[] gShared = gr.sharedMaterials;
                                    foreach (var gm in gShared)
                                    {
                                        if (gm.HasProperty("_BaseColor")) gm.SetColor("_BaseColor", groundColor);
                                        else if (gm.HasProperty("_Color")) gm.SetColor("_Color", groundColor);
                                    }
                                }
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
                    // Muted Mars Palette — used at barren stage / cold start.
                    Color colorLow = new Color(0.55f, 0.25f, 0.15f);
                    Color colorMid = new Color(0.65f, 0.35f, 0.20f);
                    Color colorHigh = new Color(0.75f, 0.45f, 0.25f);
                    return GenerateHeightGradient(colorLow, colorMid, colorHigh);
                }

                /// <summary>
                /// Rebuild the terrain height-band texture and optional albedo tint.
                /// Used by <see cref="ClimateVisualStages"/> for barren→living ground look.
                /// </summary>
                public void ApplyClimateGroundPalette(Color colorLow, Color colorMid, Color colorHigh, Color baseTint)
                {
                    MeshRenderer renderer = GetComponent<MeshRenderer>();
                    if (renderer == null) return;

                    // Photo albedo planets keep the authored surface exact — no climate tint washout.
                    if (Config != null && Config.AlbedoMap != null)
                        return;

                    Material mat = renderer.material; // instance so we don't mutate a shared asset
                    if (mat == null) return;

                    if (mat.mainTexture is Texture2D oldTex && oldTex != null
                        && oldTex.name.StartsWith("ClimateGroundGrad", System.StringComparison.Ordinal))
                    {
                        Destroy(oldTex);
                    }

                    Texture2D gradient = GenerateHeightGradient(colorLow, colorMid, colorHigh);
                    gradient.name = "ClimateGroundGrad";
                    mat.mainTexture = gradient;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", gradient);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseTint);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseTint);

                    // Terrain wrap ghosts share the generator's material path — retarget them.
                    foreach (Transform child in transform)
                    {
                        if (child == null || !child.name.StartsWith("Terrain Ghost", System.StringComparison.Ordinal))
                            continue;
                        if (child.TryGetComponent(out MeshRenderer ghostRenderer))
                            ghostRenderer.sharedMaterial = mat;
                    }

                    renderer.sharedMaterial = mat;
                }

                private Texture2D GenerateHeightGradient(Color colorLow, Color colorMid, Color colorHigh)
                {
                Texture2D tex = new Texture2D(1, 256);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear; // Smooth blending

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
                Vector3 center = GetStartingAreaCenter();

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
