using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class BuildingVariantRefactorer : EditorWindow
{
    [MenuItem("Tools/Refactor Building Variants")]
    public static void RefactorBuildings()
    {
        string basePrefabPath = "Assets/Units/Buildings/BaseBuilding.prefab";
        
        // Ensure BaseBuilding exists
        if (!File.Exists(basePrefabPath))
        {
            // If it doesn't exist, let's copy the Command Post as a starting point
            string commandPostPath = "Assets/Units/Buildings/Command Post/Command Post.prefab";
            if (!File.Exists(commandPostPath))
            {
                Debug.LogError("Could not find Command Post to use as a base. Please ensure 'Assets/Units/Buildings/Command Post/Command Post.prefab' exists.");
                return;
            }
            AssetDatabase.CopyAsset(commandPostPath, basePrefabPath);
            AssetDatabase.Refresh();
            
            // Clean up the newly created BaseBuilding
            GameObject baseContents = PrefabUtility.LoadPrefabContents(basePrefabPath);
            
            // Clear the ScriptableObject reference on the base
            var bb = baseContents.GetComponent<GameDevTV.RTS.Units.BaseBuilding>();
            if (bb != null)
            {
                var soField = typeof(GameDevTV.RTS.Units.BaseBuilding).GetField("<UnitSO>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (soField != null) soField.SetValue(bb, null);
            }
            
            PrefabUtility.SaveAsPrefabAsset(baseContents, basePrefabPath);
            PrefabUtility.UnloadPrefabContents(baseContents);
            Debug.Log("Created BaseBuilding.prefab from Command Post.");
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);

        // Find all building prefabs to convert
        string[] allBuildingGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Units/Buildings" });

        foreach (string guid in allBuildingGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == basePrefabPath || path.Contains("Ghost Variant") || path.Contains("Command Post") || path.Contains("Foundry")) 
                continue;

            // Load the old prefab to extract its data
            GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (oldPrefab == null || oldPrefab.GetComponent<GameDevTV.RTS.Units.BaseBuilding>() == null)
                continue;

            if (PrefabUtility.GetPrefabAssetType(oldPrefab) == PrefabAssetType.Variant)
            {
                // It's already a variant, skip it
                continue;
            }

            // Extract the UnitSO before we overwrite
            var oldBB = oldPrefab.GetComponent<GameDevTV.RTS.Units.BaseBuilding>();
            var oldUnitSO = oldBB.BuildingSO;

            // Create a new variant in memory from the BaseBuilding
            GameObject variantInstance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

            // Apply the UnitSO override
            var variantBB = variantInstance.GetComponent<GameDevTV.RTS.Units.BaseBuilding>();
            if (variantBB != null && oldUnitSO != null)
            {
                var serializedObj = new SerializedObject(variantBB);
                var prop = serializedObj.FindProperty("<UnitSO>k__BackingField");
                if (prop != null)
                {
                    prop.objectReferenceValue = oldUnitSO;
                    serializedObj.ApplyModifiedProperties();
                }
            }

            // Save the new variant over the exact same path
            PrefabUtility.SaveAsPrefabAsset(variantInstance, path);
            DestroyImmediate(variantInstance);

            Debug.Log($"Successfully converted {path} into a Prefab Variant of BaseBuilding.");
        }

        AssetDatabase.Refresh();
        Debug.Log("Finished converting buildings to Prefab Variants!");
    }
}
