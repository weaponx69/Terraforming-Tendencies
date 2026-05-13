using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class PlanetGenerator : MonoBehaviour
    {
        public static PlanetGenerator Instance { get; private set; }

        public PlanetConfig Config;
        public float CellSize = 1f;

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

            // Note: In a full project, you would call GetComponent<NavMeshSurface>().BuildNavMesh() here
            // if you have the Unity.AI.Navigation package installed.
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
