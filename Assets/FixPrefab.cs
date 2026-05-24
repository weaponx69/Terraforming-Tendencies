using UnityEditor;
using UnityEngine;
using GameDevTV.RTS.Units;

public class FixPrefab
{
    public static void Fix()
    {
        string path = "Assets/Units/Buildings/Oxygen Processor/Oxygen Processor.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            OxygenProcessor op = prefab.GetComponent<OxygenProcessor>();
            if (op != null)
            {
                Renderer r = prefab.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    var serializedObject = new SerializedObject(op);
                    serializedObject.FindProperty("<MainRenderer>k__BackingField").objectReferenceValue = r;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(op);
                    PrefabUtility.SavePrefabAsset(prefab);
                    Debug.Log("Successfully assigned MainRenderer!");
                }
            }
        }
    }
}
