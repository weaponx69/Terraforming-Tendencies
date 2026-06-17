using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Commands;

public class FixBuildingPrefabsAndIcons : EditorWindow
{
    [MenuItem("Tools/Fix Habitat and Solar Panel Visuals")]
    public static void FixVisuals()
    {
        // 1. Create Unique Materials
        Material habMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        habMat.color = new Color(0.2f, 0.8f, 0.2f); // Green
        AssetDatabase.CreateAsset(habMat, "Assets/Units/Buildings/HabitatMaterial.mat");

        Material solarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        solarMat.color = new Color(0.1f, 0.5f, 0.9f); // Blue
        AssetDatabase.CreateAsset(solarMat, "Assets/Units/Buildings/SolarPanelMaterial.mat");

        // 2. Assign to Prefabs
        AssignMaterialToPrefab("Assets/Units/Buildings/Habitat.prefab", habMat);
        AssignMaterialToPrefab("Assets/Units/Buildings/SolarPanel.prefab", solarMat);
        
        // Also fix the Ghost Prefabs so the placement hologram looks right
        AssignMaterialToPrefab("Assets/Units/Buildings/Habitat Ghost.prefab", habMat);
        AssignMaterialToPrefab("Assets/Units/Buildings/Solar Panel Ghost.prefab", solarMat);

        // 3. Clear the icons so they don't look like Barracks
        ClearCommandIcon("Assets/Units/Commands/Build Habitat.asset");
        ClearCommandIcon("Assets/Units/Commands/Build Solar Panel.asset");

        AssetDatabase.SaveAssets();
        Debug.Log("[FixVisuals] Successfully gave the Habitat and Solar Panel unique colors and cleared their confusing icons!");
    }

    private static void AssignMaterialToPrefab(string path, Material mat)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject.name != "SelectionIndicator" && r.gameObject.name != "SelectionIndicator(Clone)")
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for(int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        PrefabUtility.UnloadPrefabContents(instance);
    }

    private static void ClearCommandIcon(string path)
    {
        BuildBuildingCommand cmd = AssetDatabase.LoadAssetAtPath<BuildBuildingCommand>(path);
        if (cmd != null)
        {
            SerializedObject so = new SerializedObject(cmd);
            SerializedProperty iconProp = so.FindProperty("<Icon>k__BackingField");
            if (iconProp != null)
            {
                iconProp.objectReferenceValue = null;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cmd);
            }
        }
    }
}
