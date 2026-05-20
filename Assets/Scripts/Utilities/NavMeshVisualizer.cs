using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Utilities
{
    public class NavMeshVisualizer : MonoBehaviour
    {
        [SerializeField] private Color navMeshColor = new Color(0f, 0.6f, 1f, 0.25f);
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        public static void Create(GameObject parent)
        {
            if (parent == null) return;
            
            // Avoid duplicates
            if (parent.GetComponent<NavMeshVisualizer>() != null)
            {
                parent.GetComponent<NavMeshVisualizer>().UpdateNavMesh();
                return;
            }

            parent.AddComponent<NavMeshVisualizer>();
        }

        private void Start()
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Find a built-in shader that supports transparency/color tinting
            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null) shader = Shader.Find("Transparent/Diffuse");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = navMeshColor;

            // Configure Standard shader if that was our fallback
            if (shader.name == "Standard")
            {
                mat.SetFloat("_Mode", 3f); // Transparent mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            meshRenderer.sharedMaterial = mat;
            
            // Turn off shadows and light probes to keep rendering clean
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            UpdateNavMesh();
        }

        public void UpdateNavMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0) return;

            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Runtime NavMesh Visualizer";
            }
            else
            {
                mesh.Clear();
            }

            mesh.vertices = triangulation.vertices;
            mesh.triangles = triangulation.indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.mesh = mesh;
            }
        }
    }
}
