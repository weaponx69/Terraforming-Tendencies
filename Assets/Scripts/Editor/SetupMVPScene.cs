#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Environment;

public class SetupMVPScene
{
    // [MenuItem("Terraforming/1-Click Setup MVP Scene")]
    public static void SetupScene()
    {
        // 1. Create or find PlanetManager
        GameObject planetManager = GameObject.Find("PlanetManager");
        if (planetManager == null)
        {
            planetManager = new GameObject("PlanetManager");
        }

        // 2. Add all necessary components
        CampaignManager campaignManager = planetManager.GetComponent<CampaignManager>() ?? planetManager.AddComponent<CampaignManager>();
        PlanetGenerator planetGenerator = planetManager.GetComponent<PlanetGenerator>() ?? planetManager.AddComponent<PlanetGenerator>();
        MapWrapper mapWrapper = planetManager.GetComponent<MapWrapper>() ?? planetManager.AddComponent<MapWrapper>();
        GlobalDecayManager decayManager = planetManager.GetComponent<GlobalDecayManager>() ?? planetManager.AddComponent<GlobalDecayManager>();
        
        // Add NavMeshSurface for the AI
        NavMeshSurface navMeshSurface = planetManager.GetComponent<NavMeshSurface>() ?? planetManager.AddComponent<NavMeshSurface>();

        // 3. Ensure a Planet Config exists
        string configPath = "Assets/Settings/Planet 1 - Easy.asset";
        PlanetConfig config = AssetDatabase.LoadAssetAtPath<PlanetConfig>(configPath);
        
        if (config == null)
        {
            // Ensure Settings folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            config = ScriptableObject.CreateInstance<PlanetConfig>();
            config.MapWidth = 50;
            config.MapHeight = 50;
            config.NoiseScale = 15f;
            config.HeightMultiplier = 3f;
            config.ResourceCount = 10;
            config.BaseDecayRate = 2f;
            
            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log("[MVP Setup] Created new PlanetConfig at " + configPath);
        }

        // Auto-assign prefabs to config
        config.SurfaceRockPrefabs = FindPrefabsInFolder("Assets/ProceduralAssets/Prefabs");
        config.ResourcePrefabs = FindPrefabsInFolder("Assets/Gatherable Supplies");
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        // 4. Assign the Config to our scripts
        planetGenerator.Config = config;

        // Use reflection or serialized object to set CampaignManager's levelConfigs if it's private/serialized
        SerializedObject serializedCampaign = new SerializedObject(campaignManager);
        SerializedProperty levelConfigsProp = serializedCampaign.FindProperty("levelConfigs");
        if (levelConfigsProp != null)
        {
            levelConfigsProp.arraySize = 1;
            levelConfigsProp.GetArrayElementAtIndex(0).objectReferenceValue = config;
            serializedCampaign.ApplyModifiedProperties();
        }

        // Make sure it gets saved
        EditorUtility.SetDirty(planetManager);
        
        Debug.Log("[MVP Setup] Successfully rebuilt the PlanetManager and attached all scripts! You can now hit Play.");
        Selection.activeGameObject = planetManager;
    }

    private static GameObject[] FindPrefabsInFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return new GameObject[0];

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
        }
        return prefabs.ToArray();
    }
}
#endif
