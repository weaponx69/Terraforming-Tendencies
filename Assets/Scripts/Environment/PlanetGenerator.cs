using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class PlanetGenerator : MonoBehaviour
    {
        public static PlanetGenerator Instance { get; private set; }

        public PlanetConfig Config;
        public float CellSize = 1f;
        public bool SpawnFloraOnStart = false; // Default to barren planet

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (GameDevTV.RTS.Player.CampaignManager.Instance != null && GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet != null)
            {
                Config = GameDevTV.RTS.Player.CampaignManager.Instance.CurrentPlanet;
            }
            
            // Only generate if we haven't already generated it in the editor
            if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                GeneratePlanet();
            }
            else
            {
                // If it was pre-generated in editor, we still need to bake navmesh and spawn resources at runtime
                if (TryGetComponent<NavMeshSurface>(out var navMeshSurface)) navMeshSurface.BuildNavMesh();
                if (TryGetComponent<HiddenResourceSpawner>(out var resourceSpawner)) resourceSpawner.SpawnResources();
            }
        }

        [ContextMenu("Clear Planet (Editor)")]
        public void ClearPlanet()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            if (TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = null;
            if (TryGetComponent<MeshCollider>(out var mc)) mc.sharedMesh = null;
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
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
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

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue; 
                    
                    GameObject ghost = new GameObject($"Terrain Ghost ({x},{z})");
                    ghost.transform.parent = transform;
                    ghost.transform.localPosition = new Vector3(x * mapWidthWorld, 0, z * mapHeightWorld);
                    
                    MeshFilter mf = ghost.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    
                    MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = renderer.sharedMaterial;
                }
            }

            if (TryGetComponent<NavMeshSurface>(out var navMeshSurface))
            {
                navMeshSurface.BuildNavMesh();
            }

            ScatterSurfaceFeatures();
            ScatterEnvironment();

            if (TryGetComponent<HiddenResourceSpawner>(out var resourceSpawner))
            {
                resourceSpawner.SpawnResources();
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

        private void ScatterEnvironment()
        {
            if (!SpawnFloraOnStart) return;
            if (Config.EnvironmentPrefabs == null || Config.EnvironmentPrefabs.Length == 0) return;

            float mapWidth = Config.MapWidth * CellSize;
            float mapHeight = Config.MapHeight * CellSize;
            
            float minSpacing = 4f; // Minimum distance to prevent clustering
            System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

            int maxAttempts = Config.EnvironmentDensity * 10;
            int spawnedCount = 0;

            for (int i = 0; i < maxAttempts && spawnedCount < Config.EnvironmentDensity; i++)
            {
                Vector3 randomPos = new Vector3(Random.Range(0, mapWidth), 0, Random.Range(0, mapHeight));
                
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    bool tooClose = false;
                    foreach(Vector3 pos in spawnedPositions)
                    {
                        if (Vector3.Distance(pos, hit.position) < minSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        GameObject prefab = Config.EnvironmentPrefabs[Random.Range(0, Config.EnvironmentPrefabs.Length)];
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, hit.position, randomRot, transform);
                        
                        float scaleVar = Random.Range(0.8f, 1.2f);
                        instance.transform.localScale *= scaleVar;

                        spawnedPositions.Add(hit.position);
                        spawnedCount++;
                    }
                }
            }
        }

        private void ScatterSurfaceFeatures()
        {
            if (Config.SurfaceFeaturePrefabs == null || Config.SurfaceFeaturePrefabs.Length == 0) return;

            float mapWidth = Config.MapWidth * CellSize;
            float mapHeight = Config.MapHeight * CellSize;
            
            float minSpacing = 5f; // Minimum distance to prevent overlapping big rocks
            System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

            int maxAttempts = Config.SurfaceFeatureDensity * 10;
            int spawnedCount = 0;

            for (int i = 0; i < maxAttempts && spawnedCount < Config.SurfaceFeatureDensity; i++)
            {
                Vector3 randomPos = new Vector3(Random.Range(0, mapWidth), 0, Random.Range(0, mapHeight));
                
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    bool tooClose = false;
                    foreach(Vector3 pos in spawnedPositions)
                    {
                        if (Vector3.Distance(pos, hit.position) < minSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        GameObject prefab = Config.SurfaceFeaturePrefabs[Random.Range(0, Config.SurfaceFeaturePrefabs.Length)];
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject instance = Instantiate(prefab, hit.position, randomRot, transform);
                        
                        float scaleVar = Random.Range(0.6f, 1.5f);
                        instance.transform.localScale *= scaleVar;

                        spawnedPositions.Add(hit.position);
                        spawnedCount++;
                    }
                }
            }
        }
    }
}
