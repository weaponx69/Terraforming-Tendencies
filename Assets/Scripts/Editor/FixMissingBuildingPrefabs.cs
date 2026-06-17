using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;

public class FixMissingBuildingPrefabs : EditorWindow
{
    [MenuItem("Tools/Fix Missing Building Prefabs")]
    public static void FixPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingSO");
        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingSO so = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            
            if (so != null && so.Prefab == null)
            {
                // Try to find a prefab with the same name in the same folder
                string folderPath = System.IO.Path.GetDirectoryName(path);
                string prefabPath = folderPath + "/" + so.name + ".prefab";
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab == null)
                {
                    // Fallback: search whole project for a prefab with this name
                    string[] prefabGuids = AssetDatabase.FindAssets(so.name + " t:Prefab");
                    foreach (string pGuid in prefabGuids)
                    {
                        string pPath = AssetDatabase.GUIDToAssetPath(pGuid);
                        // Make sure it's an exact name match (to avoid matching "Command Post Variant")
                        if (System.IO.Path.GetFileNameWithoutExtension(pPath) == so.name)
                        {
                            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                            break;
                        }
                    }
                }

                if (prefab != null)
                {
                    SerializedObject serializedSO = new SerializedObject(so);
                    SerializedProperty prefabProp = serializedSO.FindProperty("prefab");
                    if (prefabProp == null) prefabProp = serializedSO.FindProperty("<Prefab>k__BackingField");
                    
                    if (prefabProp != null)
                    {
                        prefabProp.objectReferenceValue = prefab;
                        serializedSO.ApplyModifiedProperties();
                        EditorUtility.SetDirty(so);
                        updatedCount++;
                        Debug.Log($"[FixPrefabs] Successfully linked prefab {prefab.name} to {so.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[FixPrefabs] Could not find a matching prefab for {so.name} at {path}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixPrefabs] Finished! Fixed {updatedCount} missing prefabs.");
    }
}
