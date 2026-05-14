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

            int width = Config.MapWidth;
            int height = Config.MapHeight;

            Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[width * height * 6];

            for (int i = 0, y = 0; y <= height; y++)
            {
                for (int x = 0; x <= width; x++, i++)
                {
                    // Simple edge blending for seamless wrap
                    float noise = GetSeamlessNoise(x, y, width, height, Config.NoiseScale);
                    float yPos = noise * Config.HeightMultiplier;

                    vertices[i] = new Vector3(x * CellSize, yPos, y * CellSize);
                    uvs[i] = new Vector2((float)x / width, (float)y / height);
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

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            GetComponent<MeshFilter>().mesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = new Color(0.2f, 0.5f, 0.2f); // Default dark green
                    renderer.sharedMaterial = mat;
                }
            }

            // Create 8 visual ghosts for seamless wrapping
            float mapWidthWorld = width * CellSize;
            float mapHeightWorld = height * CellSize;

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue; // Skip the real central terrain
                    
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

            ScatterEnvironment();

            if (TryGetComponent<HiddenResourceSpawner>(out var resourceSpawner))
            {
                resourceSpawner.SpawnResources();
            }
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

        private float GetSeamlessNoise(float x, float y, float width, float height, float scale)
        {
            float s = x / scale;
            float t = y / scale;
            
            float dx = width / scale;
            float dy = height / scale;

            float n00 = Mathf.PerlinNoise(s, t);
            float n10 = Mathf.PerlinNoise(s - dx, t);
            float n01 = Mathf.PerlinNoise(s, t - dy);
            float n11 = Mathf.PerlinNoise(s - dx, t - dy);

            float blendX = x / width;
            float blendY = y / height;

            float valTop = Mathf.Lerp(n00, n10, blendX);
            float valBottom = Mathf.Lerp(n01, n11, blendX);
            return Mathf.Lerp(valTop, valBottom, blendY);
        }
    }
}
