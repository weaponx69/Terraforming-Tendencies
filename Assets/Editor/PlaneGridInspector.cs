using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class PlaneGridInspector
{
    [MenuItem("Tools/Inspect Selected Mesh Grid")]
    public static void InspectSelectedMeshGrid()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("No GameObject selected.");
            return;
        }

        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("Selected GameObject has no MeshFilter/sharedMesh.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts = mesh.vertices;

        // Transform vertices to world (in case mesh is not centered)
        for (int i = 0; i < verts.Length; i++)
            verts[i] = go.transform.TransformPoint(verts[i]);

        // Collect unique X and Z (treat Y as up)
        var xs = verts.Select(v => v.x).Distinct().OrderBy(v => v).ToArray();
        var zs = verts.Select(v => v.z).Distinct().OrderBy(v => v).ToArray();

        bool isGrid = xs.Length * zs.Length == verts.Length;

        string msg = $"Mesh '{mesh.name}' vertex count: {verts.Length}\nUnique X: {xs.Length}, Unique Z: {zs.Length}\nGrid candidate: {isGrid}\n";

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
            // check for simple quad
            if (verts.Length == 4)
            {
                msg += "This is likely a single quad (4 vertices) — not a grid.\n";
            }
            else
            {
                msg += "Not a regular grid (vertex count doesn't equal uniqueX * uniqueZ).\n";
            }
        }

        // check normals roughly up
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