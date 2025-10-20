using UnityEngine;
using UnityEditor;
using System;

public class TerrainToSphericalMesh : EditorWindow
{
    Terrain selectedTerrain;
    float baseRadius = 1000f;   // Base sphere radius
    float heightScale = 100f;   // Height exaggeration added on top of base radius
    int downsample = 2;         // Controls mesh resolution (lower = more detail)
    Material meshMaterial;

    [MenuItem("Window/Terrain/Convert Selected Terrain To Full Sphere")]
    static void Open() => GetWindow<TerrainToSphericalMesh>("Terrain → Sphere");

    void OnGUI()
    {
        selectedTerrain = EditorGUILayout.ObjectField("Terrain", selectedTerrain, typeof(Terrain), true) as Terrain;
        baseRadius = EditorGUILayout.FloatField("Base Radius", baseRadius);
        heightScale = EditorGUILayout.FloatField("Height Scale", heightScale);
        downsample = Mathf.Max(1, EditorGUILayout.IntField("Downsample Factor", downsample));
        meshMaterial = (Material)EditorGUILayout.ObjectField("Mesh Material", meshMaterial, typeof(Material), false);
        
        EditorGUILayout.HelpBox("This tool converts the full TerrainData heightmap onto a complete sphere.\nThe latitude (0 to PI) and longitude (0 to 2PI) are used to create a full planet. Textures/splatmaps are not transferred.", MessageType.Info);

        if (GUILayout.Button("Create Full Spherical Mesh") && selectedTerrain != null)
        {
            ConvertTerrainToFullSphere(selectedTerrain, baseRadius, heightScale, downsample, meshMaterial);
        }
    }

    static void ConvertTerrainToFullSphere(Terrain terrain, float baseRadius, float heightScale, int downsample, Material mat)
    {
        var td = terrain.terrainData;
        if (td == null)
        {
            Debug.LogError("Terrain has no TerrainData.");
            return;
        }

        int hmRes = td.heightmapResolution;
        int w = Mathf.CeilToInt((float)hmRes / downsample);
        int h = Mathf.CeilToInt((float)hmRes / downsample);

        // Read full-resolution heights then sample using downsample factor
        float[,] heightsFull = td.GetHeights(0, 0, hmRes, hmRes);

        Vector3[] verts = new Vector3[w * h];
        Vector2[] uvs = new Vector2[w * h];
        int[] tris = new int[(w - 1) * (h - 1) * 6];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        // Map grid coordinates to spherical lat/long:
        // Latitude ranges from 0 (north pole) to PI (south pole)
        // Longitude ranges from 0 to 2PI.
        for (int j = 0; j < h; j++)
        {
            int srcJ = Mathf.Clamp(j * downsample, 0, hmRes - 1);
            float tLat = (float)j / (h - 1);
            float lat = Mathf.Lerp(0f, Mathf.PI, tLat);
            for (int i = 0; i < w; i++)
            {
                int srcI = Mathf.Clamp(i * downsample, 0, hmRes - 1);
                float tLon = (float)i / (w - 1);
                float lon = Mathf.Lerp(0f, 2 * Mathf.PI, tLon);

                float height01 = heightsFull[srcJ, srcI];
                float worldHeight = height01 * td.size.y;
                minHeight = Mathf.Min(minHeight, worldHeight);
                maxHeight = Mathf.Max(maxHeight, worldHeight);

                float finalRadius = baseRadius + worldHeight * heightScale;
                // Spherical direction: (sin(lat)*cos(lon), cos(lat), sin(lat)*sin(lon))
                Vector3 direction = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                verts[j * w + i] = direction * finalRadius;
                uvs[j * w + i] = new Vector2(tLon, tLat);
            }
        }

        int ti = 0;
        for (int j = 0; j < h - 1; j++)
        {
            for (int i = 0; i < w - 1; i++)
            {
                int a = j * w + i;
                int b = a + 1;
                int c = (j + 1) * w + i;
                int d = c + 1;

                tris[ti++] = a;
                tris[ti++] = c;
                tris[ti++] = b;

                tris[ti++] = b;
                tris[ti++] = c;
                tris[ti++] = d;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = (verts.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log($"Spherical Mesh: Terrain='{terrain.name}' Grid={w}x{h}, Height(min={minHeight:F3}, max={maxHeight:F3}). BaseRadius={baseRadius}, HeightScale={heightScale}");

        // Create new GameObject
        string goName = terrain.name + "_FullSpherical";
        GameObject go = new GameObject(goName);
        go.transform.position = Vector3.zero; // vertices are now in world-space
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Standard"));
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Conversion Complete",
            $"Created '{goName}' with {verts.Length} vertices.\n(Heights: min={minHeight:F3}, max={maxHeight:F3})",
            "OK");
    }
}