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

            int triangleCount = width * height * 2;
            int vertexCount = triangleCount * 3;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[vertexCount];

            // Precalculate heights
            float[,] heights = new float[width + 1, height + 1];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            for (int y = 0; y <= height; y++)
            {
                for (int x = 0; x <= width; x++)
                {
                    float noise = GetFractalNoise(x, y, width, height, Config.NoiseScale, 4);
                    noise = Mathf.Pow(noise, 1.8f);
                    float yPos = noise * Config.HeightMultiplier;
                    heights[x, y] = yPos;

                    if (yPos < minHeight) minHeight = yPos;
                    if (yPos > maxHeight) maxHeight = yPos;
                }
            }

            int vIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 p00 = new Vector3(x * CellSize, heights[x, y], y * CellSize);
                    Vector3 p10 = new Vector3((x + 1) * CellSize, heights[x + 1, y], y * CellSize);
                    Vector3 p01 = new Vector3(x * CellSize, heights[x, y + 1], (y + 1) * CellSize);
                    Vector3 p11 = new Vector3((x + 1) * CellSize, heights[x + 1, y + 1], (y + 1) * CellSize);

                    // Triangle 1
                    vertices[vIndex] = p00;
                    vertices[vIndex + 1] = p01;
                    vertices[vIndex + 2] = p11;

                    // Triangle 2
                    vertices[vIndex + 3] = p00;
                    vertices[vIndex + 4] = p11;
                    vertices[vIndex + 5] = p10;

                    for (int i = 0; i < 6; i++)
                    {
                        triangles[vIndex + i] = vIndex + i;
                        // Map UV y to normalized height for our gradient
                        float normHeight = Mathf.InverseLerp(minHeight, maxHeight, vertices[vIndex + i].y);
                        uvs[vIndex + i] = new Vector2(0, normHeight);
                    }
                    vIndex += 6;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            GetComponent<MeshFilter>().mesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = GenerateHeightGradient();
                mat.SetFloat("_Smoothness", 0.0f); // Completely matte for stark low-poly look
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
            tex.filterMode = FilterMode.Point; // Forces stark, sharp pixels with no blurring
            
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                Color c;
                
                // Create stark, hard-edged bands of color instead of a smooth fade
                if (t < 0.2f) c = new Color(0.15f, 0.05f, 0.05f); // Deep valleys
                else if (t < 0.45f) c = new Color(0.4f, 0.2f, 0.1f); // Lowlands
                else if (t < 0.75f) c = new Color(0.65f, 0.45f, 0.3f); // Slopes
                else c = new Color(0.85f, 0.7f, 0.5f); // Peaks
                
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

        private float GetFractalNoise(float x, float y, float width, float height, float scale, int octaves)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;
            
            for (int i = 0; i < octaves; i++)
            {
                total += GetSeamlessNoise(x * frequency, y * frequency, width * frequency, height * frequency, scale) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return total / maxValue;
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
