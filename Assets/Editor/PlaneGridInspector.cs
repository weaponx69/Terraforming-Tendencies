using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlaneGridInspector
{
    [MenuItem("Tools/Inspect Selected Mesh Grid")]
    public static void InspectSelectedMeshGrid()
    {
        UnityEngine.Object sel = Selection.activeObject;
        GameObject sceneGO = Selection.activeGameObject;
        Mesh mesh = null;
        string meshName = "<none>";

        // 1) If a scene GameObject is selected, prefer its MeshFilter
        if (sceneGO != null)
        {
            var mf = sceneGO.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                mesh = mf.sharedMesh;
                meshName = mesh.name + " (from scene GameObject)";
            }
        }

        // 2) If a Mesh asset is selected in Project view
        if (mesh == null && sel is Mesh selMesh)
        {
            mesh = selMesh;
            meshName = mesh.name + " (Mesh asset)";
        }

        // 3) If a prefab GameObject asset is selected, try to load prefab contents and find a MeshFilter
        if (mesh == null && sel is GameObject selGO)
        {
            string assetPath = AssetDatabase.GetAssetPath(selGO);
            if (!string.IsNullOrEmpty(assetPath))
            {
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(assetPath);
                    var mf = contents.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        mesh = mf.sharedMesh;
                        meshName = mesh.name + " (from prefab asset)";
                    }
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        if (mesh == null)
        {
            Debug.LogWarning("Selected GameObject/asset has no MeshFilter/sharedMesh (select a scene object with a MeshFilter, a Mesh asset, or a prefab containing a MeshFilter).");
            return;
        }

        Vector3[] verts = mesh.vertices;
        // Note: if we used a scene GameObject earlier, we already transformed verts to world; do it here only for scene selection
        if (sceneGO != null && sceneGO.GetComponent<MeshFilter>() != null && sceneGO.GetComponent<MeshFilter>().sharedMesh == mesh)
        {
            for (int i = 0; i < verts.Length; i++)
                verts[i] = sceneGO.transform.TransformPoint(verts[i]);
        }

        // Collect unique X and Z (treat Y as up)
        var xs = verts.Select(v => v.x).Distinct().OrderBy(v => v).ToArray();
        var zs = verts.Select(v => v.z).Distinct().OrderBy(v => v).ToArray();

        bool isGrid = xs.Length * zs.Length == verts.Length;

        string msg = $"Mesh '{meshName}' vertex count: {verts.Length}\nUnique X: {xs.Length}, Unique Z: {zs.Length}\nGrid candidate: {isGrid}\n";

        if (isGrid)
        {
            // check uniform spacing
            Func<float[], (bool ok, float spacing, float maxDiff)> checkSpacing = (arr) =>
            {
                if (arr.Length < 2) return (true, 0f, 0f);
                var diffs = new List<float>();
                for (int i = 1; i < arr.Length; i++) diffs.Add(arr[i] - arr[i - 1]);
                float avg = diffs.Average();
                float maxDiff = diffs.Max(d => Math.Abs(d - avg));
                return (maxDiff < 1e-3f, avg, maxDiff);
            };

            var (xOk, xSpacing, xMaxDiff) = checkSpacing(xs);
            var (zOk, zSpacing, zMaxDiff) = checkSpacing(zs);

            msg += $"X spacing ≈ {xSpacing:F4} (uniform: {xOk}, max dev: {xMaxDiff:F6})\n";
            msg += $"Z spacing ≈ {zSpacing:F4} (uniform: {zOk}, max dev: {zMaxDiff:F6})\n";

            if (xOk && zOk)
                msg += "Conclusion: mesh vertices form a regular grid suitable for spherify operations.\n";
            else
                msg += "Conclusion: mesh forms grid-shaped topology but spacing is not uniform (may be irregular).\n";
        }
        else
        {
            if (verts.Length == 4)
            {
                msg += "This is likely a single quad (4 vertices) — not a grid.\n";
            }
            else
            {
                msg += "Not a regular grid (vertex count doesn't equal uniqueX * uniqueZ).\n";
            }
        }

        bool normalsExist = mesh.normals != null && mesh.normals.Length == verts.Length;
        if (normalsExist)
        {
            var upAligned = mesh.normals.All(n => Vector3.Dot(n.normalized, Vector3.up) > 0.9f);
            msg += $"Normals present: yes. Mostly Y-up: {upAligned}\n";
        }
        else
        {
            msg += "Normals present: no (or count mismatch).\n";
        }

        Debug.Log(msg);
        EditorUtility.DisplayDialog("Mesh Grid Inspector", msg, "OK");
    }
}