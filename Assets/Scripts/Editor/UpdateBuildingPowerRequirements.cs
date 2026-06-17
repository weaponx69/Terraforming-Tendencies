using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;

public class UpdateBuildingPowerRequirements : EditorWindow
{
    [MenuItem("Tools/Update All Building Power Requirements")]
    public static void UpdatePower()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingSO");
        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingSO so = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            
            if (so != null)
            {
                // Skip the Solar Panel since it generates power, it doesn't need an upkeep.
                if (so.name.Contains("Solar", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool modified = false;

                // Ensure it has a BuildingConfigSO
                if (so.BuildingConfig == null)
                {
                    BuildingConfigSO newConfig = ScriptableObject.CreateInstance<BuildingConfigSO>();
                    newConfig.name = so.name + "_Config";
                    
                    // Save the new config asset in the same folder as the BuildingSO
                    string folderPath = System.IO.Path.GetDirectoryName(path);
                    string configPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + newConfig.name + ".asset");
                    
                    AssetDatabase.CreateAsset(newConfig, configPath);
                    
                    so.GetType().GetField("buildingConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so, newConfig);
                    modified = true;
                    Debug.Log($"[PowerUpdater] Created missing BuildingConfig for {so.name}");
                }

                // Ensure PowerUpkeep > 0
                if (so.BuildingConfig != null && so.BuildingConfig.PowerUpkeep <= 0)
                {
                    so.BuildingConfig.GetType().GetField("powerUpkeep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so.BuildingConfig, 5f);
                    EditorUtility.SetDirty(so.BuildingConfig);
                    modified = true;
                    Debug.Log($"[PowerUpdater] Set PowerUpkeep to 5 for {so.name}");
                }

                if (modified)
                {
                    EditorUtility.SetDirty(so);
                    updatedCount++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PowerUpdater] Finished updating {updatedCount} buildings to require power.");
    }
}
