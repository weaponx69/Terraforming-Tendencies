using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Environment.Procedural
{
    public static class ProceduralMeshUtils
    {
        public static Mesh GenerateIcoSphere(int subdivisions, float radius)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural IcoSphere";

            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;

            List<Vector3> vertices = new List<Vector3>()
            {
                new Vector3(-1,  t,  0).normalized * radius,
                new Vector3( 1,  t,  0).normalized * radius,
                new Vector3(-1, -t,  0).normalized * radius,
                new Vector3( 1, -t,  0).normalized * radius,
                new Vector3( 0, -1,  t).normalized * radius,
                new Vector3( 0,  1,  t).normalized * radius,
                new Vector3( 0, -1, -t).normalized * radius,
                new Vector3( 0,  1, -t).normalized * radius,
                new Vector3( t,  0, -1).normalized * radius,
                new Vector3( t,  0,  1).normalized * radius,
                new Vector3(-t,  0, -1).normalized * radius,
                new Vector3(-t,  0,  1).normalized * radius
            };

            List<int> triangles = new List<int>()
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (int i = 0; i < subdivisions; i++)
            {
                List<int> newTriangles = new List<int>();
                Dictionary<long, int> midpointCache = new Dictionary<long, int>();

                for (int j = 0; j < triangles.Count; j += 3)
                {
                    int v1 = triangles[j];
                    int v2 = triangles[j + 1];
                    int v3 = triangles[j + 2];

                    int a = GetMidpointIndex(vertices, midpointCache, v1, v2, radius);
                    int b = GetMidpointIndex(vertices, midpointCache, v2, v3, radius);
                    int c = GetMidpointIndex(vertices, midpointCache, v3, v1, radius);

                    newTriangles.AddRange(new int[] { v1, a, c });
                    newTriangles.AddRange(new int[] { v2, b, a });
                    newTriangles.AddRange(new int[] { v3, c, b });
                    newTriangles.AddRange(new int[] { a, b, c });
                }

                triangles = newTriangles;
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }

        private static int GetMidpointIndex(List<Vector3> vertices, Dictionary<long, int> cache, int i1, int i2, float radius)
        {
            long smallerIndex = Mathf.Min(i1, i2);
            long greaterIndex = Mathf.Max(i1, i2);
            long key = (smallerIndex << 32) + greaterIndex;

            if (cache.TryGetValue(key, out int ret))
            {
                return ret;
            }

            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];
            Vector3 middle = ((p1 + p2) / 2.0f).normalized * radius;

            vertices.Add(middle);
            int index = vertices.Count - 1;
            cache.Add(key, index);

            return index;
        }

        public static void ApplyNoise(Mesh mesh, float scale, float strength, int seed, bool flattenBottom = false)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            System.Random prng = new System.Random(seed);
            float offsetX = prng.Next(-10000, 10000);
            float offsetY = prng.Next(-10000, 10000);
            float offsetZ = prng.Next(-10000, 10000);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                
                float x = v.x * scale + offsetX;
                float y = v.y * scale + offsetY;
                float z = v.z * scale + offsetZ;

                float noiseValue = Noise3D(x, y, z) * 2f - 1f; // Map to -1 to 1

                v += normals[i] * noiseValue * strength;

                if (flattenBottom && v.y < 0)
                {
                    v.y = 0;
                }

                vertices[i] = v;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static float Noise3D(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float yz = Mathf.PerlinNoise(y, z);
            float zx = Mathf.PerlinNoise(z, x);
            float yx = Mathf.PerlinNoise(y, x);
            float zy = Mathf.PerlinNoise(z, y);
            float xz = Mathf.PerlinNoise(x, z);

            return (xy + yz + zx + yx + zy + xz) / 6f;
        }

        public static Mesh GenerateCrystal(int sides, float height, float radius)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural Crystal";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Bottom center
            vertices.Add(new Vector3(0, 0, 0));
            // Top point (spire)
            vertices.Add(new Vector3(0, height, 0));

            int bottomCenterIndex = 0;
            int topCenterIndex = 1;

            float angleStep = 360f / sides;

            for (int i = 0; i < sides; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                // Add a middle vertex slightly above 0 for the base of the crystal
                float yOffset = Random.Range(height * 0.1f, height * 0.3f);
                
                vertices.Add(new Vector3(x, yOffset, z));
            }

            for (int i = 0; i < sides; i++)
            {
                int current = 2 + i;
                int next = 2 + ((i + 1) % sides);

                // Bottom triangle
                triangles.Add(bottomCenterIndex);
                triangles.Add(next);
                triangles.Add(current);

                // Top triangle (spire)
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(topCenterIndex);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            // To get sharp edges on a crystal, we need to split vertices
            return SplitVerticesForFlatShading(mesh);
        }

        private static Mesh SplitVerticesForFlatShading(Mesh mesh)
        {
            Vector3[] oldVerts = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3[] newVerts = new Vector3[triangles.Length];

            for (int i = 0; i < triangles.Length; i++)
            {
                newVerts[i] = oldVerts[triangles[i]];
                triangles[i] = i;
            }

            Mesh flatMesh = new Mesh();
            flatMesh.name = mesh.name;
            flatMesh.vertices = newVerts;
            flatMesh.triangles = triangles;
            flatMesh.RecalculateNormals();
            flatMesh.RecalculateBounds();

            return flatMesh;
        }
    }
}
