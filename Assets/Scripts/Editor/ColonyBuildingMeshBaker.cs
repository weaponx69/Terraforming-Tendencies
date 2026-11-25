using System.IO;
using System.Text;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS
{
    /// <summary>
    /// Writes a mesh asset for every themed building that still shows a Unity primitive
    /// and points the prefab at that mesh.
    /// </summary>
    public static class ColonyBuildingMeshBaker
    {
        const string MeshFolder = "Assets/Units/Buildings/Themed/SiteMeshes";

        public static string BakeCommandPost()
        {
            const string path = "Assets/Units/Buildings/Command Post/Command Post.prefab";
            Directory.CreateDirectory(MeshFolder);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "Building_02")
                        t.gameObject.SetActive(false);
                }

                Transform meshT = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "Building Mesh")
                    {
                        meshT = t;
                        break;
                    }
                }
                if (meshT == null)
                {
                    var go = new GameObject("Building Mesh");
                    go.transform.SetParent(root.transform, false);
                    meshT = go.transform;
                }

                var filter = meshT.GetComponent<MeshFilter>();
                if (filter == null) filter = meshT.gameObject.AddComponent<MeshFilter>();
                var renderer = meshT.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = meshT.gameObject.AddComponent<MeshRenderer>();

                Mesh mesh = ColonyGroundMesh.CreateCommandPostMesh();
                mesh = SaveMesh(mesh, MeshFolder + "/CommandPost.asset");
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = new[]
                {
                    Mat("CommandDirt", new Color(0.74f, 0.40f, 0.16f), 0f, 0.12f, Color.black),
                    Mat("CommandHull", new Color(0.72f, 0.68f, 0.62f), 0.15f, 0.38f, Color.black),
                    Mat("CommandAccent", new Color(0.78f, 0.38f, 0.12f), 0.25f, 0.4f, Color.black),
                    Mat("CommandGlass", new Color(0.12f, 0.16f, 0.20f), 0.1f, 0.7f, new Color(0.04f, 0.06f, 0.1f))
                };
                meshT.localPosition = Vector3.zero;
                meshT.localRotation = Quaternion.identity;
                meshT.localScale = Vector3.one;
                meshT.gameObject.SetActive(true);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return "command post baked verts=" + mesh.vertexCount;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string BakeHabitat()
        {
            const string path = "Assets/Units/Buildings/Habitat/Habitat.prefab";
            Directory.CreateDirectory(MeshFolder);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var doomed = new System.Collections.Generic.List<GameObject>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == root.transform) continue;
                    if (t.name == "Building_02" || t.name.StartsWith("tripo_node"))
                        t.gameObject.SetActive(false);
                    if (t.name == "Building Mesh")
                        doomed.Add(t.gameObject);
                }
                foreach (var go in doomed)
                    Object.DestroyImmediate(go);

                var meshGo = new GameObject("Building Mesh");
                meshGo.transform.SetParent(root.transform, false);
                var filter = meshGo.AddComponent<MeshFilter>();
                var renderer = meshGo.AddComponent<MeshRenderer>();
                Mesh mesh = ColonyBuildingMeshLibrary.Build("Habitat");
                mesh = SaveMesh(mesh, MeshFolder + "/HabitatDomes.asset");
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = new[]
                {
                    Mat("HabitatHull", new Color(0.82f, 0.84f, 0.86f), 0.15f, 0.45f, Color.black),
                    Mat("HabitatSteel", new Color(0.45f, 0.48f, 0.52f), 0.65f, 0.5f, Color.black),
                    Mat("HabitatDoor", new Color(0.08f, 0.09f, 0.11f), 0.1f, 0.25f, Color.black),
                    Mat("HabitatGlass", new Color(0.25f, 0.78f, 0.95f), 0.05f, 0.85f, new Color(0.05f, 0.35f, 0.48f)),
                    Mat("HabitatTrim", new Color(0.9f, 0.46f, 0.14f), 0.25f, 0.42f, new Color(0.22f, 0.07f, 0.02f))
                };
                var box = root.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                }
                var building = root.GetComponent<BaseBuilding>();
                if (building != null)
                    building.SetMainRenderer(renderer);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return "habitat baked size=" + mesh.bounds.size;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Placement preview for the Habitat card. Same dome mesh as the finished building,
        /// drawn see-through. The old Tripo ghost stays in the prefab but turned off.
        /// </summary>
        public static string BakeHabitatGhost()
        {
            const string path = "Assets/Units/Buildings/Habitat/Habitat Ghost Variant.prefab";
            const string meshPath = MeshFolder + "/HabitatDomes.asset";
            Directory.CreateDirectory(MeshFolder);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
                return "missing " + meshPath;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var doomed = new System.Collections.Generic.List<GameObject>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == root.transform) continue;
                    if (t.name == "Building_05" || t.name.StartsWith("tripo_node")
                        || t.name == "Vision" || t.name.Contains("Selection"))
                        t.gameObject.SetActive(false);
                    if (t.name == "Building Mesh")
                        doomed.Add(t.gameObject);
                }
                foreach (var go in doomed)
                    Object.DestroyImmediate(go);

                var meshGo = new GameObject("Building Mesh");
                meshGo.transform.SetParent(root.transform, false);
                var filter = meshGo.AddComponent<MeshFilter>();
                var renderer = meshGo.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                var ghost = GhostMat(
                    "HabitatGhost",
                    new Color(0.55f, 0.82f, 0.95f, 0.42f),
                    new Color(0.08f, 0.28f, 0.38f, 1f));
                renderer.sharedMaterials = new[] { ghost, ghost, ghost, ghost, ghost };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return "habitat ghost baked verts=" + mesh.vertexCount + " queue=" + ghost.renderQueue;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string BakeOxygen()
        {
            const string path = "Assets/Units/Buildings/Oxygen Processor/Oxygen Processor Visuals.prefab";
            Directory.CreateDirectory(MeshFolder);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.localScale = Vector3.one;
                var doomed = new System.Collections.Generic.List<GameObject>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "Building Mesh")
                        doomed.Add(t.gameObject);
                }
                foreach (var go in doomed)
                    Object.DestroyImmediate(go);

                var mesh = ColonyBuildingMeshLibrary.Build("Oxygen Processor");
                mesh = SaveMesh(mesh, MeshFolder + "/OxygenProcessor.asset");
                var meshGo = new GameObject("Building Mesh");
                meshGo.transform.SetParent(root.transform, false);
                var filter = meshGo.AddComponent<MeshFilter>();
                var renderer = meshGo.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = new[]
                {
                    Mat("OxygenHull", new Color(0.72f, 0.76f, 0.78f), 0.2f, 0.4f, Color.black),
                    Mat("OxygenSteel", new Color(0.42f, 0.48f, 0.52f), 0.7f, 0.5f, Color.black),
                    Mat("OxygenPit", new Color(0.08f, 0.1f, 0.12f), 0.1f, 0.2f, Color.black),
                    Mat("OxygenGlass", new Color(0.35f, 0.82f, 0.95f), 0.05f, 0.8f, new Color(0.04f, 0.28f, 0.4f)),
                    Mat("OxygenTrim", new Color(0.9f, 0.5f, 0.16f), 0.25f, 0.4f, new Color(0.2f, 0.08f, 0.02f))
                };
                var box = root.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                }
                var building = root.GetComponent<BaseBuilding>();
                if (building != null)
                    building.SetMainRenderer(renderer);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return "oxygen baked size=" + mesh.bounds.size;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string BakeOxygenGhost()
        {
            const string path = "Assets/Units/Buildings/Oxygen Processor/Oxygen Processor Ghost Variant.prefab";
            const string meshPath = MeshFolder + "/OxygenProcessor.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
                return "missing " + meshPath;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.localScale = Vector3.one;
                var doomed = new System.Collections.Generic.List<GameObject>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == root.transform) continue;
                    if (t.name == "Building_05" || t.name.StartsWith("tripo_node")
                        || t.name == "Vision" || t.name.Contains("Selection"))
                        t.gameObject.SetActive(false);
                    if (t.name == "Building Mesh")
                        doomed.Add(t.gameObject);
                }
                foreach (var go in doomed)
                    Object.DestroyImmediate(go);

                var meshGo = new GameObject("Building Mesh");
                meshGo.transform.SetParent(root.transform, false);
                var filter = meshGo.AddComponent<MeshFilter>();
                var renderer = meshGo.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                var ghost = GhostMat(
                    "OxygenGhost",
                    new Color(0.55f, 0.88f, 0.95f, 0.4f),
                    new Color(0.06f, 0.32f, 0.4f, 1f));
                renderer.sharedMaterials = new[] { ghost, ghost, ghost, ghost, ghost };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                return "oxygen ghost baked verts=" + mesh.vertexCount + " queue=" + ghost.renderQueue;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string BakeAll()
        {
            Directory.CreateDirectory(MeshFolder);
            var mats = EnsureMaterials();
            var sb = new StringBuilder();
            int done = 0;
            string[] prefabs = Directory.GetFiles("Assets/Units/Buildings/Themed", "*.prefab", SearchOption.AllDirectories);
            foreach (string path in prefabs)
            {
                string assetPath = path.Replace('\\', '/');
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    bool changed = false;
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter == null) continue;
                        if (filter.gameObject.name.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        Mesh mesh = ColonyBuildingMeshLibrary.Build(root.name);
                        if (mesh == null)
                        {
                            sb.Append("NO MESH ").Append(root.name).Append("; ");
                            continue;
                        }
                        string meshPath = MeshFolder + "/" + Sanitize(root.name) + ".asset";
                        mesh = SaveMesh(mesh, meshPath);
                        filter.sharedMesh = mesh;
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            var used = new Material[mesh.subMeshCount];
                            for (int i = 0; i < used.Length; i++)
                                used[i] = mats[Mathf.Min(i, mats.Length - 1)];
                            renderer.sharedMaterials = used;
                        }
                        filter.transform.localPosition = Vector3.zero;
                        filter.transform.localRotation = Quaternion.identity;
                        filter.transform.localScale = Vector3.one;
                        filter.gameObject.name = "Building Mesh";
                        changed = true;
                    }
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    done++;
                    sb.Append(root.name).Append("; ");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            return "baked=" + done + " " + sb;
        }

        static Mesh SaveMesh(Mesh mesh, string meshPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        static Material[] EnsureMaterials()
        {
            return new[]
            {
                Mat("Hull", new Color(0.62f, 0.40f, 0.24f), 0.05f, 0.18f, Color.black),
                Mat("Steel", new Color(0.55f, 0.58f, 0.60f), 0.55f, 0.42f, Color.black),
                Mat("Pit", new Color(0.16f, 0.09f, 0.06f), 0f, 0.08f, Color.black),
                Mat("Glass", new Color(0.45f, 0.62f, 0.68f), 0.05f, 0.72f, new Color(0.05f, 0.08f, 0.1f)),
                Mat("Trim", new Color(0.85f, 0.42f, 0.14f), 0.2f, 0.35f, new Color(0.35f, 0.12f, 0.02f))
            };
        }

        static Material Mat(string name, Color color, float metallic, float smoothness, Color emission)
        {
            string path = MeshFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GhostMat(string name, Color color, Color emission)
        {
            string path = MeshFolder + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
