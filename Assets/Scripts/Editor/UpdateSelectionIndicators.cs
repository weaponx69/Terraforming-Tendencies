using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

public class UpdateSelectionIndicators : EditorWindow
{
    [MenuItem("Tools/Update Selection Indicators (Fix Warping)")]
    public static void UpdateIndicators()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject");
        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                AbstractCommandable commandable = prefab.GetComponent<AbstractCommandable>();
                if (commandable != null)
                {
                    // Use reflection to get the protected selectionIndicator field
                    var field = typeof(AbstractCommandable).GetField("selectionIndicator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        GameObject indicator = (GameObject)field.GetValue(commandable);
                        if (indicator != null)
                        {
                            if (indicator.GetComponent<TessellatedPlaneGenerator>() == null)
                            {
                                indicator.AddComponent<TessellatedPlaneGenerator>();
                                
                                // Get the actual instance in the prefab
                                GameObject prefabInstance = PrefabUtility.LoadPrefabContents(path);
                                AbstractCommandable instCommandable = prefabInstance.GetComponent<AbstractCommandable>();
                                GameObject instIndicator = (GameObject)field.GetValue(instCommandable);
                                
                                if (instIndicator != null)
                                {
                                    instIndicator.AddComponent<TessellatedPlaneGenerator>();
                                    // Make it face upwards if it's currently a flat floor quad
                                    TessellatedPlaneGenerator gen = instIndicator.GetComponent<TessellatedPlaneGenerator>();
                                    if (instIndicator.transform.localRotation.eulerAngles.x == 90f)
                                    {
                                        gen.faceUpwards = true;
                                        instIndicator.transform.localRotation = Quaternion.identity;
                                    }
                                    
                                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
                                    updatedCount++;
                                    Debug.Log($"[Indicator Fix] Added TessellatedPlaneGenerator to {prefab.name}'s Selection Indicator.");
                                }
                                PrefabUtility.UnloadPrefabContents(prefabInstance);
                            }
                        }
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Indicator Fix] Finished! Updated {updatedCount} prefabs.");
    }
}
