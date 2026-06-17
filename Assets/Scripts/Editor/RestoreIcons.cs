using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Commands;

public class RestoreIcons : EditorWindow
{
    [MenuItem("Tools/Restore Original Icons")]
    public static void Restore()
    {
        RestoreIcon("Assets/Units/Commands/Build Habitat.asset", "722bb5e635d66aa4eb800775fb6cdbf1");
        RestoreIcon("Assets/Units/Commands/Build Solar Panel.asset", "e7c49208e47e57c47b55c30f3f47b29d");

        AssetDatabase.SaveAssets();
        Debug.Log("[RestoreIcons] Restored the original UI Icons to the build commands!");
    }

    private static void RestoreIcon(string cmdPath, string spriteGuid)
    {
        BuildBuildingCommand cmd = AssetDatabase.LoadAssetAtPath<BuildBuildingCommand>(cmdPath);
        string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        if (cmd != null && sprite != null)
        {
            SerializedObject so = new SerializedObject(cmd);
            SerializedProperty iconProp = so.FindProperty("<Icon>k__BackingField");
            if (iconProp != null)
            {
                iconProp.objectReferenceValue = sprite;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cmd);
                Debug.Log($"Restored {sprite.name} to {cmd.name}");
            }
        }
    }
}
