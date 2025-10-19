using UnityEngine;
using UnityEditor;

public class TerrainToSphericalMesh : EditorWindow
{
    Terrain selectedTerrain;
    float planetRadius = 50f;
    float heightScale = 1f;
    int downsample = 4; // 1 = full resolution, 2 = half, etc.
    Material meshMaterial;

    [MenuItem("Window/Terrain/Convert Selected Terrain To Sphere")]
    static void Open() => GetWindow<TerrainToSphericalMesh>("Terrain → Sphere");

    void OnGUI()
    {
        selectedTerrain = EditorGUILayout.ObjectField("Terrain", selectedTerrain, typeof(Terrain), true) as Terrain;
        planetRadius = EditorGUILayout.FloatField("Base Radius", planetRadius);
        heightScale = EditorGUILayout.FloatField("Height Scale", heightScale);
        downsample = Mathf.Max(1, EditorGUILayout.IntField("Downsample Factor", downsample));
        meshMaterial = (Material)EditorGUILayout.ObjectField("Mesh Material", meshMaterial, typeof(Material), false);

        EditorGUILayout.HelpBox("This converts the TerrainData heightmap into a mesh and projects vertices radially onto a sphere.\nTextures / splatmaps are NOT transferred.", MessageType.Info);

        if (GUILayout.Button("Create Spherified Mesh") && selectedTerrain != null)
        {
            ConvertTerrainToSphere(selectedTerrain, planetRadius, heightScale, downsample, meshMaterial);
        }
    }

    static void ConvertTerrainToSphere(Terrain terrain, float baseRadius, float heightScale, int downsample, Material mat)
    {
        var td = terrain.terrainData;
        if (td == null)
        {
            Debug.LogError("Terrain has no TerrainData.");
            return;
        }

        int hmW = td.heightmapResolution;
        int hmH = td.heightmapResolution;
        int w = Mathf.CeilToInt((float)hmW / downsample);
        int h = Mathf.CeilToInt((float)hmH / downsample);

        // sample heights block (downsampled)
        float[,] heights = td.GetHeights(0, 0, hmW, hmH);

        Vector3 terrainSize = td.size;
        Vector3[] verts = new Vector3[w * h];
        Vector2[] uvs = new Vector2[w * h];
        int[] tris = new int[(w - 1) * (h - 1) * 6];

        // origin centering in XZ
        float halfX = terrainSize.x * 0.5f;
        float halfZ = terrainSize.z * 0.5f;

        for (int j = 0; j < h; j++)
        {
            int srcJ = Mathf.Clamp(j * downsample, 0, hmH - 1);
            for (int i = 0; i < w; i++)
            {
                int srcI = Mathf.Clamp(i * downsample, 0, hmW - 1);
                float height01 = heights[srcJ, srcI]; // 0..1
                float worldHeight = height01 * terrainSize.y;

                // local plane coords centered
                float x = ((float)i / (w - 1)) * terrainSize.x - halfX;
                float z = ((float)j / (h - 1)) * terrainSize.z - halfZ;

                // direction in XZ plane from center to vertex (avoid Y influence for direction)
                Vector3 dir = new Vector3(x, 0f, z);
                if (dir.sqrMagnitude < 1e-6f)
                    dir = Vector3.forward; // avoid zero dir at center
                dir.Normalize();

                // radial position: baseRadius + (height * heightScale)
                float radial = baseRadius + worldHeight * heightScale;
                Vector3 v = dir * radial;

                verts[j * w + i] = v;
                uvs[j * w + i] = new Vector2((float)i / (w - 1), (float)j / (h - 1));
            }
        }

        int ti = 0;
        for (int j = 0; j < h - 1; j++)
        {
            for (int i = 0; i < w - 1; i++)
            {
                int a = j * w + i;
                int b = j * w + i + 1;
                int c = (j + 1) * w + i;
                int d = (j + 1) * w + i + 1;

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

        // create GameObject
        string name = terrain.name + "_Spherified";
        GameObject go = new GameObject(name);
        go.transform.position = terrain.transform.position; // position at terrain object position
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;
        else mr.sharedMaterial = new Material(Shader.Find("Standard"));

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        Selection.activeGameObject = go;
        Debug.Log($"Created spherified mesh '{name}' with {verts.Length} verts ({w}x{h}). Note: splatmaps/trees are not transferred.");
    }
}