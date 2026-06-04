using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs once per editor session to rebuild all BehaviorAuthoringGraph runtime data.
/// The Unity Behavior package only rebuilds compiled runtime graph modules (SwitchComposite
/// children, node trees, etc.) when assets are saved through the visual graph editor.
/// This validator replicates that process via reflection so that stale compiled modules
/// are corrected automatically on domain reload without needing to open the editor.
/// </summary>
[InitializeOnLoad]
public class BehaviorGraphValidator
{
    private const string k_SessionKey = "BehaviorGraphsRebuilt_V1";

    static BehaviorGraphValidator()
    {
        EditorApplication.delayCall += RebuildAllGraphs;
    }

    [MenuItem("Tools/Behavior/Force Rebuild All Behavior Graphs")]
    public static void ForceRebuildAll()
    {
        SessionState.EraseBool(k_SessionKey);
        RebuildAllGraphs();
    }

    private static void RebuildAllGraphs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (SessionState.GetBool(k_SessionKey, false))
            return;

        // Locate BehaviorAuthoringGraph type and its internal rebuild method via reflection.
        System.Type authoringType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            authoringType = asm.GetType("Unity.Behavior.BehaviorAuthoringGraph");
            if (authoringType != null) break;
        }

        if (authoringType == null)
        {
            Debug.LogWarning("[BehaviorGraphValidator] BehaviorAuthoringGraph type not found — skipping rebuild.");
            return;
        }

        MethodInfo rebuildMethod = authoringType.GetMethod(
            "RebuildGraphAndBlackboardRuntimeData",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (rebuildMethod == null)
        {
            Debug.LogWarning("[BehaviorGraphValidator] RebuildGraphAndBlackboardRuntimeData not found — skipping rebuild.");
            return;
        }

        // Find every .asset that is a BehaviorAuthoringGraph (main asset type check).
        string[] allAssetGuids = AssetDatabase.FindAssets("t:ScriptableObject");
        var toRebuild = new List<Object>();

        foreach (string guid in allAssetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".asset")) continue;

            System.Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainType == authoringType)
            {
                Object graph = AssetDatabase.LoadAssetAtPath(path, authoringType);
                if (graph != null)
                    toRebuild.Add(graph);
            }
        }

        if (toRebuild.Count == 0)
        {
            SessionState.SetBool(k_SessionKey, true);
            return;
        }

        Debug.Log($"[BehaviorGraphValidator] Rebuilding runtime data for {toRebuild.Count} Behavior Graph(s)...");

        foreach (Object graph in toRebuild)
        {
            try
            {
                rebuildMethod.Invoke(graph, null);
            }
            catch (System.Exception ex)
            {
                var realEx = ex is System.Reflection.TargetInvocationException ? ex.InnerException : ex;
                Debug.LogError($"[BehaviorGraphValidator] Failed to rebuild graph at {AssetDatabase.GetAssetPath(graph)}: {realEx?.Message}\n{realEx?.StackTrace}");
            }
}

        AssetDatabase.SaveAssets();
        SessionState.SetBool(k_SessionKey, true);
        Debug.Log("[BehaviorGraphValidator] Behavior Graph runtime data rebuild complete.");
    }
}
