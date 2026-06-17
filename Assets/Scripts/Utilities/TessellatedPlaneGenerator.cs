using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    [RequireComponent(typeof(MeshFilter))]
    public class TessellatedPlaneGenerator : MonoBehaviour
    {
        [Tooltip("Number of subdivisions along the X and Z axes. Higher numbers bend smoother but cost more performance.")]
        [Range(1, 50)]
        public int subdivisions = 10;

        [Tooltip("The size of the plane. A standard Unity Quad is 1x1.")]
        public Vector2 size = new Vector2(1f, 1f);

        [Tooltip("If true, the plane faces UP (like a floor). If false, it faces FORWARD (like a standard Quad).")]
        public bool faceUpwards = false;

        private void Awake()
        {
            GenerateMesh();
        }

        [ContextMenu("Generate Mesh Now")]
        private void GenerateMesh()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) return;

            Mesh mesh = new Mesh();
            mesh.name = "Tessellated Plane";

            int segmentsX = subdivisions + 1;
            int segmentsZ = subdivisions + 1;

            Vector3[] vertices = new Vector3[(segmentsX + 1) * (segmentsZ + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[segmentsX * segmentsZ * 6];

            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            int i = 0;
            for (int z = 0; z <= segmentsZ; z++)
            {
                float zPercent = (float)z / segmentsZ;
                float zPos = Mathf.Lerp(-halfHeight, halfHeight, zPercent);

                for (int x = 0; x <= segmentsX; x++)
                {
                    float xPercent = (float)x / segmentsX;
                    float xPos = Mathf.Lerp(-halfWidth, halfWidth, xPercent);

                    if (faceUpwards)
                    {
                        vertices[i] = new Vector3(xPos, 0f, zPos);
                        normals[i] = Vector3.up;
                    }
                    else
                    {
                        vertices[i] = new Vector3(xPos, zPos, 0f);
                        normals[i] = Vector3.back; // Standard Quad normal
                    }
                    
                    uvs[i] = new Vector2(xPercent, zPercent);
                    i++;
                }
            }

            int ti = 0;
            int vi = 0;
            for (int z = 0; z < segmentsZ; z++)
            {
                for (int x = 0; x < segmentsX; x++)
                {
                    triangles[ti] = vi;
                    triangles[ti + 1] = vi + segmentsX + 1;
                    triangles[ti + 2] = vi + 1;

                    triangles[ti + 3] = vi + 1;
                    triangles[ti + 4] = vi + segmentsX + 1;
                    triangles[ti + 5] = vi + segmentsX + 2;

                    ti += 6;
                    vi++;
                }
                vi++; // Skip edge vertex
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;
            
            // To ensure bounds are large enough if curved world shader bends it out of normal bounds
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x * 2, size.x * 2, size.y * 2));

            mf.mesh = mesh;
        }
    }
}
