using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>Procedural hill-top windmill — tower mesh plus a spinning three-blade rotor.</summary>
    public static class WindTurbineMeshBuilder
    {
        const string RotorName = "Wind Rotor";
        static Mesh towerMesh;
        static Mesh bladeMesh;

        public static bool IsWindBuilding(BuildingSO so)
        {
            if (so == null || string.IsNullOrEmpty(so.Name)) return false;
            string n = so.Name;
            if (n.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return n.IndexOf("Wind", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Windmill", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Apply(GameObject root)
        {
            if (root == null) return;

            MeshFilter targetFilter = null;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null) continue;
                string n = filter.gameObject.name;
                if (n.IndexOf("Building Mesh", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetFilter = filter;
                    break;
                }
            }
            if (targetFilter == null)
            {
                var go = new GameObject("Building Mesh");
                go.transform.SetParent(root.transform, false);
                targetFilter = go.AddComponent<MeshFilter>();
                if (go.GetComponent<MeshRenderer>() == null)
                    go.AddComponent<MeshRenderer>();
            }

            targetFilter.sharedMesh = TowerMesh;
            targetFilter.gameObject.name = "Building Mesh";
            targetFilter.transform.localPosition = Vector3.zero;
            targetFilter.transform.localRotation = Quaternion.identity;
            targetFilter.transform.localScale = Vector3.one;

            var renderer = targetFilter.GetComponent<MeshRenderer>();
            if (renderer != null && (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0))
            {
                var refRenderer = root.GetComponentInChildren<MeshRenderer>();
                if (refRenderer != null && refRenderer.sharedMaterials != null && refRenderer.sharedMaterials.Length > 0)
                    renderer.sharedMaterials = refRenderer.sharedMaterials;
            }

            EnsureRotor(root.transform, targetFilter.transform);
        }

        static Mesh TowerMesh
        {
            get
            {
                if (towerMesh != null) return towerMesh;
                var built = ColonyBuildingMeshLibrary.Build("Windmill");
                towerMesh = built != null ? built : CreateTowerMesh();
                towerMesh.name = "WindmillTower";
                return towerMesh;
            }
        }

        static Mesh BladeMesh
        {
            get
            {
                if (bladeMesh != null) return bladeMesh;
                bladeMesh = CreateBladeMesh();
                bladeMesh.name = "WindmillBlade";
                return bladeMesh;
            }
        }

        static void EnsureRotor(Transform root, Transform meshTransform)
        {
            Transform rotor = root.Find(RotorName);
            if (rotor == null)
            {
                var rotorGo = new GameObject(RotorName);
                rotor = rotorGo.transform;
                rotor.SetParent(root, false);
            }

            rotor.localPosition = new Vector3(0f, 3.35f, 0f);
            rotor.localRotation = Quaternion.identity;
            rotor.localScale = Vector3.one;

            if (rotor.GetComponent<WindTurbineRotor>() == null)
                rotor.gameObject.AddComponent<WindTurbineRotor>();

            for (int i = 0; i < 3; i++)
            {
                string bladeName = $"Blade_{i}";
                Transform blade = rotor.Find(bladeName);
                if (blade == null)
                {
                    var bladeGo = new GameObject(bladeName);
                    blade = bladeGo.transform;
                    blade.SetParent(rotor, false);
                    var filter = bladeGo.AddComponent<MeshFilter>();
                    filter.sharedMesh = BladeMesh;
                    var rend = bladeGo.AddComponent<MeshRenderer>();
                    var towerRend = meshTransform.GetComponent<MeshRenderer>();
                    if (towerRend != null)
                        rend.sharedMaterials = towerRend.sharedMaterials;
                }

                blade.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                blade.localPosition = Vector3.zero;
            }
        }

        static Mesh CreateTowerMesh()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            AddBox(verts, normals, tris, new Vector3(0f, 0.08f, 0f), new Vector3(2.8f, 0.14f, 2.4f));
            AddBox(verts, normals, tris, new Vector3(0.55f, 0.22f, 0.4f), new Vector3(1.6f, 0.28f, 1.1f));
            AddCylinder(verts, normals, tris, new Vector3(0f, 1.1f, 0f), 0.16f, 2.0f, 10, Vector3.up);
            AddBox(verts, normals, tris, new Vector3(0f, 2.15f, 0f), new Vector3(0.45f, 0.35f, 0.55f));
            AddCylinder(verts, normals, tris, new Vector3(0f, 2.55f, 0f), 0.22f, 0.35f, 10, Vector3.forward);

            var mesh = new Mesh { name = "WindmillTowerFallback" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateBladeMesh()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            AddBox(verts, normals, tris, new Vector3(0f, 0f, 1.05f), new Vector3(0.12f, 0.05f, 2.0f));
            var mesh = new Mesh { name = "WindmillBlade" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddBox(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                center + new Vector3(-h.x, -h.y, -h.z),
                center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z),
                center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z),
                center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z),
                center + new Vector3(-h.x, h.y, h.z),
            };
            int[] faceIndices = { 0, 1, 2, 0, 2, 3, 5, 4, 7, 5, 7, 6, 4, 0, 3, 4, 3, 7, 1, 5, 6, 1, 6, 2, 3, 2, 6, 3, 6, 7, 4, 5, 1, 4, 1, 0 };
            Vector3[] faceNormals = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            int face = 0;
            for (int f = 0; f < 6; f++)
            {
                int start = verts.Count;
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(corners[faceIndices[f * 4 + i]]);
                    normals.Add(faceNormals[f]);
                }
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                face++;
            }
        }

        static void AddCylinder(List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 center, float radius, float height, int segments, Vector3 axis)
        {
            axis = axis.normalized;
            Vector3 up = Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Cross(up, axis).normalized;
            Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
            float halfH = height * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 o0 = tangent * Mathf.Cos(a0) * radius + bitangent * Mathf.Sin(a0) * radius;
                Vector3 o1 = tangent * Mathf.Cos(a1) * radius + bitangent * Mathf.Sin(a1) * radius;
                int start = verts.Count;
                verts.Add(center - axis * halfH + o0);
                verts.Add(center - axis * halfH + o1);
                verts.Add(center + axis * halfH + o1);
                verts.Add(center + axis * halfH + o0);
                Vector3 n = (o0 + o1).normalized;
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }
        }
    }

}