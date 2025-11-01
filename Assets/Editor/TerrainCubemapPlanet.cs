using UnityEngine;
using UnityEditor;

public class TerrainCubemapPlanet : EditorWindow
{
    Terrain selectedTerrain;
    float sphereRadius = 1000f;
    bool spherifyTiles = true; // if true, tiles are projected so that their centers lie on the sphere

    [MenuItem("Window/Terrain/Convert Terrain To Cubemap Planet")]
    public static void ShowWindow() =>
        GetWindow<TerrainCubemapPlanet>("Terrain Cubemap Planet");

    void OnGUI()
    {
        selectedTerrain = EditorGUILayout.ObjectField("Terrain", selectedTerrain, typeof(Terrain), true) as Terrain;
        sphereRadius = EditorGUILayout.FloatField("Sphere Radius", sphereRadius);
        spherifyTiles = EditorGUILayout.Toggle("Project Tiles to Sphere", spherifyTiles);

        EditorGUILayout.HelpBox("This tool duplicates a selected Terrain into six faces, arranged like a cubemap. " +
            "If 'Project Tiles to Sphere' is enabled, each face is moved and rotated so its center lies on a sphere of the specified radius.", MessageType.Info);

        if (GUILayout.Button("Generate Cubemap Planet") && selectedTerrain != null)
        {
            GenerateCubemapPlanet(selectedTerrain, sphereRadius, spherifyTiles);
        }
    }

    static void GenerateCubemapPlanet(Terrain terrain, float radius, bool spherify)
    {
        // Create a parent for organization.
        GameObject planetParent = new GameObject(terrain.name + "_CubemapPlanet");

        // Define directions corresponding to cube faces.
        Vector3[] faceDirections = new Vector3[]
        {
            Vector3.forward, // Front
            Vector3.back,    // Back
            Vector3.left,    // Left
            Vector3.right,   // Right
            Vector3.up,      // Top
            Vector3.down     // Bottom
        };

        // For each direction, duplicate and position the terrain.
        foreach (Vector3 dir in faceDirections)
        {
            GameObject clone = Instantiate(terrain.gameObject, planetParent.transform);
            clone.name = terrain.gameObject.name + "_" + dir.ToString();

            // Assume the terrain pivot is at one corner; 
            // adjust by half the terrain size to center it.
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 centerOffset = new Vector3(terrainSize.x, 0, terrainSize.z) * 0.5f;

            // Initial local position as if tiles were arranged flat.
            Vector3 localPos = dir * (terrainSize.z * 0.5f) - centerOffset;

            if (spherify)
            {
                // Project the position onto a sphere.
                Vector3 projected = localPos.normalized * radius;
                clone.transform.position = projected;
                // Rotate the clone so that its "up" (assumed to be Vector3.up) faces outward.
                clone.transform.rotation = Quaternion.FromToRotation(Vector3.up, projected.normalized);
            }
            else
            {
                clone.transform.position = localPos;
            }
        }

        EditorUtility.DisplayDialog("Cubemap Planet Generated",
            $"Created {faceDirections.Length} terrain copies under '{planetParent.name}'.\n" +
            "Adjust and refine positioning as needed to reduce seams.",
            "OK");
        Selection.activeGameObject = planetParent;
    }
}