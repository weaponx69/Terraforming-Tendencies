using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;

namespace GameDevTV.RTS.Utilities
{
    public class NavMeshVisualizer : MonoBehaviour
    {
        [SerializeField] private Color navMeshColor = new Color(0f, 0.6f, 1f, 0.08f);
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        public static void Create(GameObject parent)
        {
            CreateAll();
        }

        public static void CreateAll()
        {
            // Find all NavMeshSurface components in the scene
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude);
            if (surfaces == null || surfaces.Length == 0) return;

            // Store original active states
            var activeStates = new Dictionary<NavMeshSurface, bool>();
            foreach (var s in surfaces)
            {
                activeStates[s] = s.enabled;
            }

            // Triangulate each surface in isolation to separate the layers
            foreach (var targetSurface in surfaces)
            {
                // Disable all other surfaces
                foreach (var s in surfaces)
                {
                    s.enabled = (s == targetSurface);
                }

                // Retrieve triangulation for the active surface
                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

                Transform child = targetSurface.transform.Find("NavMeshVisualizer");
                if (triangulation.vertices != null && triangulation.vertices.Length > 0)
                {
                    GameObject visualizerObj;
                    if (child == null)
                    {
                        visualizerObj = new GameObject("NavMeshVisualizer");
                        visualizerObj.transform.parent = targetSurface.transform;
                        visualizerObj.transform.localPosition = Vector3.zero;
                        visualizerObj.transform.localRotation = Quaternion.identity;
                        visualizerObj.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        visualizerObj = child.gameObject;
                    }

                    var visualizer = visualizerObj.GetComponent<NavMeshVisualizer>();
                    if (visualizer == null)
                    {
                        visualizer = visualizerObj.AddComponent<NavMeshVisualizer>();
                    }

                    visualizer.SetTriangulation(triangulation);
                }
                else
                {
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            // Restore original active states
            foreach (var s in surfaces)
            {
                s.enabled = activeStates[s];
            }
        }

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            if (meshFilter != null && meshRenderer != null) return;

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null) shader = Shader.Find("Transparent/Diffuse");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

            Material mat = null;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.color = navMeshColor;

                if (shader.name == "Standard")
                {
                    mat.SetFloat("_Mode", 3f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
            }

            if (meshRenderer != null && mat != null)
            {
                meshRenderer.sharedMaterial = mat;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
        }

        public void SetTriangulation(NavMeshTriangulation triangulation)
        {
            InitializeComponents();

            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Runtime NavMesh Visualizer";
            }
            else
            {
                mesh.Clear();
            }

            // Convert world space vertices returned by Unity to the local coordinate system of the parent NavMeshSurface
            Vector3[] localVertices = new Vector3[triangulation.vertices.Length];
            for (int i = 0; i < triangulation.vertices.Length; i++)
            {
                localVertices[i] = transform.InverseTransformPoint(triangulation.vertices[i]);
            }

            mesh.vertices = localVertices;
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
