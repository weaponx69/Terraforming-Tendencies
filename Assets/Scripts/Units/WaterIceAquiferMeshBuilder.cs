using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Mars ice-drill rig for the Water Ice Aquifer — sled, derrick, kelly, melt tank.
    /// </summary>
    public static class WaterIceAquiferMeshBuilder
    {
        static Mesh cached;

        public static Mesh SharedMesh
        {
            get
            {
                if (cached == null)
                {
                    cached = Create();
                    cached.name = "WaterIceAquifer";
                }
                return cached;
            }
        }

        /// <summary>Swap the placeholder sphere for the drill mesh.</summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null) continue;
                string n = filter.gameObject.name;
                if (n.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Aquifer Mesh", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                filter.sharedMesh = SharedMesh;
                filter.transform.localPosition = Vector3.zero;
                filter.transform.localRotation = Quaternion.identity;
                filter.transform.localScale = Vector3.one;
                filter.gameObject.name = "Aquifer Mesh";
            }
        }

        public static Mesh Create()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            // Skid undercarriage + tie-down points.
            AddBox(verts, normals, tris, new Vector3(0f, 0.05f, 0f), new Vector3(2.85f, 0.1f, 1.65f));
            AddBox(verts, normals, tris, new Vector3(-1.15f, 0.12f, 0.95f), new Vector3(0.55f, 0.06f, 0.18f));
            AddBox(verts, normals, tris, new Vector3(1.15f, 0.12f, 0.95f), new Vector3(0.55f, 0.06f, 0.18f));
            AddBox(verts, normals, tris, new Vector3(-1.15f, 0.12f, -0.95f), new Vector3(0.55f, 0.06f, 0.18f));
            AddBox(verts, normals, tris, new Vector3(1.15f, 0.12f, -0.95f), new Vector3(0.55f, 0.06f, 0.18f));

            // Insulated control cabin (left).
            AddBox(verts, normals, tris, new Vector3(-1.05f, 0.55f, 0f), new Vector3(0.75f, 0.85f, 1.15f));
            AddBox(verts, normals, tris, new Vector3(-1.35f, 0.62f, 0.35f), new Vector3(0.08f, 0.35f, 0.4f));
            AddBox(verts, normals, tris, new Vector3(-1.35f, 0.62f, -0.35f), new Vector3(0.08f, 0.35f, 0.4f));

            // Power / heat unit beside cabin.
            AddBox(verts, normals, tris, new Vector3(-0.55f, 0.42f, -0.72f), new Vector3(0.55f, 0.55f, 0.45f));
            AddCylinder(verts, normals, tris, new Vector3(-0.55f, 0.75f, -0.72f), 0.11f, 0.35f, 8, Vector3.up);

            // Vertical drill mast (center).
            AddBox(verts, normals, tris, new Vector3(0.05f, 1.65f, 0f), new Vector3(0.28f, 2.95f, 0.28f));
            AddBox(verts, normals, tris, new Vector3(0.38f, 1.65f, 0f), new Vector3(0.14f, 2.95f, 0.14f));

            // A-frame crown + pulley housing.
            Vector3 crown = new Vector3(0.05f, 3.05f, 0f);
            AddBox(verts, normals, tris, crown, new Vector3(1.05f, 0.14f, 0.75f));
            AddBeam(verts, normals, tris, new Vector3(-0.95f, 0.15f, 0.95f), crown + new Vector3(-0.15f, 0f, 0f), 0.09f);
            AddBeam(verts, normals, tris, new Vector3(0.95f, 0.15f, 0.95f), crown + new Vector3(0.15f, 0f, 0f), 0.09f);
            AddBeam(verts, normals, tris, new Vector3(-0.95f, 0.15f, -0.95f), crown, 0.09f);
            AddBeam(verts, normals, tris, new Vector3(0.95f, 0.15f, -0.95f), crown, 0.09f);
            AddBeam(verts, normals, tris, new Vector3(-0.55f, 1.4f, 0f), new Vector3(0.6f, 1.4f, 0f), 0.06f);

            // Kelly / drill string into the regolith.
            AddCylinder(verts, normals, tris, new Vector3(0.05f, 0.55f, 0f), 0.07f, 1.05f, 10, Vector3.up);
            AddCylinder(verts, normals, tris, new Vector3(0.05f, 1.25f, 0f), 0.05f, 1.35f, 8, Vector3.up);

            // Rotary head block under crown.
            AddBox(verts, normals, tris, new Vector3(0.05f, 2.75f, 0f), new Vector3(0.42f, 0.22f, 0.42f));

            // Insulated melt-water tank (right) with banding.
            AddCylinder(verts, normals, tris, new Vector3(1.15f, 0.62f, 0f), 0.48f, 1.15f, 14, Vector3.right);
            AddCylinder(verts, normals, tris, new Vector3(1.15f, 1.15f, 0f), 0.51f, 0.08f, 14, Vector3.right);
            AddCylinder(verts, normals, tris, new Vector3(1.15f, 0.62f, 0f), 0.51f, 0.08f, 14, Vector3.right);

            // Hose bundle tank → heat exchanger.
            AddCylinder(verts, normals, tris, new Vector3(0.65f, 0.55f, 0.35f), 0.06f, 0.9f, 6, new Vector3(0.6f, 0.15f, 0f).normalized);
            AddCylinder(verts, normals, tris, new Vector3(0.55f, 0.45f, -0.25f), 0.05f, 0.75f, 6, new Vector3(0.2f, 0.1f, -0.4f).normalized);

            // Steam bleed / vent stack at crown.
            AddCylinder(verts, normals, tris, new Vector3(-0.25f, 3.25f, 0.4f), 0.05f, 0.55f, 6, Vector3.up);

            var mesh = new Mesh { name = "WaterIceAquifer" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddBox(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] faces =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };
            foreach (var n in faces)
            {
                Vector3 tangent = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 bitangent = Vector3.Cross(n, tangent).normalized;
                tangent = Vector3.Cross(bitangent, n).normalized;
                Vector3 faceCenter = center + Vector3.Scale(n, h);
                Vector3 tu = Vector3.Scale(tangent, h);
                Vector3 tv = Vector3.Scale(bitangent, h);
                int i = verts.Count;
                verts.Add(faceCenter - tu - tv);
                verts.Add(faceCenter + tu - tv);
                verts.Add(faceCenter + tu + tv);
                verts.Add(faceCenter - tu + tv);
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
        }

        static void AddCylinder(
            List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 center, float radius, float height, int segments, Vector3 axis)
        {
            axis.Normalize();
            Quaternion align = Quaternion.FromToRotation(Vector3.up, axis);
            float half = height * 0.5f;
            int ring = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = (Mathf.PI * 2f * i) / segments;
                Vector3 radial = align * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Vector3 n = radial.normalized;
                verts.Add(center + axis * half + radial);
                verts.Add(center - axis * half + radial);
                normals.Add(n);
                normals.Add(n);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = ring + i * 2;
                int i1 = ring + ((i + 1) % segments) * 2;
                tris.Add(i0); tris.Add(i1); tris.Add(i0 + 1);
                tris.Add(i1); tris.Add(i1 + 1); tris.Add(i0 + 1);
            }

            AddCap(verts, normals, tris, center + axis * half, axis, radius, segments, align);
            AddCap(verts, normals, tris, center - axis * half, -axis, radius, segments, align);
        }

        static void AddCap(
            List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 center, Vector3 normal, float radius, int segments, Quaternion align)
        {
            int hub = verts.Count;
            verts.Add(center);
            normals.Add(normal);
            int rim = verts.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = (Mathf.PI * 2f * i) / segments;
                Vector3 radial = align * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                verts.Add(center + radial);
                normals.Add(normal);
            }
            bool flip = Vector3.Dot(normal, align * Vector3.up) < 0f;
            for (int i = 0; i < segments; i++)
            {
                int a = rim + i;
                int b = rim + (i + 1) % segments;
                if (flip)
                {
                    tris.Add(hub); tris.Add(b); tris.Add(a);
                }
                else
                {
                    tris.Add(hub); tris.Add(a); tris.Add(b);
                }
            }
        }

        static void AddBeam(
            List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 a, Vector3 b, float thickness)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f) return;
            AddBox(verts, normals, tris, (a + b) * 0.5f, new Vector3(thickness, len, thickness));
            int start = verts.Count - 24;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
            Vector3 mid = (a + b) * 0.5f;
            for (int i = start; i < verts.Count; i++)
                verts[i] = mid + rot * (verts[i] - mid);
            for (int i = start; i < normals.Count; i++)
                normals[i] = rot * normals[i];
        }
    }
}