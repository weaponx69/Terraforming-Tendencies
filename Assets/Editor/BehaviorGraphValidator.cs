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
        // Block execution if we are in play mode, transitioning, or if the editor is busy
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
        {
            return; 
        }

        if (SessionState.GetBool("BehaviorGraphsValidated_V3", false))
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
            SessionState.SetBool("BehaviorGraphsValidated_V3", true);
            Debug.Log("[BehaviorGraphValidator] Successfully validated Behavior Graphs!");
        }
    }
}
