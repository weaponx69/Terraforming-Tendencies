using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Shared planet height field: curated DEM when assigned, otherwise Perlin fallback.
    /// Spec: docs/specs/2026-11-21-planet-surface-dem-design.md
    /// </summary>
    public static class PlanetTerrainBuilder
    {
        public const string ChildName = "PlanetTerrain";

        public struct HeightParams
        {
            public float TerrainSize;
            public float MapWidthWorld;
            public float MapHeightWorld;
            public float Wavelength;
            public float HeightAmp;
            public float HeightMapMax;
            public float Ox;
            public float Oz;
            public float FlatRadius;
            public Vector3 LandingLocal;
            public Texture2D Heightmap;
            public float HeightAmplitude;
        }

        public static HeightParams MakeParams(PlanetConfig config, float cellSize, Vector3 landingLocal)
        {
            float mapW = Mathf.Max(16f, config.MapWidth * cellSize);
            float mapH = Mathf.Max(16f, config.MapHeight * cellSize);
            float terrainSize = Mathf.Max(mapW, mapH);
            int seed = config.TerrainSeed != 0 ? config.TerrainSeed : StableHash(config.name);
            float wavelength = Mathf.Clamp(config.NoiseScale > 0.1f ? config.NoiseScale : terrainSize / 6f,
                Mathf.Max(8f, terrainSize / 20f),
                terrainSize / 2f);
            float heightAmp = Mathf.Max(8f, config.HeightMultiplier);
            float demAmp = Mathf.Max(0.5f,
                config.HeightAmplitude > 0.1f ? config.HeightAmplitude : config.HeightMultiplier);
            float heightMapMax = Mathf.Max(demAmp * 3f, heightAmp * 3f, 40f);
            float flatR = config.LandingFlatRadius > 0.1f
                ? config.LandingFlatRadius
                : Mathf.Clamp(terrainSize * 0.06f, 10f, 28f);

            return new HeightParams
            {
                TerrainSize = terrainSize,
                MapWidthWorld = mapW,
                MapHeightWorld = mapH,
                Wavelength = wavelength,
                HeightAmp = heightAmp,
                HeightMapMax = heightMapMax,
                Ox = (seed % 997) * 0.137f,
                Oz = (seed % 991) * 0.173f,
                FlatRadius = flatR,
                LandingLocal = landingLocal,
                Heightmap = config.Heightmap,
                HeightAmplitude = demAmp
            };
        }

        /// <summary>World-space Y height with landing disk flattened.</summary>
        public static float SampleWorldY(float wx, float wz, in HeightParams p)
        {
            float y;
            if (p.Heightmap != null)
            {
                float u = Mathf.Clamp01(wx / Mathf.Max(0.001f, p.MapWidthWorld));
                float v = Mathf.Clamp01(wz / Mathf.Max(0.001f, p.MapHeightWorld));
                Color c = p.Heightmap.GetPixelBilinear(u, v);
                float h01 = c.r;
                y = (h01 - 0.5f) * p.HeightAmplitude;
            }
            else
            {
                float h = SampleHills(wx, wz, p.Ox, p.Oz, p.Wavelength, p.HeightAmp);
                y = h * 0.85f;
            }

            float dx = wx - p.LandingLocal.x;
            float dz = wz - p.LandingLocal.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < p.FlatRadius)
            {
                float t = dist / Mathf.Max(0.001f, p.FlatRadius);
                float w = t * t * (3f - 2f * t);
                y = Mathf.Lerp(0f, y, w);
            }
            return y;
        }

        public static Terrain BuildOrUpdate(
            Transform host,
            PlanetConfig config,
            float cellSize,
            Vector3 landingWorld)
        {
            if (host == null || config == null) return null;

            Vector3 landingLocal = host.InverseTransformPoint(landingWorld);
            var p = MakeParams(config, cellSize, landingLocal);
            int seed = config.TerrainSeed != 0 ? config.TerrainSeed : StableHash(config.name);

            int res = PickHeightmapResolution(p.TerrainSize);
            var data = new TerrainData
            {
                heightmapResolution = res,
                size = new Vector3(p.TerrainSize, p.HeightMapMax, p.TerrainSize)
            };

            float[,] heights = new float[res, res];
            float mn = 1f, mx = 0f;
            float yBase = -Mathf.Max(p.HeightAmplitude, p.HeightAmp) * 0.5f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)(res - 1);
                    float nz = z / (float)(res - 1);
                    float wx = nx * p.MapWidthWorld;
                    float wz = nz * p.MapHeightWorld;
                    float y = SampleWorldY(wx, wz, in p);
                    float norm = Mathf.Clamp01((y - yBase) / p.HeightMapMax);
                    heights[z, x] = norm;
                    if (norm < mn) mn = norm;
                    if (norm > mx) mx = norm;
                }
            }

            data.SetHeights(0, 0, heights);
            ApplyStarterLayers(data);

            Terrain terrain = EnsureTerrainComponent(host, data);
            terrain.transform.localPosition = new Vector3(0f, yBase, 0f);
            terrain.groupingID = seed;
            terrain.allowAutoConnect = false;
            terrain.drawHeightmap = false;
            terrain.drawInstanced = false;
            ApplyUrpTerrainMaterial(terrain);
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer >= 0)
                terrain.gameObject.layer = terrainLayer;
            if (terrain.TryGetComponent<TerrainCollider>(out var tc))
                tc.enabled = false;

            string src = p.Heightmap != null ? "DEM" : "PerlinFallback";
            Debug.Log(
                $"[PlanetTerrainBuilder] Terrain '{ChildName}' size={p.TerrainSize:0} heightMax={p.HeightMapMax:0.#} " +
                $"res={res} src={src} amp={p.HeightAmplitude:0.#} flatR={p.FlatRadius:0.#} " +
                $"normSpan={(mx - mn):0.###} seed={seed}");

            Physics.SyncTransforms();
            return terrain;
        }

        public static float SampleWorldHeight(Terrain terrain, Vector3 worldPos)
        {
            if (terrain == null) return worldPos.y;
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y;
        }

        public static void DestroyExisting(Transform host)
        {
            if (host == null) return;
            Transform child = host.Find(ChildName);
            if (child != null)
            {
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }

        static Terrain EnsureTerrainComponent(Transform host, TerrainData data)
        {
            Transform child = host.Find(ChildName);
            GameObject go;
            if (child == null)
            {
                go = Terrain.CreateTerrainGameObject(data);
                go.name = ChildName;
                go.transform.SetParent(host, false);
            }
            else
            {
                go = child.gameObject;
                var existing = go.GetComponent<Terrain>();
                if (existing != null)
                {
                    existing.terrainData = data;
                    var collider = go.GetComponent<TerrainCollider>();
                    if (collider != null) collider.terrainData = data;
                    return existing;
                }
            }

            var terrain = go.GetComponent<Terrain>();
            var col = go.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = data;
            return terrain;
        }

        static float SampleHills(float wx, float wz, float ox, float oz, float wavelength, float amp)
        {
            float f1 = 1f / Mathf.Max(1f, wavelength);
            float f2 = f1 * 2.3f;
            float f3 = f1 * 5.1f;
            float n =
                Mathf.PerlinNoise(ox + wx * f1, oz + wz * f1) * 1.0f +
                Mathf.PerlinNoise(ox + 20f + wx * f2, oz + 20f + wz * f2) * 0.55f +
                Mathf.PerlinNoise(ox + 40f + wx * f3, oz + 40f + wz * f3) * 0.3f;
            n /= 1.85f;
            float centered = n * 2f - 1f;
            return Mathf.Sign(centered) * Mathf.Pow(Mathf.Abs(centered), 0.85f) * amp;
        }

        static void ApplyUrpTerrainMaterial(Terrain terrain)
        {
            if (terrain == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                            ?? Shader.Find("Nature/Terrain/Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            terrain.materialTemplate = mat;
        }

        static void ApplyStarterLayers(TerrainData data)
        {
            data.alphamapResolution = 64;
            var rock = new TerrainLayer
            {
                diffuseTexture = MakeColorTex(new Color(0.45f, 0.38f, 0.32f)),
                tileSize = new Vector2(18f, 18f)
            };
            var dust = new TerrainLayer
            {
                diffuseTexture = MakeColorTex(new Color(0.62f, 0.48f, 0.36f)),
                tileSize = new Vector2(22f, 22f)
            };
            var ash = new TerrainLayer
            {
                diffuseTexture = MakeColorTex(new Color(0.35f, 0.36f, 0.4f)),
                tileSize = new Vector2(16f, 16f)
            };
            data.terrainLayers = new[] { dust, rock, ash };

            int aRes = data.alphamapResolution;
            float[,,] map = new float[aRes, aRes, 3];
            int hm = data.heightmapResolution;
            for (int y = 0; y < aRes; y++)
            {
                for (int x = 0; x < aRes; x++)
                {
                    int hx = Mathf.RoundToInt(x / (float)(aRes - 1) * (hm - 1));
                    int hz = Mathf.RoundToInt(y / (float)(aRes - 1) * (hm - 1));
                    float nh = data.GetHeights(hx, hz, 1, 1)[0, 0];
                    float low = 1f - nh;
                    float mid = 1f - Mathf.Abs(nh - 0.45f) * 2f;
                    float peak = Mathf.InverseLerp(0.55f, 0.95f, nh);
                    float sum = low + Mathf.Max(0f, mid) + peak + 0.001f;
                    map[y, x, 0] = low / sum;
                    map[y, x, 1] = Mathf.Max(0f, mid) / sum;
                    map[y, x, 2] = peak / sum;
                }
            }
            data.SetAlphamaps(0, 0, map);
        }

        static Texture2D MakeColorTex(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply(false, true);
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        static int PickHeightmapResolution(float terrainSize)
        {
            if (terrainSize >= 400f) return 513;
            if (terrainSize >= 200f) return 257;
            return 129;
        }

        static int StableHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 42;
            unchecked
            {
                int h = 23;
                for (int i = 0; i < s.Length; i++)
                    h = h * 31 + s[i];
                return h == 0 ? 42 : h;
            }
        }
    }
}
