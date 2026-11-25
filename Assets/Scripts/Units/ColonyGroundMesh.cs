using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Deep-core laser, basalt strip mine, and Command Post meshes.
    /// Each one sits on a desert-colored apron so the edge meets the planet ground.
    /// </summary>
    public static class ColonyGroundMesh
    {
        const int Dirt = 0;
        const int Pit = 1;
        const int Metal = 2;
        const int Glow = 3;

        static readonly Color Desert = new(0.74f, 0.40f, 0.16f);
        static readonly Color Soil = new(0.22f, 0.11f, 0.06f);
        static readonly Color Hull = new(0.72f, 0.68f, 0.62f);
        static readonly Color Steel = new(0.28f, 0.30f, 0.32f);
        static readonly Color Accent = new(0.78f, 0.38f, 0.12f);
        static readonly Color Glass = new(0.12f, 0.16f, 0.20f);
        static readonly Color Beam = new(0.55f, 0.92f, 1f);

        const int MeshVersion = 4;
        static int builtVersion;
        static Mesh laserMesh;
        static Mesh stripMesh;
        static Mesh postMesh;
        static Material dirtMat;
        static Material pitMat;
        static Material metalMat;
        static Material hullMat;
        static Material accentMat;
        static Material glassMat;
        static Material beamMat;

        public static void Apply(GameObject root)
        {
            if (root == null) return;
            // Baked prefab meshes stay. The runtime swap was leaving the cylinder
            // on the asset and only covering it after the building woke up.
            foreach (var existing in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (existing != null
                    && existing.gameObject.name.IndexOf("Building Mesh", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            }
            string name = root.name ?? string.Empty;
            var building = root.GetComponent<BaseBuilding>();
            if (building != null && building.BuildingSO != null)
                name = building.BuildingSO.Name + " " + name;

            if (name.IndexOf("Deep-Core", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Deep Core", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Swap(root, LaserMesh(), DirtMat(), PitMat(), MetalMat(), BeamMat());
            }
            else if (name.IndexOf("Basalt", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Swap(root, StripMesh(), DirtMat(), PitMat(), MetalMat(), AccentMat());
            }
            else if (name.IndexOf("Command Post", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Swap(root, PostMesh(), DirtMat(), HullMat(), AccentMat(), GlassMat());
            }
        }

        static void EnsureFresh()
        {
            if (builtVersion == MeshVersion) return;
            builtVersion = MeshVersion;
            laserMesh = null;
            stripMesh = null;
            postMesh = null;
        }

        static Mesh LaserMesh()
        {
            EnsureFresh();
            if (laserMesh != null) return laserMesh;
            var d = new Draft();
            d.Disc(Vector3.zero, 2.3f, 0.04f, 24, Dirt);
            d.Slope(0.04f, 2.22f, 0.36f, 1.72f, 24, Dirt);
            d.Ring(0.36f, 1.35f, 1.74f, 24, Dirt);
            d.Slope(0.36f, 1.32f, 0.2f, 0.95f, 22, Pit);
            d.Ring(0.2f, 0.58f, 0.95f, 18, Pit);
            d.Disc(new Vector3(0f, 0.1f, 0f), 0.58f, 0f, 14, Pit);
            Vector3 crown = new Vector3(0f, 1.15f, 0f);
            d.Beam(new Vector3(0.85f, 0.36f, 0.85f), crown, 0.045f, Metal);
            d.Beam(new Vector3(-0.85f, 0.36f, 0.85f), crown, 0.045f, Metal);
            d.Beam(new Vector3(0.85f, 0.36f, -0.85f), crown, 0.045f, Metal);
            d.Beam(new Vector3(-0.85f, 0.36f, -0.85f), crown, 0.045f, Metal);
            d.Box(new Vector3(0f, 1.22f, 0f), new Vector3(0.14f, 0.08f, 0.14f), Glow);
            d.Cylinder(new Vector3(0f, 0.62f, 0f), 0.025f, 0.95f, 6, Vector3.up, Glow);
            laserMesh = d.ToMesh("DeepCoreMiningLaser");
            return laserMesh;
        }

        static Mesh StripMesh()
        {
            EnsureFresh();
            if (stripMesh != null) return stripMesh;
            var d = new Draft();
            d.Disc(Vector3.zero, 2.3f, 0.04f, 24, Dirt);
            d.Slope(0.04f, 2.22f, 0.4f, 1.8f, 24, Dirt);
            d.Ring(0.4f, 1.4f, 1.82f, 24, Dirt);
            d.Slope(0.4f, 1.36f, 0.22f, 0.95f, 22, Pit);
            d.Ring(0.22f, 0.55f, 0.95f, 18, Pit);
            d.Disc(new Vector3(0f, 0.1f, 0f), 0.55f, 0f, 14, Pit);
            d.Box(new Vector3(0f, 0.2f, 0.05f), new Vector3(0.4f, 0.14f, 0.55f), Metal);
            stripMesh = d.ToMesh("BasaltStripMine");
            return stripMesh;
        }

        /// <summary>The low habitat shown on the Command Post. A fresh mesh so it can be saved on the prefab.</summary>
        public static Mesh CreateCommandPostMesh()
        {
            postMesh = null;
            return PostMesh();
        }

        static Mesh PostMesh()
        {
            EnsureFresh();
            if (postMesh != null) return postMesh;
            var d = new Draft();
            d.Disc(Vector3.zero, 2.15f, 0.05f, 22, Dirt);
            d.Slope(0.05f, 2.1f, 0.16f, 1.45f, 18, Dirt);
            d.Disc(Vector3.zero, 1.45f, 0.16f, 18, Dirt);
            d.Box(new Vector3(1.15f, 0.08f, 0.4f), new Vector3(0.7f, 0.08f, 0.45f), Dirt);
            d.Box(new Vector3(-0.9f, 0.07f, -0.7f), new Vector3(0.55f, 0.07f, 0.4f), Dirt);
            d.Cylinder(new Vector3(0f, 0.72f, 0f), 0.82f, 1.15f, 16, Vector3.up, Pit);
            d.Cylinder(new Vector3(0f, 1.42f, 0f), 0.52f, 0.38f, 14, Vector3.up, Pit);
            d.Cylinder(new Vector3(0.95f, 0.48f, 0.15f), 0.32f, 0.55f, 10, Vector3.up, Pit);
            d.Box(new Vector3(0f, 0.48f, 0.95f), new Vector3(0.42f, 0.55f, 0.28f), Pit);
            d.Cylinder(new Vector3(0f, 0.95f, 0f), 0.84f, 0.07f, 16, Vector3.up, Metal);
            d.Cylinder(new Vector3(-0.15f, 1.78f, 0.15f), 0.28f, 0.04f, 10, new Vector3(0.35f, 1f, 0.15f), Pit);
            d.Cylinder(new Vector3(-0.55f, 1.55f, -0.2f), 0.025f, 0.7f, 5, Vector3.up, Pit);
            d.Cylinder(new Vector3(0.15f, 1.62f, -0.35f), 0.02f, 0.85f, 5, Vector3.up, Pit);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                d.Box(new Vector3(Mathf.Cos(a) * 0.78f, 0.78f, Mathf.Sin(a) * 0.78f), new Vector3(0.22f, 0.16f, 0.04f), Glow, a);
            }
            postMesh = d.ToMesh("CommandPost");
            return postMesh;
        }

        static void Swap(GameObject root, Mesh mesh, Material a, Material b, Material c, Material d)
        {
            var renderer = FindVisual(root);
            if (renderer == null) return;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) return;
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { a, b, c, d };
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.gameObject.SetActive(true);
            renderer.gameObject.name = "Site Mesh";
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.gameObject.name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    canvas.gameObject.SetActive(false);
            }
            foreach (var other in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (other == null || other == renderer) continue;
                string n = other.gameObject.name.ToLowerInvariant();
                if (n.Contains("label") || n.Contains("canvas") || n.Contains("vision")
                    || n.Contains("selection") || n.Contains("decal") || n.Contains("indicator"))
                    continue;
                other.gameObject.SetActive(false);
            }
        }

        static MeshRenderer FindVisual(GameObject root)
        {
            MeshRenderer placeholder = null;
            MeshRenderer building = null;
            MeshRenderer fallback = null;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (n.Contains("label") || n.Contains("canvas") || n.Contains("vision")
                    || n.Contains("selection") || n.Contains("decal") || n.Contains("indicator"))
                    continue;
                if (n.Contains("site mesh")) return renderer;
                bool shown = renderer.gameObject.activeInHierarchy;
                if (n.Contains("placeholder") && (placeholder == null || shown))
                    placeholder = renderer;
                else if (n.Contains("building_02") && shown)
                    building = renderer;
                else if (fallback == null && shown)
                    fallback = renderer;
            }
            if (placeholder != null) return placeholder;
            if (building != null) return building;
            return fallback;
        }

        static Material DirtMat() => dirtMat ??= Lit("ColonyDirt", Desert, 0.92f, Color.black);
        static Material PitMat() => pitMat ??= Lit("ColonyPit", Soil, 0.94f, Color.black);
        static Material MetalMat() => metalMat ??= Lit("ColonySteel", Steel, 0.48f, Color.black);
        static Material HullMat() => hullMat ??= Lit("ColonyHull", Hull, 0.62f, Color.black);
        static Material AccentMat() => accentMat ??= Lit("ColonyAccent", Accent, 0.55f, Color.black);
        static Material GlassMat() => glassMat ??= Lit("ColonyGlass", Glass, 0.25f, new Color(0.05f, 0.08f, 0.12f));
        static Material BeamMat() => beamMat ??= Lit("ColonyBeam", Beam, 0.2f, Beam * 2.2f);

        static Material Lit(string name, Color color, float roughness, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - roughness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", roughness < 0.5f ? 0.35f : 0f);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        sealed class Draft
        {
            readonly List<Vector3> verts = new();
            readonly List<Vector3> normals = new();
            readonly List<int>[] tris = { new(), new(), new(), new() };

            public void Box(Vector3 center, Vector3 size, int sub, float yaw = 0f)
            {
                int start = verts.Count;
                Vector3 h = size * 0.5f;
                Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
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
                    tris[sub].Add(i); tris[sub].Add(i + 2); tris[sub].Add(i + 1);
                    tris[sub].Add(i); tris[sub].Add(i + 3); tris[sub].Add(i + 2);
                }
                if (Mathf.Abs(yaw) < 0.001f) return;
                var rot = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
                for (int i = start; i < verts.Count; i++)
                {
                    verts[i] = center + rot * (verts[i] - center);
                    normals[i] = rot * normals[i];
                }
            }

            public void Cylinder(Vector3 center, float radius, float height, int segments, Vector3 axis, int sub)
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
                    tris[sub].Add(i0); tris[sub].Add(i1); tris[sub].Add(i0 + 1);
                    tris[sub].Add(i1); tris[sub].Add(i1 + 1); tris[sub].Add(i0 + 1);
                }
                Cap(center + axis * half, axis, radius, segments, align, sub);
                Cap(center - axis * half, -axis, radius, segments, align, sub);
            }

            public void Beam(Vector3 a, Vector3 b, float thickness, int sub)
            {
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f) return;
                int start = verts.Count;
                Box((a + b) * 0.5f, new Vector3(thickness, len, thickness), sub);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
                Vector3 mid = (a + b) * 0.5f;
                for (int i = start; i < verts.Count; i++)
                {
                    verts[i] = mid + rot * (verts[i] - mid);
                    normals[i] = rot * normals[i];
                }
            }

            public void Disc(Vector3 center, float radius, float y, int segments, int sub)
            {
                Cap(center + Vector3.up * y, Vector3.up, radius, segments, Quaternion.identity, sub);
            }

            public void Ring(float y, float inner, float outer, int segments, int sub)
            {
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    verts.Add(new Vector3(c * outer, y, s * outer));
                    verts.Add(new Vector3(c * inner, y, s * inner));
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + ((i + 1) % segments) * 2;
                    tris[sub].Add(i0); tris[sub].Add(i0 + 1); tris[sub].Add(i1);
                    tris[sub].Add(i1); tris[sub].Add(i0 + 1); tris[sub].Add(i1 + 1);
                }
            }

            public void Slope(float yTop, float rTop, float yBottom, float rBottom, int segments, int sub, bool faceUp = false)
            {
                int ring = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    Vector3 top = new Vector3(c * rTop, yTop, s * rTop);
                    Vector3 bot = new Vector3(c * rBottom, yBottom, s * rBottom);
                    Vector3 tangent = new Vector3(-s, 0f, c);
                    Vector3 n = Vector3.Cross(tangent, bot - top).normalized;
                    if (faceUp)
                        n = new Vector3(-c, 0.45f, -s).normalized;
                    else if (n.y < 0f)
                        n = -n;
                    if (!faceUp && Mathf.Abs(rTop - rBottom) < 0.001f)
                        n = new Vector3(c, 0.15f, s).normalized;
                    verts.Add(top);
                    verts.Add(bot);
                    normals.Add(n);
                    normals.Add(n);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = ring + i * 2;
                    int i1 = ring + ((i + 1) % segments) * 2;
                    if (faceUp)
                    {
                        tris[sub].Add(i0); tris[sub].Add(i1); tris[sub].Add(i0 + 1);
                        tris[sub].Add(i1); tris[sub].Add(i1 + 1); tris[sub].Add(i0 + 1);
                    }
                    else
                    {
                        tris[sub].Add(i0); tris[sub].Add(i0 + 1); tris[sub].Add(i1);
                        tris[sub].Add(i1); tris[sub].Add(i0 + 1); tris[sub].Add(i1 + 1);
                    }
                }
            }

            void Cap(Vector3 center, Vector3 normal, float radius, int segments, Quaternion align, int sub)
            {
                int hub = verts.Count;
                verts.Add(center);
                normals.Add(normal);
                int rim = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = (Mathf.PI * 2f * i) / segments;
                    verts.Add(center + align * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                    normals.Add(normal);
                }
                bool flip = Vector3.Dot(normal, align * Vector3.up) < 0f;
                for (int i = 0; i < segments; i++)
                {
                    int a = rim + i;
                    int b = rim + (i + 1) % segments;
                    if (flip) { tris[sub].Add(hub); tris[sub].Add(a); tris[sub].Add(b); }
                    else { tris[sub].Add(hub); tris[sub].Add(b); tris[sub].Add(a); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                int count = 0;
                for (int i = 0; i < tris.Length; i++)
                    if (tris[i].Count > 0) count = i + 1;
                mesh.subMeshCount = Mathf.Max(1, count);
                for (int i = 0; i < mesh.subMeshCount; i++)
                    mesh.SetTriangles(tris[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
