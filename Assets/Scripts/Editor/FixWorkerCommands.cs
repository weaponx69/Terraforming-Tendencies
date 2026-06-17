using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;

public class FixWorkerCommands : EditorWindow
{
    [MenuItem("Tools/Fix Worker Commands and Icons")]
    public static void FixCommands()
    {
        // 1. Create Placeholder Icons (Colored squares)
        Sprite habIcon = CreateColorSprite("Assets/Units/Commands/HabitatIcon.png", new Color(0.2f, 0.8f, 0.2f));
        Sprite solarIcon = CreateColorSprite("Assets/Units/Commands/SolarPanelIcon.png", new Color(0.1f, 0.5f, 0.9f));

        // 2. Assign the new icons to the command assets
        AssignIcon("Assets/Units/Commands/Build Habitat.asset", habIcon);
        AssignIcon("Assets/Units/Commands/Build Solar Panel.asset", solarIcon);

        // 3. Add the Habitat command to the Mining Drone and Construction Drone
        BuildBuildingCommand habCmd = AssetDatabase.LoadAssetAtPath<BuildBuildingCommand>("Assets/Units/Commands/Build Habitat.asset");
        if (habCmd != null)
        {
            AddCommandToPrefab("Assets/Units/Mining Drone/Mining Drone.prefab", habCmd);
            AddCommandToPrefab("Assets/Units/Construction Drone/Construction Drone.prefab", habCmd); // If it exists
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FixWorkerCommands] Successfully restored icons and added the Habitat to the Worker build menus!");
    }

    private static Sprite CreateColorSprite(string path, Color color)
    {
        Texture2D tex = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        tex.SetPixels(colors);
        tex.Apply();
        
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void AssignIcon(string path, Sprite sprite)
    {
        BuildBuildingCommand cmd = AssetDatabase.LoadAssetAtPath<BuildBuildingCommand>(path);
        if (cmd != null && sprite != null)
        {
            SerializedObject so = new SerializedObject(cmd);
            SerializedProperty iconProp = so.FindProperty("<Icon>k__BackingField");
            if (iconProp != null)
            {
                iconProp.objectReferenceValue = sprite;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cmd);
            }
        }
    }

    private static void AddCommandToPrefab(string path, BaseCommand cmdToAdd)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = PrefabUtility.LoadPrefabContents(path);
        AbstractUnit unit = instance.GetComponent<AbstractUnit>();
        
        if (unit != null)
        {
            SerializedObject so = new SerializedObject(unit);
            SerializedProperty cmds = so.FindProperty("<AvailableCommands>k__BackingField");
            if (cmds != null)
            {
                // Check if it already exists
                bool exists = false;
                for (int i = 0; i < cmds.arraySize; i++)
                {
                    if (cmds.GetArrayElementAtIndex(i).objectReferenceValue == cmdToAdd)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    cmds.InsertArrayElementAtIndex(cmds.arraySize);
                    cmds.GetArrayElementAtIndex(cmds.arraySize - 1).objectReferenceValue = cmdToAdd;
                    so.ApplyModifiedProperties();
                    Debug.Log($"Added {cmdToAdd.name} to {prefab.name}");
                }
            }
        }
        
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        PrefabUtility.UnloadPrefabContents(instance);
    }
}
