#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Environment;

public class SetupMVPScene
{
    [MenuItem("Terraforming/1-Click Setup MVP Scene")]
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
        HiddenResourceSpawner resourceSpawner = planetManager.GetComponent<HiddenResourceSpawner>() ?? planetManager.AddComponent<HiddenResourceSpawner>();
        
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
            AssetDatabase.SaveAssets();
            Debug.Log("[MVP Setup] Created new PlanetConfig at " + configPath);
        }

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
}
#endif
