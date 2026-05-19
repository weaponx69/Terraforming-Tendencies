using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public class BehaviorGraphValidator
{
    static BehaviorGraphValidator()
    {
        EditorApplication.delayCall += ValidateGraphs;
    }

    private static void ValidateGraphs()
    {
        if (SessionState.GetBool("BehaviorGraphsValidated", false))
        {
            return; // Only run once per editor session
        }

        string[] guids = AssetDatabase.FindAssets("t:Unity.Behavior.BehaviorGraph");
        List<string> paths = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            paths.Add(path);
        }

        if (paths.Count > 0)
        {
            Debug.Log($"[BehaviorGraphValidator] Force reserializing {paths.Count} Behavior Graphs to fix assembly references...");
            AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            SessionState.SetBool("BehaviorGraphsValidated", true);
            Debug.Log("[BehaviorGraphValidator] Successfully validated Behavior Graphs!");
        }
    }
}
