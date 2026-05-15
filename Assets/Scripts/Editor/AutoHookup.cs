using UnityEditor;
using UnityEngine;
using GameDevTV.RTS.Environment;
using System.Collections.Generic;

[InitializeOnLoad]
public class AutoHookup
{
    static AutoHookup()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        PlanetConfig config = AssetDatabase.LoadAssetAtPath<PlanetConfig>("Assets/Settings/Planet 1 - Easy.asset");
        if (config != null)
        {
            config.MapWidth = 100;
            config.MapHeight = 100;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ProceduralAssets/Prefabs", "Assets/SciFi Pack/Prefabs" });
            List<GameObject> prefabs = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
                if ((path.Contains("rock") || path.Contains("boulder")) && !path.Contains("tentacle"))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) prefabs.Add(prefab);
                }
            }
            
            if (prefabs.Count > 0)
            {
                config.SurfaceRockPrefabs = prefabs.ToArray();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AutoHookup] Successfully found and hooked up {prefabs.Count} Rock/Boulder prefabs to your Planet Config!");
            }
        }
    }
}
