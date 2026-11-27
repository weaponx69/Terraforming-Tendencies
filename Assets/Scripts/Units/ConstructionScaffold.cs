using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Orange pad scaffold for every building under construction.
    /// One shared mesh + one shared material for all scaffolds (batch-friendly).
    /// </summary>
    public static class ConstructionScaffold
    {
        public const string ObjectName = "BuildScaffold";

        const float Reach = 1.85f;
        const float Height = 3.15f;
        const float PoleThick = 0.07f;
        const float RailThick = 0.06f;

        static Mesh sharedMesh;
        static Material sharedMat;

        public static GameObject Ensure(Transform building)
        {
            if (building == null) return null;
            Transform existing = building.Find(ObjectName);
            if (existing != null) return existing.gameObject;

            var scaffold = new GameObject(ObjectName);
            scaffold.transform.SetParent(building, false);
            scaffold.transform.localPosition = Vector3.zero;

            var meshGo = new GameObject("ScaffoldMesh");
            meshGo.transform.SetParent(scaffold.transform, false);
            var mf = meshGo.AddComponent<MeshFilter>();
            mf.sharedMesh = SharedMesh();
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SharedMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return scaffold;
        }

        public static void Clear(Transform building)
        {
            if (building == null) return;
            Transform existing = building.Find(ObjectName);
            if (existing != null)
                Object.Destroy(existing.gameObject);
        }

        static Mesh SharedMesh()
        {
            if (sharedMesh != null) return sharedMesh;

            var boxes = new List<Bounds>();
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -Reach : Reach;
                float z = (i & 2) == 0 ? -Reach : Reach;
                boxes.Add(CenteredBox(new Vector3(x, Height * 0.5f, z),
                    new Vector3(PoleThick, Height, PoleThick)));
            }

            AddRailBoxes(boxes, Height * 0.42f);
            AddRailBoxes(boxes, Height * 0.78f);
            boxes.Add(CenteredBox(new Vector3(0f, Height * 0.42f, 0f),
                new Vector3(Reach * 2f, RailThick, Reach * 2f)));

            sharedMesh = MergeBoxes(boxes);
            sharedMesh.name = "SharedBuildScaffold";
            return sharedMesh;
        }

        static void AddRailBoxes(List<Bounds> boxes, float y)
        {
            boxes.Add(CenteredBox(new Vector3(0f, y, Reach), new Vector3(Reach * 2f, RailThick, RailThick)));
            boxes.Add(CenteredBox(new Vector3(0f, y, -Reach), new Vector3(Reach * 2f, RailThick, RailThick)));
            boxes.Add(CenteredBox(new Vector3(Reach, y, 0f), new Vector3(RailThick, RailThick, Reach * 2f)));
            boxes.Add(CenteredBox(new Vector3(-Reach, y, 0f), new Vector3(RailThick, RailThick, Reach * 2f)));
        }

        static Bounds CenteredBox(Vector3 center, Vector3 size)
        {
            return new Bounds(center, size);
        }

        static Mesh MergeBoxes(List<Bounds> boxes)
        {
            var verts = new List<Vector3>(boxes.Count * 8);
            var tris = new List<int>(boxes.Count * 36);
            for (int b = 0; b < boxes.Count; b++)
            {
                int baseV = verts.Count;
                AppendBox(verts, tris, boxes[b], baseV);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AppendBox(List<Vector3> verts, List<int> tris, Bounds b, int baseV)
        {
            Vector3 c = b.center;
            Vector3 e = b.extents;
            verts.Add(c + new Vector3(-e.x, -e.y, -e.z));
            verts.Add(c + new Vector3(e.x, -e.y, -e.z));
            verts.Add(c + new Vector3(e.x, -e.y, e.z));
            verts.Add(c + new Vector3(-e.x, -e.y, e.z));
            verts.Add(c + new Vector3(-e.x, e.y, -e.z));
            verts.Add(c + new Vector3(e.x, e.y, -e.z));
            verts.Add(c + new Vector3(e.x, e.y, e.z));
            verts.Add(c + new Vector3(-e.x, e.y, e.z));

            int[] faces =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                1, 2, 6, 1, 6, 5,
                3, 0, 4, 3, 4, 7
            };
            foreach (int f in faces)
                tris.Add(baseV + f);
        }

        static Material SharedMaterial()
        {
            if (sharedMat != null) return sharedMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            sharedMat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            var c = new Color(0.92f, 0.55f, 0.12f);
            if (sharedMat.HasProperty("_BaseColor")) sharedMat.SetColor("_BaseColor", c);
            if (sharedMat.HasProperty("_Color")) sharedMat.SetColor("_Color", c);
            sharedMat.color = c;
            sharedMat.enableInstancing = true;
            return sharedMat;
        }
    }
}