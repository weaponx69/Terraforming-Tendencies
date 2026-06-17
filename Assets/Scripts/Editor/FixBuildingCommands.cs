using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;

public class FixBuildingCommands : EditorWindow
{
    [MenuItem("Tools/Fix Habitat and Solar Panel Commands")]
    public static void FixCommands()
    {
        ClearCommandsFromPrefab("Assets/Units/Buildings/Habitat.prefab");
        ClearCommandsFromPrefab("Assets/Units/Buildings/SolarPanel.prefab");

        // Let's also check if they are in the subfolders just in case
        ClearCommandsFromPrefab("Assets/Units/Buildings/Habitat/Habitat.prefab");
        ClearCommandsFromPrefab("Assets/Units/Buildings/SolarPanel/SolarPanel.prefab");

        AssetDatabase.SaveAssets();
        Debug.Log("[FixCommands] Cleared out the copied Barracks commands from Habitat and Solar Panel!");
    }

    private static void ClearCommandsFromPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        BaseBuilding bb = instance.GetComponent<BaseBuilding>();
        
        if (bb != null)
        {
            SerializedObject so = new SerializedObject(bb);
            SerializedProperty cmds = so.FindProperty("<AvailableCommands>k__BackingField");
            if (cmds != null)
            {
                cmds.ClearArray();
                so.ApplyModifiedProperties();
                Debug.Log($"Cleared commands for {path}");
            }
        }
        
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        PrefabUtility.UnloadPrefabContents(instance);
    }
}
