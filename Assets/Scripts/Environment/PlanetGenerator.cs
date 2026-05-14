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
                    // Base Layer: Low-frequency Standard FBM (approx 40% influence)
                    float baseNoise = GetStandardFBM(x, y, width, height, Config.NoiseScale * 1.5f, 4, 0.45f);
                    
                    // Detail Layer: Ridged noise on top of the base layer
                    float ridgedNoise = GetRidgedMultifractal(x, y, width, height, Config.NoiseScale, 4, 0.45f);
                    ridgedNoise = Mathf.Clamp01(ridgedNoise / 1.5f);
                    
                    // Combine Base and Detail
                    float combinedNoise = (baseNoise * 0.4f) + (ridgedNoise * 0.6f);
                    
                    // Power Curve applied to final combined height to aggressively flatten lowlands
                    float finalNoise = Mathf.Pow(combinedNoise, 3.0f);
                    
                    // Clamping before scaling to world units
                    finalNoise = Mathf.Clamp01(finalNoise);
                    
                    float yPos = finalNoise * Config.HeightMultiplier;

                    // Add a second layer: Seamless Worley Noise for impact craters
                    float worleyDist = GetSeamlessWorleyNoise(x, y, width, height, 12f); // 12 cells across the map
                    float craterRadius = 0.35f; 
                    if (worleyDist < craterRadius)
                    {
                        // Map distance to a 0..1 scale within the crater
                        float t = worleyDist / craterRadius;
                        // Beautiful math profile for a crater: -cos(1.5*pi*t) * (1-t)
                        // This creates a deep bowl at t=0, crosses 0, peaks for a rim, and smoothly returns to 0 at t=1.
                        float craterShape = -Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t);
                        
                        yPos += craterShape * 5f; // Depth and rim height of the crater
                    }
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
            tex.filterMode = FilterMode.Bilinear; // Smooth blending
            
            Color colorDeep = new Color(0.15f, 0.05f, 0.05f); // Deep valleys
            Color colorLow = new Color(0.4f, 0.2f, 0.1f);     // Lowlands
            Color colorMid = new Color(0.65f, 0.45f, 0.3f);   // Slopes
            Color colorPeak = new Color(0.85f, 0.7f, 0.5f);   // Peaks

            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                Color c;
                
                // Smoothly blend between the high-contrast colors
                if (t < 0.2f) c = Color.Lerp(colorDeep, colorLow, t / 0.2f);
                else if (t < 0.45f) c = Color.Lerp(colorLow, colorMid, (t - 0.2f) / 0.25f);
                else if (t < 0.75f) c = Color.Lerp(colorMid, colorPeak, (t - 0.45f) / 0.3f);
                else c = colorPeak; // Top peaks stay solid light
                
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

        private float GetStandardFBM(float x, float y, float width, float height, float scale, int octaves, float persistence)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float maxValue = 0;
            
            for (int i = 0; i < octaves; i++)
            {
                total += GetSeamlessNoise(x * frequency, y * frequency, width * frequency, height * frequency, scale) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }
            return total / maxValue;
        }

        private float GetRidgedMultifractal(float x, float y, float width, float height, float scale, int octaves, float persistence)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float weight = 1.0f;
            
            for (int i = 0; i < octaves; i++)
            {
                // Get seamless noise, convert from 0..1 to -1..1
                float n = GetSeamlessNoise(x * frequency, y * frequency, width * frequency, height * frequency, scale);
                n = n * 2.0f - 1.0f;
                // Create sharp ridge by inverting absolute value
                n = 1.0f - Mathf.Abs(n);
                n *= n; // square to sharpen
                
                n *= weight; // Octave Weighting (Gain): detail only appears on ridges
                weight = Mathf.Clamp01(n); // Value of the previous octave limits the amplitude of the next
                
                total += n * amplitude;
                amplitude *= persistence; // Prevent peaks from becoming needles
                frequency *= 2f;
            }
            return total;
        }

        private float GetSeamlessWorleyNoise(float x, float y, float width, float height, float numCells)
        {
            float s = (x / width) * numCells;
            float t = (y / height) * numCells;

            int cellX = Mathf.FloorToInt(s);
            int cellY = Mathf.FloorToInt(t);

            float minDist = float.MaxValue;

            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int cx = cellX + i;
                    int cy = cellY + j;

                    // Seamlessly wrap the cell coordinates
                    int wrappedCx = (cx % (int)numCells + (int)numCells) % (int)numCells;
                    int wrappedCy = (cy % (int)numCells + (int)numCells) % (int)numCells;

                    // Deterministic pseudo-random generation based on wrapped cell coordinates
                    // Only spawn a crater in ~35% of the cells so they remain rare
                    float spawnChance = Frac(Mathf.Sin(wrappedCx * 73.156f + wrappedCy * 21.91f) * 43758.5453f);
                    if (spawnChance > 0.35f) continue;

                    float randomX = Frac(Mathf.Sin(wrappedCx * 12.989f + wrappedCy * 78.233f) * 43758.5453f);
                    float randomY = Frac(Mathf.Sin(wrappedCx * 39.346f + wrappedCy * 11.135f) * 43758.5453f);

                    float px = cx + randomX;
                    float py = cy + randomY;

                    float dist = Vector2.Distance(new Vector2(s, t), new Vector2(px, py));
                    if (dist < minDist)
                    {
                        minDist = dist;
                    }
                }
            }
            return minDist;
        }

        private float Frac(float v) { return v - Mathf.Floor(v); }

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
